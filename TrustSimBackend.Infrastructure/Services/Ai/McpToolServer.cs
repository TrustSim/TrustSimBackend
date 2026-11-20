using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Ai.Plugins;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Services.Ai;

/// <summary>
/// Internal Model Context Protocol (MCP) server managing tool schemas, RBAC permission gates,
/// argument guardrails, JSON-RPC 2.0 dispatch, and RAG provenance collection.
/// </summary>
public class McpToolServer : IMcpToolServer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAiAuthorizationService _authService;
    private readonly IAiGuardrailService _guardrailService;
    private readonly ILogger<McpToolServer> _logger;
    private readonly Dictionary<string, RegisteredTool> _tools;

    private sealed record RegisteredTool(
        McpToolDescriptor Descriptor,
        Func<IServiceProvider, JsonElement, CancellationToken, Task<McpToolExecutionResult>> Handler);

    public McpToolServer(
        IServiceScopeFactory scopeFactory,
        IAiAuthorizationService authService,
        IAiGuardrailService guardrailService,
        ILogger<McpToolServer> logger)
    {
        _scopeFactory = scopeFactory;
        _authService = authService;
        _guardrailService = guardrailService;
        _logger = logger;
        _tools = BuildToolRegistry();
    }

    public IReadOnlyList<McpToolDescriptor> GetAllToolDescriptors()
    {
        return _tools.Values.Select(t => t.Descriptor).ToList();
    }

    public IReadOnlyList<McpToolDescriptor> ListAuthorizedTools(
        AiUserContext userContext,
        IReadOnlyCollection<string>? nameFilter = null)
    {
        var filterSet = nameFilter is { Count: > 0 }
            ? new HashSet<string>(nameFilter, StringComparer.OrdinalIgnoreCase)
            : null;

        return _tools.Values
            .Select(t => t.Descriptor)
            .Where(d => filterSet == null || filterSet.Contains(d.Name))
            .Where(d => _authService.AuthorizeToolAccess(d, userContext).Allowed)
            .ToList();
    }

    public async Task<McpToolExecutionResult> CallToolAsync(
        string toolName,
        JsonElement arguments,
        AiUserContext userContext,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (!_tools.TryGetValue(toolName, out var registered))
        {
            return new McpToolExecutionResult
            {
                ToolName = toolName,
                IsError = true,
                ContentJson = JsonSerializer.Serialize(new { error = "UNKNOWN_TOOL", message = $"Công cụ '{toolName}' không tồn tại." }),
                Summary = $"Không tìm thấy công cụ {toolName}",
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }

        var (allowed, denialMessage) = _authService.AuthorizeToolAccess(registered.Descriptor, userContext);
        if (!allowed)
        {
            _logger.LogWarning("[MCP AuthZ Gate] Denied tool {Tool} for identity {Identity}", toolName, userContext.RateLimitIdentity);
            return new McpToolExecutionResult
            {
                ToolName = toolName,
                IsError = true,
                PermissionDenied = true,
                ContentJson = JsonSerializer.Serialize(new
                {
                    error = "UNAUTHORIZED_TOOL",
                    requiresAuth = true,
                    message = denialMessage
                }),
                Summary = $"Từ chối quyền truy cập công cụ {toolName}",
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }

        var (argsValid, validationError) = _guardrailService.ValidateToolArguments(toolName, arguments);
        if (!argsValid)
        {
            _logger.LogWarning("[MCP Guardrail] Invalid arguments for tool {Tool}: {Error}", toolName, validationError);
            return new McpToolExecutionResult
            {
                ToolName = toolName,
                IsError = true,
                ContentJson = JsonSerializer.Serialize(new
                {
                    error = "INVALID_TOOL_ARGUMENTS",
                    message = validationError
                }),
                Summary = $"Tham số không hợp lệ cho công cụ {toolName}",
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var result = await registered.Handler(scope.ServiceProvider, arguments, cancellationToken);
            result.ToolName = toolName;
            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MCP Execution Error] Tool {Tool} threw an exception.", toolName);
            return new McpToolExecutionResult
            {
                ToolName = toolName,
                IsError = true,
                ContentJson = JsonSerializer.Serialize(new
                {
                    error = "TOOL_EXECUTION_ERROR",
                    message = "Không thể thực thi công cụ ở thời điểm hiện tại."
                }),
                Summary = $"Lỗi thực thi công cụ {toolName}",
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public async Task<McpJsonRpcResponse> HandleJsonRpcAsync(
        McpJsonRpcRequest request,
        AiUserContext userContext,
        CancellationToken cancellationToken = default)
    {
        switch (request.Method)
        {
            case "server/discover":
                return new McpJsonRpcResponse
                {
                    Id = request.Id,
                    Result = new
                    {
                        protocolVersion = "2026-07-28",
                        serverInfo = new { name = "TrustSim.McpToolServer", version = "1.0.0" },
                        capabilities = new
                        {
                            tools = new { listChanged = false },
                            resources = new { subscribe = false }
                        }
                    }
                };

            case "tools/list":
            {
                var authorized = ListAuthorizedTools(userContext);
                return new McpJsonRpcResponse
                {
                    Id = request.Id,
                    Result = new
                    {
                        tools = authorized.Select(t => new
                        {
                            name = t.Name,
                            description = t.Description,
                            inputSchema = t.InputSchema,
                            annotations = new
                            {
                                readOnlyHint = t.ReadOnlyHint,
                                requiredPermission = t.RequiredPermission.ToString()
                            }
                        })
                    }
                };
            }

            case "tools/call":
            {
                if (!request.Params.HasValue || !request.Params.Value.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
                {
                    return CreateJsonRpcError(request.Id, -32602, "Missing required 'name' parameter in tools/call.");
                }

                var toolName = nameProp.GetString() ?? string.Empty;
                var argsElement = request.Params.Value.TryGetProperty("arguments", out var argsProp)
                    ? argsProp
                    : JsonDocument.Parse("{}").RootElement;

                var execResult = await CallToolAsync(toolName, argsElement, userContext, cancellationToken);
                if (execResult.PermissionDenied)
                {
                    return CreateJsonRpcError(request.Id, -32001, execResult.Summary, JsonSerializer.Deserialize<object>(execResult.ContentJson));
                }

                return new McpJsonRpcResponse
                {
                    Id = request.Id,
                    Result = new
                    {
                        isError = execResult.IsError,
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = execResult.ContentJson
                            }
                        },
                        meta = new
                        {
                            summary = execResult.Summary,
                            executionTimeMs = execResult.ExecutionTimeMs,
                            referencedChunkIds = execResult.ReferencedChunkIds,
                            suggestedSimsCount = execResult.SuggestedSims.Count
                        }
                    }
                };
            }

            default:
                return CreateJsonRpcError(request.Id, -32601, $"Unsupported MCP method '{request.Method}'.");
        }
    }

    private static McpJsonRpcResponse CreateJsonRpcError(object? id, int code, string message, object? data = null)
    {
        return new McpJsonRpcResponse
        {
            Id = id,
            Error = new McpJsonRpcError
            {
                Code = code,
                Message = message,
                Data = data
            }
        };
    }

    private Dictionary<string, RegisteredTool> BuildToolRegistry()
    {
        var list = new List<RegisteredTool>
        {
            CreateTool(
                name: "SearchAvailableSims",
                description: "Tìm kiếm danh sách số SIM thực tế đang mở bán trên sàn TrustSim theo ngân sách, nhà mạng, đầu số hoặc mẫu số. CHỈ gọi khi người dùng muốn tìm SIM mua hoặc cần gợi ý danh sách số có thể mua.",
                schema: new
                {
                    type = "object",
                    properties = new
                    {
                        carrier = new { type = "string", description = "Tên nhà mạng (Viettel, VinaPhone, MobiFone, Vietnamobile, Wintel, ITelecom) nếu người dùng có yêu cầu." },
                        prefix = new { type = "string", description = "Đầu số cụ thể (ví dụ: 098, 090, 091, 088, 086...) nếu người dùng có yêu cầu." },
                        keyword = new { type = "string", description = "Thế số hoặc đuôi số cần tìm (ví dụ: 6868, 7979, 9999, 1368, 888...) nếu người dùng có yêu cầu." },
                        minPrice = new { type = "number", description = "Giá tối thiểu bằng VNĐ nếu có." },
                        maxPrice = new { type = "number", description = "Giá tối đa bằng VNĐ nếu có." }
                    }
                },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.SimSearch, AiIntentType.FengShuiAndBirthYear, AiIntentType.GeneralConsultation },
                handler: ExecuteSearchAvailableSimsAsync),

            CreateTool(
                name: "GetSimDetails",
                description: "Lấy thông tin chi tiết một số SIM cụ thể trên sàn TrustSim.",
                schema: new
                {
                    type = "object",
                    properties = new
                    {
                        phoneNumber = new { type = "string", description = "Số điện thoại cần xem chi tiết (ví dụ: 0986868686)" }
                    },
                    required = new[] { "phoneNumber" }
                },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.SimSearch, AiIntentType.SimValuation },
                handler: ExecuteGetSimDetailsAsync),

            CreateTool(
                name: "QueryFengShuiKnowledge",
                description: "Truy vấn kho tri thức RAG phong thủy ngũ hành, can chi và cẩm nang số học từ TrustSim. CHỈ gọi khi người dùng hỏi về kiến thức phong thủy, hợp mệnh, ý nghĩa đuôi số.",
                schema: new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Câu hỏi hoặc từ khóa tìm kiếm phong thủy" }
                    },
                    required = new[] { "query" }
                },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.FengShuiAndBirthYear, AiIntentType.SimSearch, AiIntentType.GeneralConsultation },
                handler: ExecuteQueryFengShuiKnowledgeAsync),

            CreateTool(
                name: "GetFengShuiGuidelineByBirthYear",
                description: "Tra cứu bản mệnh ngũ hành, quái số và các cặp số cát tường tương sinh theo năm sinh dương lịch.",
                schema: new
                {
                    type = "object",
                    properties = new
                    {
                        birthYear = new { type = "integer", description = "Năm sinh dương lịch của khách hàng (ví dụ: 2004, 1990)" }
                    },
                    required = new[] { "birthYear" }
                },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.FengShuiAndBirthYear, AiIntentType.SimSearch, AiIntentType.GeneralConsultation },
                handler: (sp, args, _) =>
                {
                    var plugin = sp.GetRequiredService<FengShuiKnowledgePlugin>();
                    int year = GetInt(args, "birthYear") ?? 2000;
                    var json = plugin.GetFengShuiGuidelineByBirthYear(year);
                    return Task.FromResult(new McpToolExecutionResult
                    {
                        ContentJson = json,
                        Summary = $"Đã xác định bản mệnh ngũ hành & số cát tường năm sinh {year}"
                    });
                }),

            CreateTool(
                name: "GetActiveAuctions",
                description: "Lấy danh sách tất cả các phiên đấu giá SIM số đẹp VIP (ACTIVE: đang diễn ra, SCHEDULE: sắp diễn ra) trên sàn TrustSim. Gọi khi khách hỏi về đấu giá hoặc số lượng sim đang đấu giá.",
                schema: new
                {
                    type = "object",
                    properties = new
                    {
                        status = new { type = "string", description = "Trạng thái: 'ACTIVE', 'SCHEDULE', hoặc để trống để lấy tất cả" }
                    }
                },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.Auction, AiIntentType.GeneralConsultation },
                handler: ExecuteGetActiveAuctionsAsync),

            CreateTool(
                name: "GetAuctionDetail",
                description: "Lấy thông tin chi tiết một phiên đấu giá SIM VIP theo mã phiên (AuctionCode) bao gồm giá hiện tại, bước giá, tiền đặt cọc và thời hạn.",
                schema: new
                {
                    type = "object",
                    properties = new
                    {
                        auctionCode = new { type = "string", description = "Mã phiên đấu giá (ví dụ: AUC123456)" }
                    },
                    required = new[] { "auctionCode" }
                },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.Auction },
                handler: async (sp, args, ct) =>
                {
                    var plugin = sp.GetRequiredService<AuctionPlugin>();
                    var code = GetString(args, "auctionCode") ?? string.Empty;
                    var json = await plugin.GetAuctionDetailAsync(code, ct);
                    return new McpToolExecutionResult
                    {
                        ContentJson = json,
                        Summary = $"Đã tra cứu chi tiết phiên đấu giá {code}"
                    };
                }),

            CreateTool(
                name: "GetEscrowAndSafetyPolicy",
                description: "Giải thích quy trình giao dịch đảm bảo (Escrow), giữ tiền an toàn 100% và bảo vệ người mua trên TrustSim.",
                schema: new { type = "object", properties = new { } },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.EscrowAndPolicy, AiIntentType.Auction, AiIntentType.GeneralConsultation },
                handler: (sp, _, _) =>
                {
                    var plugin = sp.GetRequiredService<TradeAndEscrowPlugin>();
                    var json = plugin.GetEscrowAndSafetyPolicy();
                    return Task.FromResult(new McpToolExecutionResult
                    {
                        ContentJson = json,
                        Summary = "Đã tra cứu chính sách giao dịch đảm bảo TrustSim Escrow"
                    });
                }),

            CreateTool(
                name: "CalculateTradeFees",
                description: "Tính toán chính xác phí sàn và tiền đặt cọc cần thiết khi mua hoặc đấu giá một số SIM.",
                schema: new
                {
                    type = "object",
                    properties = new
                    {
                        price = new { type = "number", description = "Mức giá giao dịch của số SIM (VND)" }
                    },
                    required = new[] { "price" }
                },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.EscrowAndPolicy, AiIntentType.Auction, AiIntentType.WalletAndDeposit },
                handler: (sp, args, _) =>
                {
                    var plugin = sp.GetRequiredService<TradeAndEscrowPlugin>();
                    var price = GetDecimal(args, "price") ?? 0m;
                    var json = plugin.CalculateTradeFees(price);
                    return Task.FromResult(new McpToolExecutionResult
                    {
                        ContentJson = json,
                        Summary = $"Đã tính toán biểu phí sàn & tiền đặt cọc cho mức giá {price:N0} đ"
                    });
                }),

            CreateTool(
                name: "GetMyWalletBalance",
                description: "Tra cứu số dư ví khả dụng hiện tại của người dùng đã đăng nhập.",
                schema: new { type = "object", properties = new { } },
                permission: McpToolPermissionLevel.Authenticated,
                intents: new[] { AiIntentType.WalletAndDeposit },
                handler: async (sp, _, ct) =>
                {
                    var plugin = sp.GetRequiredService<UserWalletPlugin>();
                    var json = await plugin.GetMyWalletBalanceAsync(ct);
                    return new McpToolExecutionResult
                    {
                        ContentJson = json,
                        Summary = "Đã kiểm tra số dư ví khả dụng của tài khoản"
                    };
                }),

            CreateTool(
                name: "GetSePayDepositGuideline",
                description: "Cung cấp thông tin và hướng dẫn nạp tiền vào ví sàn TrustSim qua SePay VietQR cho người dùng đã đăng nhập.",
                schema: new
                {
                    type = "object",
                    properties = new
                    {
                        amount = new { type = "number", description = "Số tiền muốn nạp vào ví (VND)" }
                    },
                    required = new[] { "amount" }
                },
                permission: McpToolPermissionLevel.Authenticated,
                intents: new[] { AiIntentType.WalletAndDeposit },
                handler: (sp, args, _) =>
                {
                    var plugin = sp.GetRequiredService<UserWalletPlugin>();
                    var amount = GetDecimal(args, "amount") ?? 100000m;
                    var json = plugin.GetSePayDepositGuideline(amount);
                    return Task.FromResult(new McpToolExecutionResult
                    {
                        ContentJson = json,
                        Summary = "Đã lấy thông tin chuyển khoản nạp tiền tự động SePay VietQR"
                    });
                }),

            CreateTool(
                name: "AnalyzeSimCadence",
                description: "Bóc tách thế nhịp ngắt và các tổ hợp phong thủy của một số SIM cụ thể.",
                schema: new
                {
                    type = "object",
                    properties = new
                    {
                        phoneNumber = new { type = "string", description = "Số điện thoại cần bóc tách phân tích" }
                    },
                    required = new[] { "phoneNumber" }
                },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.SimValuation },
                handler: (sp, args, _) =>
                {
                    var plugin = sp.GetRequiredService<SimCadencePlugin>();
                    var phone = GetString(args, "phoneNumber") ?? string.Empty;
                    var json = plugin.AnalyzeSimCadence(phone);
                    return Task.FromResult(new McpToolExecutionResult
                    {
                        ContentJson = json,
                        Summary = $"Đã bóc tách thế nhịp và âm luật số {phone}"
                    });
                }),

            CreateTool(
                name: "EvaluateSimPrice",
                description: "Định giá nhanh một số điện thoại dựa trên thế nhịp ngắt, uy tín đầu số và bóc tách đặc trưng số học. Gọi khi người dùng hỏi số này giá bao nhiêu hoặc yêu cầu thẩm định giá.",
                schema: new
                {
                    type = "object",
                    properties = new
                    {
                        phoneNumber = new { type = "string", description = "Số điện thoại cần định giá (ví dụ: 0988888888, 0903123456)" }
                    },
                    required = new[] { "phoneNumber" }
                },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.SimValuation, AiIntentType.GeneralConsultation },
                handler: (sp, args, _) =>
                {
                    var plugin = sp.GetRequiredService<SimValuationPlugin>();
                    var phone = GetString(args, "phoneNumber") ?? string.Empty;
                    var json = plugin.EvaluateSimPrice(phone);
                    return Task.FromResult(new McpToolExecutionResult
                    {
                        ContentJson = json,
                        Summary = $"Đã thẩm định cấu trúc giá trị và phân khúc thị trường số {phone}"
                    });
                }),

            CreateTool(
                name: "GetMarketPriceComparables",
                description: "Tra cứu khung giá thị trường tham chiếu, khoảng giá giao dịch thực tế và biên độ thanh khoản theo thể loại số SIM (Tứ quý, Tam hoa, Thần tài, Lộc phát...) và đầu số.",
                schema: new
                {
                    type = "object",
                    properties = new
                    {
                        categoryCode = new { type = "string", description = "Mã thể loại SIM (ví dụ: TU_QUY, TAM_HOA, THAN_TAI, LOC_PHAT, SANH_TIEN, TAXI, NGU_QUY)" },
                        prefix = new { type = "string", description = "Đầu số nhà mạng (ví dụ: 098, 090, 091, 088...)" }
                    }
                },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.MarketBenchmark, AiIntentType.SimValuation, AiIntentType.GeneralConsultation },
                handler: async (sp, args, ct) =>
                {
                    var plugin = sp.GetRequiredService<SimMarketHistoryPlugin>();
                    var cat = GetString(args, "categoryCode");
                    var pref = GetString(args, "prefix");
                    var json = await plugin.GetMarketPriceComparablesAsync(cat, pref, ct);
                    return new McpToolExecutionResult
                    {
                        ContentJson = json,
                        Summary = $"Đã tra cứu biểu giá tham chiếu thị trường cho thể loại {cat ?? "chung"}"
                    };
                }),

            CreateTool(
                name: "GetSimCategories",
                description: "Lấy danh sách các thể loại SIM số đẹp phổ biến trên sàn TrustSim kèm hệ số nhân giá trị.",
                schema: new { type = "object", properties = new { } },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.MarketBenchmark, AiIntentType.SimSearch },
                handler: async (sp, _, ct) =>
                {
                    var plugin = sp.GetRequiredService<SimMarketHistoryPlugin>();
                    var json = await plugin.GetSimCategoriesAsync(ct);
                    return new McpToolExecutionResult
                    {
                        ContentJson = json,
                        Summary = "Đã tra cứu danh mục thể loại số đẹp trên sàn TrustSim"
                    };
                }),

            CreateTool(
                name: "GetCarrierInfo",
                description: "Tra cứu thông tin chi tiết về nhà mạng viễn thông (Viettel, VinaPhone, MobiFone...), danh sách đầu số và quy trình thủ tục sang tên chính chủ.",
                schema: new
                {
                    type = "object",
                    properties = new
                    {
                        carrierName = new { type = "string", description = "Tên hoặc mã nhà mạng (Viettel, VinaPhone, MobiFone...)" }
                    },
                    required = new[] { "carrierName" }
                },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.EscrowAndPolicy },
                handler: async (sp, args, ct) =>
                {
                    var plugin = sp.GetRequiredService<CarrierPolicyPlugin>();
                    var carrier = GetString(args, "carrierName") ?? string.Empty;
                    var json = await plugin.GetCarrierInfoAsync(carrier, ct);
                    return new McpToolExecutionResult
                    {
                        ContentJson = json,
                        Summary = $"Đã tra cứu thông tin nhà mạng & thủ tục sang tên {carrier}"
                    };
                }),

            CreateTool(
                name: "GetPrefixInfo",
                description: "Tra cứu thông tin, điểm uy tín và ý nghĩa phong thủy của một đầu số điện thoại (ví dụ: 098, 090, 091, 088, 086...).",
                schema: new
                {
                    type = "object",
                    properties = new
                    {
                        prefixValue = new { type = "string", description = "Đầu số cần tra cứu (ví dụ: 098, 090, 091, 086, 088...)" }
                    },
                    required = new[] { "prefixValue" }
                },
                permission: McpToolPermissionLevel.Anonymous,
                intents: new[] { AiIntentType.FengShuiAndBirthYear, AiIntentType.SimValuation, AiIntentType.MarketBenchmark, AiIntentType.EscrowAndPolicy, AiIntentType.GeneralConsultation },
                handler: async (sp, args, ct) =>
                {
                    var plugin = sp.GetRequiredService<CarrierPolicyPlugin>();
                    var prefix = GetString(args, "prefixValue") ?? string.Empty;
                    var json = await plugin.GetPrefixInfoAsync(prefix, ct);
                    return new McpToolExecutionResult
                    {
                        ContentJson = json,
                        Summary = $"Đã tra cứu điểm uy tín & ý nghĩa phong thủy đầu số {prefix}"
                    };
                })
        };

        return list.ToDictionary(t => t.Descriptor.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static RegisteredTool CreateTool(
        string name,
        string description,
        object schema,
        McpToolPermissionLevel permission,
        AiIntentType[] intents,
        Func<IServiceProvider, JsonElement, CancellationToken, Task<McpToolExecutionResult>> handler)
    {
        return new RegisteredTool(
            new McpToolDescriptor
            {
                Name = name,
                Description = description,
                InputSchema = schema,
                ReadOnlyHint = true,
                RequiredPermission = permission,
                SupportedIntents = intents
            },
            handler);
    }

    private static async Task<McpToolExecutionResult> ExecuteSearchAvailableSimsAsync(
        IServiceProvider sp,
        JsonElement args,
        CancellationToken ct)
    {
        var simRepo = sp.GetRequiredService<ISimNumberRepository>();
        decimal? minPrice = GetDecimal(args, "minPrice");
        decimal? maxPrice = GetDecimal(args, "maxPrice");
        string? carrier = GetString(args, "carrier");
        string? prefix = GetString(args, "prefix");
        string? keyword = GetString(args, "keyword");

        var sims = await simRepo.GetAvailableSimsForConsultationAsync(minPrice, maxPrice, carrier, prefix, keyword, limit: 6, ct);
        var suggestions = sims.Select(s => new ConsultationSimSuggestion
        {
            SimNumberId = s.Id,
            PhoneNumber = s.PhoneNumber,
            Price = s.Price,
            CarrierName = s.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông",
            HighlightReason = $"{s.FormatRhythm?.FormatRhythmName ?? "Số đẹp phong thủy"} - Đầu {s.PhonePrefix?.PrefixValue}"
        }).ToList();

        var resultJson = JsonSerializer.Serialize(new
        {
            total = sims.Count,
            items = suggestions.Select(s => new
            {
                simNumberId = s.SimNumberId,
                phoneNumber = s.PhoneNumber,
                price = s.Price,
                carrier = s.CarrierName,
                highlight = s.HighlightReason
            })
        });

        var summary = suggestions.Count > 0
            ? $"Tìm thấy {suggestions.Count} số SIM thực tế đang mở bán trên sàn TrustSim"
            : "Không tìm thấy số SIM phù hợp với tiêu chí trong kho hiện tại";

        return new McpToolExecutionResult
        {
            ContentJson = resultJson,
            Summary = summary,
            SuggestedSims = suggestions
        };
    }

    private static async Task<McpToolExecutionResult> ExecuteGetSimDetailsAsync(
        IServiceProvider sp,
        JsonElement args,
        CancellationToken ct)
    {
        var simRepo = sp.GetRequiredService<ISimNumberRepository>();
        string phone = GetString(args, "phoneNumber") ?? string.Empty;
        var sim = await simRepo.GetByPhoneNumberAsync(phone, ct);
        if (sim == null)
        {
            return new McpToolExecutionResult
            {
                ContentJson = "{\"error\":\"Không tìm thấy số SIM này trong hệ thống.\"}",
                Summary = $"Không tìm thấy số {phone}"
            };
        }

        var suggestion = new ConsultationSimSuggestion
        {
            SimNumberId = sim.Id,
            PhoneNumber = sim.PhoneNumber,
            Price = sim.Price,
            CarrierName = sim.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông",
            HighlightReason = $"{sim.FormatRhythm?.FormatRhythmName ?? "Số đẹp phong thủy"} - Đầu {sim.PhonePrefix?.PrefixValue}"
        };

        var json = JsonSerializer.Serialize(new
        {
            phoneNumber = sim.PhoneNumber,
            price = sim.Price,
            status = sim.SimStatus.ToString(),
            carrier = suggestion.CarrierName,
            rhythm = sim.FormatRhythm?.FormatRhythmName
        });

        return new McpToolExecutionResult
        {
            ContentJson = json,
            Summary = $"Tra cứu chi tiết số {phone}",
            SuggestedSims = new List<ConsultationSimSuggestion> { suggestion }
        };
    }

    private static async Task<McpToolExecutionResult> ExecuteQueryFengShuiKnowledgeAsync(
        IServiceProvider sp,
        JsonElement args,
        CancellationToken ct)
    {
        var plugin = sp.GetRequiredService<FengShuiKnowledgePlugin>();
        var query = GetString(args, "query") ?? string.Empty;
        var json = await plugin.QueryFengShuiKnowledgeAsync(query, ct);

        var chunkIds = new List<Guid>();
        var docs = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("chunkId", out var cidProp) && cidProp.TryGetGuid(out var cid))
                    {
                        chunkIds.Add(cid);
                    }
                    if (item.TryGetProperty("document", out var dProp) && dProp.ValueKind == JsonValueKind.String)
                    {
                        var docName = dProp.GetString();
                        if (!string.IsNullOrWhiteSpace(docName) && !docs.Contains(docName))
                        {
                            docs.Add(docName);
                        }
                    }
                }
            }
        }
        catch
        {
            // Keep raw JSON result if provenance metadata parsing fails.
        }

        if (docs.Count == 0 && chunkIds.Count > 0)
        {
            docs.Add("Cẩm nang Phong Thủy & Số Học TrustSim");
        }

        return new McpToolExecutionResult
        {
            ContentJson = json,
            Summary = chunkIds.Count > 0
                ? $"Đã tra cứu {chunkIds.Count} đoạn tri thức phong thủy RAG từ cẩm nang TrustSim"
                : "Đã tra cứu kho tri thức phong thủy TrustSim",
            ReferencedChunkIds = chunkIds,
            ReferencedDocuments = docs
        };
    }

    private static async Task<McpToolExecutionResult> ExecuteGetActiveAuctionsAsync(
        IServiceProvider sp,
        JsonElement args,
        CancellationToken ct)
    {
        var plugin = sp.GetRequiredService<AuctionPlugin>();
        var status = GetString(args, "status");
        var json = await plugin.GetActiveAuctionsAsync(status, ct);

        var auctionSims = new List<ConsultationSimSuggestion>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var it in items.EnumerateArray())
                {
                    if (it.TryGetProperty("simNumberId", out var sidProp) && sidProp.TryGetGuid(out var sid))
                    {
                        var aCode = it.TryGetProperty("auctionCode", out var ac) ? ac.GetString() ?? "" : "";
                        var aStatus = it.TryGetProperty("status", out var stp) ? stp.GetString() ?? "" : "";
                        auctionSims.Add(new ConsultationSimSuggestion
                        {
                            SimNumberId = sid,
                            PhoneNumber = it.TryGetProperty("phoneNumber", out var pn) ? pn.GetString() ?? "" : "",
                            Price = it.TryGetProperty("currentPrice", out var cp) ? cp.GetDecimal() : 0,
                            CarrierName = it.TryGetProperty("carrier", out var cr) ? cr.GetString() ?? "Viễn thông" : "Viễn thông",
                            HighlightReason = $"Đấu giá {aCode} ({aStatus})"
                        });
                    }
                }
            }
        }
        catch
        {
            // Ignore malformed auction item array while preserving raw JSON.
        }

        return new McpToolExecutionResult
        {
            ContentJson = json,
            Summary = "Đã tra cứu danh sách phiên đấu giá SIM VIP trên sàn TrustSim",
            SuggestedSims = auctionSims
        };
    }

    private static string? GetString(JsonElement args, string key) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static decimal? GetDecimal(JsonElement args, string key) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var val)
            ? val
            : null;

    private static int? GetInt(JsonElement args, string key) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var val)
            ? val
            : null;
}
