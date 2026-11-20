using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services.Ai;

/// <summary>
/// Routes consultation requests to the right model tier, narrowed MCP tool subset, and cache policy based on intent.
/// </summary>
public class AiRouterService : IAiRouterService
{
    private readonly GeminiSettings _settings;

    private static readonly Regex PhonePattern = new(@"(?:\+84|0)[35789]\d{8}|0\d{9}", RegexOptions.Compiled);
    private static readonly Regex YearPattern = new(@"\b(19[5-9]\d|20[0-2]\d)\b", RegexOptions.Compiled);

    private static readonly Dictionary<AiIntentType, string[]> IntentToolMap = new()
    {
        [AiIntentType.WalletAndDeposit] = new[]
        {
            "GetMyWalletBalance", "GetSePayDepositGuideline", "CalculateTradeFees"
        },
        [AiIntentType.Auction] = new[]
        {
            "GetActiveAuctions", "GetAuctionDetail", "SearchAvailableSims", "CalculateTradeFees", "GetEscrowAndSafetyPolicy"
        },
        [AiIntentType.SimValuation] = new[]
        {
            "EvaluateSimPrice", "AnalyzeSimCadence", "GetMarketPriceComparables", "GetPrefixInfo", "GetSimDetails"
        },
        [AiIntentType.MarketBenchmark] = new[]
        {
            "GetMarketPriceComparables", "GetSimCategories", "GetPrefixInfo"
        },
        [AiIntentType.FengShuiAndBirthYear] = new[]
        {
            "GetFengShuiGuidelineByBirthYear", "QueryFengShuiKnowledge", "GetPrefixInfo", "SearchAvailableSims"
        },
        [AiIntentType.EscrowAndPolicy] = new[]
        {
            "GetEscrowAndSafetyPolicy", "CalculateTradeFees", "GetCarrierInfo", "GetPrefixInfo"
        },
        [AiIntentType.SimSearch] = new[]
        {
            "SearchAvailableSims", "GetSimDetails", "GetFengShuiGuidelineByBirthYear", "QueryFengShuiKnowledge", "GetSimCategories"
        },
        [AiIntentType.GeneralConsultation] = new[]
        {
            "SearchAvailableSims", "EvaluateSimPrice", "GetFengShuiGuidelineByBirthYear", "QueryFengShuiKnowledge",
            "GetActiveAuctions", "GetEscrowAndSafetyPolicy", "GetMarketPriceComparables", "GetPrefixInfo"
        }
    };

    public AiRouterService(IOptions<GeminiSettings> settings)
    {
        _settings = settings.Value;
    }

    public AiRoutingDecision Route(
        string userMessage,
        string conversationHistory,
        int? userBirthYear,
        decimal? budgetMin,
        decimal? budgetMax,
        IReadOnlyList<McpToolDescriptor> availableTools)
    {
        var lower = (userMessage ?? string.Empty).ToLowerInvariant();
        var hasPhone = PhonePattern.IsMatch(userMessage ?? string.Empty);
        var hasBirthYear = userBirthYear.HasValue || YearPattern.IsMatch(userMessage ?? string.Empty);

        var (intent, tier, cacheable, reason) = ClassifyIntent(lower, hasPhone, hasBirthYear, budgetMin, budgetMax);

        // Disable response caching when multi-turn history is present or response depends on live/user state.
        var effectiveCacheable = _settings.EnableResponseCache
            && cacheable
            && string.IsNullOrWhiteSpace(conversationHistory);

        var mappedNames = IntentToolMap.TryGetValue(intent, out var names)
            ? names
            : IntentToolMap[AiIntentType.GeneralConsultation];

        var availableSet = new HashSet<string>(availableTools.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
        var allowedTools = mappedNames.Where(availableSet.Contains).ToList();
        if (allowedTools.Count == 0)
        {
            allowedTools = availableTools.Select(t => t.Name).ToList();
        }

        var ttlMinutes = _settings.ResponseCacheTtlMinutes > 0 ? _settings.ResponseCacheTtlMinutes : 30;

        return new AiRoutingDecision
        {
            Intent = intent,
            ModelTier = tier,
            CandidateModels = BuildCandidateModels(tier),
            AllowedToolNames = allowedTools,
            IsCacheable = effectiveCacheable,
            CacheTtl = TimeSpan.FromMinutes(ttlMinutes),
            RoutingReason = reason
        };
    }

    private static (AiIntentType Intent, AiModelTier Tier, bool Cacheable, string Reason) ClassifyIntent(
        string lower,
        bool hasPhone,
        bool hasBirthYear,
        decimal? budgetMin,
        decimal? budgetMax)
    {
        if (ContainsAny(lower, "số dư", "ví của tôi", "còn bao nhiêu tiền", "nạp tiền", "sepay", "vietqr", "chuyển khoản nạp"))
        {
            return (AiIntentType.WalletAndDeposit, AiModelTier.Fast, false, "Personal wallet/deposit query requires authenticated real-time lookup.");
        }

        var isExplicitInventorySearch = budgetMin.HasValue
            || budgetMax.HasValue
            || ContainsAny(lower, "tìm sim", "tìm số", "các số sim", "trong tầm giá", "tầm giá");

        if (ContainsAny(lower, "đấu giá", "phiên", "auction", "auc", "bước giá", "trả giá")
            && (!isExplicitInventorySearch || ContainsAny(lower, "phiên", "auc", "bước giá", "trả giá")))
        {
            return (AiIntentType.Auction, AiModelTier.Fast, false, "Live auction query routed to real-time auction tools.");
        }

        if (hasPhone && ContainsAny(lower, "định giá", "thẩm định", "bao nhiêu tiền", "bán được", "giá", "thế nhịp", "âm luật", "ngắt", "bóc tách", "phân khúc"))
        {
            return (AiIntentType.SimValuation, AiModelTier.Reasoning, true, "Specific phone valuation/cadence analysis routed to reasoning tier.");
        }

        if (ContainsAny(lower, "khung giá", "biên độ", "dao động", "thể loại", "phân loại", "hệ số nhân"))
        {
            return (AiIntentType.MarketBenchmark, AiModelTier.Fast, true, "Static market category benchmark eligible for response cache.");
        }

        if (ContainsAny(lower, "escrow", "ký quỹ", "an toàn", "lừa đảo", "thủ tục", "sang tên", "đổi chủ", "điểm giao dịch", "giấy tờ", "phí sàn", "tiền cọc"))
        {
            return (AiIntentType.EscrowAndPolicy, AiModelTier.Fast, true, "Platform escrow/carrier policy query eligible for response cache.");
        }

        if (hasBirthYear || ContainsAny(lower, "phong thủy", "mệnh", "ngũ hành", "can chi", "hợp tuổi", "hợp số", "ý nghĩa số", "49", "53", "đầu số"))
        {
            var isPureKnowledge = !ContainsAny(lower, "tìm sim", "mua sim", "gợi ý sim", "trong kho") && !budgetMin.HasValue && !budgetMax.HasValue;
            return (AiIntentType.FengShuiAndBirthYear, AiModelTier.Reasoning, isPureKnowledge, "Feng shui / RAG knowledge query.");
        }

        if (budgetMin.HasValue || budgetMax.HasValue || ContainsAny(lower, "tìm sim", "mua sim", "gợi ý", "tìm số", "viettel", "vinaphone", "mobifone", "tứ quý", "ngũ quý", "tam hoa", "thần tài", "lộc phát", "triệu", "tỷ"))
        {
            return (AiIntentType.SimSearch, AiModelTier.Reasoning, false, "Inventory search routed with reasoning model and live DB tools.");
        }

        return (AiIntentType.GeneralConsultation, AiModelTier.Fast, false, "General consultation routed to fast model with core tools.");
    }

    private IReadOnlyList<string> BuildCandidateModels(AiModelTier tier)
    {
        var (primary, configuredPool, secondary) = tier == AiModelTier.Reasoning
            ? (_settings.ComplexChatModel, _settings.ReasoningModels, _settings.ChatModel)
            : (_settings.ChatModel, _settings.FastModels, _settings.ComplexChatModel);

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(primary))
        {
            candidates.Add(primary);
        }

        if (configuredPool is { Count: > 0 })
        {
            candidates.AddRange(configuredPool);
        }
        else if (!string.IsNullOrWhiteSpace(secondary))
        {
            candidates.Add(secondary);
        }

        return candidates
            .Select(m => m?.Trim() ?? string.Empty)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ContainsAny(string source, params string[] keywords)
    {
        foreach (var kw in keywords)
        {
            if (source.Contains(kw, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
