using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services.Ai;

/// <summary>
/// Deterministic local agent fallback executed through <see cref="IMcpToolServer"/>
/// when external Gemini API is unavailable or unconfigured, preserving AuthZ and Guardrail policies.
/// </summary>
internal sealed class SmartLocalConsultationFallback
{
    private readonly IMcpToolServer _mcpServer;

    public SmartLocalConsultationFallback(IMcpToolServer mcpServer)
    {
        _mcpServer = mcpServer;
    }

    public sealed class FallbackExecutionResult
    {
        public List<ChatConsultationStreamEvent> ToolEvents { get; set; } = new();
        public List<ConsultationSimSuggestion> CollectedSims { get; set; } = new();
        public List<Guid> ReferencedChunkIds { get; set; } = new();
        public List<string> ReferencedDocuments { get; set; } = new();
        public List<string> ToolsInvoked { get; set; } = new();
        public bool PermissionDenied { get; set; }
        public string? DeniedToolName { get; set; }
        public string ReplyContent { get; set; } = string.Empty;
    }

    public async Task<FallbackExecutionResult> ExecuteAsync(
        string userMessage,
        int? userBirthYear,
        decimal? budgetMin,
        decimal? budgetMax,
        AiUserContext userContext,
        CancellationToken cancellationToken)
    {
        var result = new FallbackExecutionResult();
        var lower = userMessage.ToLowerInvariant();

        var phoneMatch = Regex.Match(userMessage, @"(?:\+84|0)[35789]\d{8}|0\d{9}");
        string? extractedPhone = phoneMatch.Success ? phoneMatch.Value : null;

        var yearMatch = Regex.Match(userMessage, @"\b(19[5-9]\d|20[0-2]\d)\b");
        int? extractedYear = userBirthYear ?? (yearMatch.Success && int.TryParse(yearMatch.Value, out var y) ? y : null);

        decimal? extractedPrice = ExtractPriceFromText(userMessage, lower);

        if (!string.IsNullOrEmpty(extractedPhone) && ContainsAny(lower, "định giá", "giá", "bán được", "thẩm định", "bao nhiêu tiền", "phân khúc"))
        {
            await HandleSimValuationAsync(extractedPhone, userContext, result, cancellationToken);
            return result;
        }

        if (ContainsAny(lower, "khung giá", "biên độ", "dao động") ||
            (lower.Contains("thị trường") && ContainsAny(lower, "tứ quý", "ngũ quý", "tam hoa", "thần tài", "lộc phát")))
        {
            await HandleMarketComparablesAsync(lower, userContext, result, cancellationToken);
            return result;
        }

        if (ContainsAny(lower, "thể loại", "phân loại", "hệ số nhân", "các dòng sim"))
        {
            await HandleSimCategoriesAsync(userContext, result, cancellationToken);
            return result;
        }

        if (!string.IsNullOrEmpty(extractedPhone) && ContainsAny(lower, "thế nhịp", "âm luật", "ngắt", "đọc", "bóc tách"))
        {
            await HandleSimCadenceAsync(extractedPhone, userContext, result, cancellationToken);
            return result;
        }

        if (extractedYear.HasValue && ContainsAny(lower, "mệnh", "hợp số", "năm sinh", "sinh năm", "phong thủy", "tuổi"))
        {
            await HandleFengShuiByBirthYearAsync(extractedYear.Value, budgetMin, budgetMax, userContext, result, cancellationToken);
            return result;
        }

        if (ContainsAny(lower, "thủ tục", "sang tên", "đổi chủ", "điểm giao dịch", "giấy tờ"))
        {
            await HandleCarrierProcedureAsync(lower, userContext, result, cancellationToken);
            return result;
        }

        if (ContainsAny(lower, "đầu số", "đầu 098", "đầu 090", "đầu 091", "đầu 088", "đầu 086"))
        {
            await HandlePrefixInfoAsync(lower, userContext, result, cancellationToken);
            return result;
        }

        if (ContainsAny(lower, "đấu giá", "phiên", "auction", "auc"))
        {
            await HandleAuctionAsync(userMessage, userContext, result, cancellationToken);
            return result;
        }

        if (ContainsAny(lower, "ký quỹ", "escrow", "an toàn", "lừa đảo", "phí sàn", "đặt cọc", "bảo đảm"))
        {
            await HandleEscrowAndFeesAsync(extractedPrice, userContext, result, cancellationToken);
            return result;
        }

        if (ContainsAny(lower, "số dư", "nạp tiền", "ví", "sepay", "vietqr", "chuyển khoản"))
        {
            await HandleWalletAndDepositAsync(extractedPrice, userContext, result, cancellationToken);
            return result;
        }

        await HandleDefaultSearchAsync(budgetMin, budgetMax, userContext, result, cancellationToken);
        return result;
    }

    private async Task<McpToolExecutionResult> InvokeToolAsync(
        string toolName,
        object argsObject,
        AiUserContext userContext,
        FallbackExecutionResult result,
        CancellationToken ct)
    {
        result.ToolEvents.Add(new ChatConsultationStreamEvent
        {
            EventType = "tool_start",
            ToolName = toolName,
            ToolSummary = $"Đang kích hoạt công cụ {toolName}..."
        });

        var argsElement = JsonSerializer.SerializeToElement(argsObject);
        var exec = await _mcpServer.CallToolAsync(toolName, argsElement, userContext, ct);

        result.ToolsInvoked.Add(toolName);
        if (exec.PermissionDenied)
        {
            result.PermissionDenied = true;
            result.DeniedToolName = toolName;
        }
        if (exec.SuggestedSims.Count > 0)
        {
            result.CollectedSims.AddRange(exec.SuggestedSims);
        }
        if (exec.ReferencedChunkIds.Count > 0)
        {
            result.ReferencedChunkIds.AddRange(exec.ReferencedChunkIds);
        }
        if (exec.ReferencedDocuments.Count > 0)
        {
            result.ReferencedDocuments.AddRange(exec.ReferencedDocuments);
        }

        result.ToolEvents.Add(new ChatConsultationStreamEvent
        {
            EventType = "tool_end",
            ToolName = toolName,
            ToolSummary = exec.Summary
        });

        return exec;
    }

    private async Task HandleSimValuationAsync(string phone, AiUserContext ctx, FallbackExecutionResult result, CancellationToken ct)
    {
        var valExec = await InvokeToolAsync("EvaluateSimPrice", new { phoneNumber = phone }, ctx, result, ct);
        await InvokeToolAsync("AnalyzeSimCadence", new { phoneNumber = phone }, ctx, result, ct);

        using var vDoc = JsonDocument.Parse(valExec.ContentJson);
        var vRoot = vDoc.RootElement;
        string formatted = GetStr(vRoot, "formattedNumber") ?? phone;
        string carrier = GetStr(vRoot, "carrier") ?? "Viễn thông";
        string prefix = GetStr(vRoot, "prefix") ?? "";
        int points = vRoot.TryGetProperty("totalPoints", out var pt) && pt.ValueKind == JsonValueKind.Number ? pt.GetInt32() : 5;

        string rhythmPattern = "3.3.4";
        string rhythmName = "Số đẹp";
        if (vRoot.TryGetProperty("rhythm", out var rh) && rh.ValueKind == JsonValueKind.Object)
        {
            rhythmPattern = GetStr(rh, "pattern") ?? rhythmPattern;
            rhythmName = GetStr(rh, "name") ?? rhythmName;
        }

        decimal retailP = 0, p2pP = 0, dealerP = 0;
        if (vRoot.TryGetProperty("priceEstimates", out var pe) && pe.ValueKind == JsonValueKind.Object)
        {
            retailP = GetDec(pe, "retailPrice");
            p2pP = GetDec(pe, "marketP2PPrice");
            dealerP = GetDec(pe, "dealerBuybackPrice");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Chào bạn! Chuyên gia Thẩm định TrustSim xin gửi tới bạn kết quả thẩm định giá trị chi tiết cho số **{formatted}**:\n");
        sb.AppendLine("### 1. Phân Tích Cấu Trúc & Thế Số");
        sb.AppendLine($"- **Nhà mạng & Đầu số:** {carrier} (Đầu số {prefix}).");
        sb.AppendLine($"- **Thế nhịp ngắt tối ưu:** **{rhythmPattern}** (`{formatted}`) - {rhythmName}.");
        sb.AppendLine($"- **Tổng nút (Điểm số phong thủy):** **{points}/10 nút**.");

        if (vRoot.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array && cats.GetArrayLength() > 0)
        {
            var catNames = cats.EnumerateArray().Select(c => GetStr(c, "name")).Where(s => !string.IsNullOrEmpty(s));
            sb.AppendLine($"- **Các thế số & tổ hợp tài lộc:** {string.Join(", ", catNames)}.");
        }

        sb.AppendLine("\n### 2. Bảng Định Giá 3 Kênh Thị Trường");
        sb.AppendLine($"- **Giá Niêm Yết Showroom / Đại Lý Lớn:** ~**{retailP:N0} VNĐ**");
        sb.AppendLine($"- **Giá Chợ Tự Do P2P (Sang tay trực tiếp):** ~**{p2pP:N0} VNĐ**");
        sb.AppendLine($"- **Giá Thợ Thu Mua 24h Tiền Mặt:** ~**{dealerP:N0} VNĐ**");

        sb.AppendLine("\n### 3. Đánh Giá Thanh Khoản & Khuyến Nghị");
        sb.AppendLine(p2pP > 500_000_000
            ? "Số SIM thuộc **Phân khúc Siêu VIP Cực Phẩm**, thanh khoản cao trong giới doanh nhân. Bạn có thể mở phiên đấu giá trên TrustSim để tối đa hóa lợi nhuận."
            : p2pP > 20_000_000
                ? "Số SIM thuộc **Phân khúc VIP Doanh nhân / Hotline**, nhu cầu thị trường ổn định, chuyển nhượng an toàn qua TrustSim Escrow."
                : "Số SIM thuộc **Phân khúc Phổ thông / Số đẹp Tầm trung**, phù hợp làm số liên lạc cá nhân hoặc kinh doanh.");

        result.ReplyContent = sb.ToString();
    }

    private async Task HandleMarketComparablesAsync(string lower, AiUserContext ctx, FallbackExecutionResult result, CancellationToken ct)
    {
        string catCode = lower.Contains("ngũ quý") ? "NGU_QUY" : lower.Contains("tam hoa") ? "TAM_HOA" : lower.Contains("thần tài") ? "THAN_TAI" : lower.Contains("lộc phát") ? "LOC_PHAT" : "TU_QUY";
        string pref = lower.Contains("090") ? "090" : lower.Contains("091") ? "091" : "098";

        var exec = await InvokeToolAsync("GetMarketPriceComparables", new { categoryCode = catCode, prefix = pref }, ctx, result, ct);
        using var cDoc = JsonDocument.Parse(exec.ContentJson);
        var cRoot = cDoc.RootElement;

        string catName = GetStr(cRoot, "categoryName") ?? "SIM Số Đẹp";
        string retailRange = "150tr - 500tr VNĐ";
        string p2pRange = "120tr - 380tr VNĐ";
        string dealerRange = "80tr - 250tr VNĐ";
        if (cRoot.TryGetProperty("priceRanges", out var pr) && pr.ValueKind == JsonValueKind.Object)
        {
            retailRange = GetStr(pr, "retailShowroom") ?? retailRange;
            p2pRange = GetStr(pr, "marketP2P") ?? p2pRange;
            dealerRange = GetStr(pr, "dealerBuyback24h") ?? dealerRange;
        }
        string liquidity = GetStr(cRoot, "liquidityLevel") ?? "Thanh khoản cao";

        var sb = new StringBuilder();
        sb.AppendLine($"Chào bạn! Dưới đây là khung giá thị trường tham chiếu cho dòng **{catName} (Đầu số {pref})**:\n");
        sb.AppendLine("### 1. Khung Giá Giao Dịch Thực Tế 3 Kênh:");
        sb.AppendLine($"- **Giá Niêm Yết Showroom / Đại Lý Lớn:** **{retailRange}**");
        sb.AppendLine($"- **Giá Chợ Tự Do P2P (Sang tay Doanh nhân):** **{p2pRange}**");
        sb.AppendLine($"- **Giá Thợ Thu Mua 24h Tiền Mặt:** **{dealerRange}**\n");
        sb.AppendLine("### 2. Đánh Giá Thanh Khoản:");
        sb.AppendLine($"- Mức độ thanh khoản: **{liquidity}**.");

        result.ReplyContent = sb.ToString();
    }

    private async Task HandleSimCategoriesAsync(AiUserContext ctx, FallbackExecutionResult result, CancellationToken ct)
    {
        var exec = await InvokeToolAsync("GetSimCategories", new { }, ctx, result, ct);
        using var doc = JsonDocument.Parse(exec.ContentJson);
        var root = doc.RootElement;

        var sb = new StringBuilder();
        sb.AppendLine("Chào bạn! Sàn Giao dịch TrustSim hiện phân loại các **Thể loại SIM Số đẹp** theo ma trận sau:\n");
        if (root.TryGetProperty("items", out var list) && list.ValueKind == JsonValueKind.Array && list.GetArrayLength() > 0)
        {
            foreach (var item in list.EnumerateArray())
            {
                string name = GetStr(item, "name") ?? "";
                float multi = item.TryGetProperty("multiplier", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetSingle() : 1.0f;
                string desc = GetStr(item, "description") ?? "";
                sb.AppendLine($"• **{name}** (Hệ số: **{multi:F1}x**) : {desc}");
            }
        }
        else
        {
            sb.AppendLine("• **Bát quý / Lục quý / Ngũ quý / Tứ quý / Sảnh tiến / Thần tài / Lộc phát / Tam hoa**.");
        }

        result.ReplyContent = sb.ToString();
    }

    private async Task HandleSimCadenceAsync(string phone, AiUserContext ctx, FallbackExecutionResult result, CancellationToken ct)
    {
        var exec = await InvokeToolAsync("AnalyzeSimCadence", new { phoneNumber = phone }, ctx, result, ct);
        using var doc = JsonDocument.Parse(exec.ContentJson);
        var root = doc.RootElement;

        string bestFmt = GetStr(root, "formatted") ?? phone;
        string bestRhythm = GetStr(root, "rhythm") ?? "Số đẹp";
        string comp = GetStr(root, "comparison") ?? "";

        var sb = new StringBuilder();
        sb.AppendLine($"Chào bạn! Phân tích **Thế nhịp ngắt & Âm luật** cho số **{phone}**:\n");
        sb.AppendLine($"- **Định dạng tối ưu:** `{bestFmt}` (**{bestRhythm}**)");
        if (!string.IsNullOrWhiteSpace(comp))
        {
            sb.AppendLine($"- **So sánh âm luật:** {comp}");
        }
        result.ReplyContent = sb.ToString();
    }

    private async Task HandleFengShuiByBirthYearAsync(int year, decimal? budgetMin, decimal? budgetMax, AiUserContext ctx, FallbackExecutionResult result, CancellationToken ct)
    {
        var fsExec = await InvokeToolAsync("GetFengShuiGuidelineByBirthYear", new { birthYear = year }, ctx, result, ct);
        await InvokeToolAsync("QueryFengShuiKnowledge", new { query = $"phong thủy năm sinh {year}" }, ctx, result, ct);
        await InvokeToolAsync("SearchAvailableSims", new { minPrice = budgetMin, maxPrice = budgetMax }, ctx, result, ct);

        using var fDoc = JsonDocument.Parse(fsExec.ContentJson);
        var fRoot = fDoc.RootElement;
        string canChi = GetStr(fRoot, "canChi") ?? "";
        string element = GetStr(fRoot, "element") ?? "Kim";
        string advice = GetStr(fRoot, "advice") ?? "";
        var favNums = fRoot.TryGetProperty("favorableNumbers", out var fn) && fn.ValueKind == JsonValueKind.Array
            ? string.Join(", ", fn.EnumerateArray().Select(n => n.GetInt32().ToString()))
            : "6, 8, 9";
        var unfavNums = fRoot.TryGetProperty("unfavorableNumbers", out var un) && un.ValueKind == JsonValueKind.Array
            ? string.Join(", ", un.EnumerateArray().Select(n => n.GetInt32().ToString()))
            : "4, 7";

        var sb = new StringBuilder();
        sb.AppendLine($"Chào bạn! Dưới đây là phân tích phong thủy số học cho gia chủ sinh năm **{year}**:\n");
        sb.AppendLine($"### 1. Bản Mệnh & Ngũ Hành");
        sb.AppendLine($"- **Năm sinh âm lịch:** **{canChi}**");
        sb.AppendLine($"- **Bản mệnh ngũ hành:** **Mệnh {element}**");
        sb.AppendLine($"- **Con số Cát Tường:** **{favNums}**");
        sb.AppendLine($"- **Con số nên tránh:** **{unfavNums}**");
        sb.AppendLine($"- **Lời khuyên:** {advice}\n");

        if (result.CollectedSims.Count > 0)
        {
            sb.AppendLine("### 2. Gợi Ý Số SIM Thực Tế Đang Mở Bán");
            foreach (var s in result.CollectedSims)
            {
                sb.AppendLine($"• **{s.PhoneNumber}** ({s.CarrierName}) — Giá: **{s.Price:N0} đ** : {s.HighlightReason}");
            }
        }

        result.ReplyContent = sb.ToString();
    }

    private async Task HandleCarrierProcedureAsync(string lower, AiUserContext ctx, FallbackExecutionResult result, CancellationToken ct)
    {
        string carrierName = lower.Contains("vina") ? "VinaPhone" : lower.Contains("mobi") ? "MobiFone" : "Viettel";
        await InvokeToolAsync("GetCarrierInfo", new { carrierName }, ctx, result, ct);
        await InvokeToolAsync("GetEscrowAndSafetyPolicy", new { }, ctx, result, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"Chào bạn! Hướng dẫn thủ tục sang tên đổi chủ mạng **{carrierName}** trên sàn TrustSim:\n");
        sb.AppendLine($"- **Địa điểm:** Bất kỳ điểm giao dịch chính thức của **{carrierName}** trên toàn quốc (15–30 phút).");
        sb.AppendLine("- **Giấy tờ:** Bên bán mang CCCD/CMND gốc + SIM gốc; Bên mua mang CCCD gắn chip gốc.");
        sb.AppendLine("- **Bảo vệ TrustSim Escrow:** Tiền được giữ an toàn trong ví ký quỹ và chỉ giải ngân sau khi sang tên chính chủ thành công.");
        result.ReplyContent = sb.ToString();
    }

    private async Task HandlePrefixInfoAsync(string lower, AiUserContext ctx, FallbackExecutionResult result, CancellationToken ct)
    {
        string prefix = lower.Contains("090") ? "090" : lower.Contains("091") ? "091" : lower.Contains("088") ? "088" : lower.Contains("086") ? "086" : "098";
        var exec = await InvokeToolAsync("GetPrefixInfo", new { prefixValue = prefix }, ctx, result, ct);
        using var pDoc = JsonDocument.Parse(exec.ContentJson);
        var pRoot = pDoc.RootElement;

        string carName = GetStr(pRoot, "carrierName") ?? "Viễn thông";
        float repScore = pRoot.TryGetProperty("reputationScore", out var rs) && rs.ValueKind == JsonValueKind.Number ? rs.GetSingle() : 1.5f;
        string category = GetStr(pRoot, "category") ?? "Đầu số cổ";
        string fengShui = GetStr(pRoot, "fengShuiMeaning") ?? "";

        var sb = new StringBuilder();
        sb.AppendLine($"Chào bạn! Thông tin chi tiết về **Đầu số {prefix}**:\n");
        sb.AppendLine($"- **Nhà mạng:** **{carName}**");
        sb.AppendLine($"- **Phân loại:** **{category}**");
        sb.AppendLine($"- **Điểm uy tín:** **{repScore:F2}**");
        sb.AppendLine($"- **Ý nghĩa phong thủy:** {fengShui}");
        result.ReplyContent = sb.ToString();
    }

    private async Task HandleAuctionAsync(string userMessage, AiUserContext ctx, FallbackExecutionResult result, CancellationToken ct)
    {
        var aucMatch = Regex.Match(userMessage, @"(?i)\b(AUC[0-9A-Z]+)\b");
        if (aucMatch.Success)
        {
            string aucCode = aucMatch.Value.ToUpperInvariant();
            var detailExec = await InvokeToolAsync("GetAuctionDetail", new { auctionCode = aucCode }, ctx, result, ct);
            using var dDoc = JsonDocument.Parse(detailExec.ContentJson);
            var dRoot = dDoc.RootElement;
            if (!dRoot.TryGetProperty("error", out _))
            {
                string phone = GetStr(dRoot, "phoneNumber") ?? "";
                decimal curP = GetDec(dRoot, "currentPrice");
                decimal stepP = GetDec(dRoot, "stepPrice");
                decimal depP = GetDec(dRoot, "depositAmount");
                int bids = dRoot.TryGetProperty("totalBids", out var tb) && tb.ValueKind == JsonValueKind.Number ? tb.GetInt32() : 0;
                string endTime = GetStr(dRoot, "endAt") ?? "";

                var dSb = new StringBuilder();
                dSb.AppendLine($"Chào bạn! Chi tiết **Phiên Đấu Giá {aucCode}** trên TrustSim:\n");
                dSb.AppendLine($"- **Số SIM:** **{phone}**");
                dSb.AppendLine($"- **Giá hiện tại:** **{curP:N0} VNĐ** ({bids} lượt trả giá)");
                dSb.AppendLine($"- **Bước giá tối thiểu:** **{stepP:N0} VNĐ**");
                dSb.AppendLine($"- **Tiền đặt cọc:** **{depP:N0} VNĐ**");
                dSb.AppendLine($"- **Kết thúc:** **{endTime}**");
                result.ReplyContent = dSb.ToString();
                return;
            }
        }

        var listExec = await InvokeToolAsync("GetActiveAuctions", new { status = (string?)null }, ctx, result, ct);
        using var aDoc = JsonDocument.Parse(listExec.ContentJson);
        var aRoot = aDoc.RootElement;
        int total = aRoot.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : 0;

        var sb = new StringBuilder();
        sb.AppendLine("Chào bạn! Thông tin các phiên đấu giá SIM VIP trên sàn TrustSim:\n");
        if (total > 0 && aRoot.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0)
        {
            foreach (var item in items.EnumerateArray())
            {
                string code = GetStr(item, "auctionCode") ?? "";
                string phone = GetStr(item, "phoneNumber") ?? "";
                decimal curPrice = GetDec(item, "currentPrice");
                decimal step = GetDec(item, "stepPrice");
                string status = GetStr(item, "status") ?? "";
                string end = GetStr(item, "endTime") ?? "";
                sb.AppendLine($"• **Mã: {code} ({status})** — Số SIM: **{phone}** — Giá hiện tại: **{curPrice:N0} đ** (Bước giá: {step:N0} đ) — Kết thúc: {end}");
            }
        }
        else
        {
            sb.AppendLine("Hiện tại chưa có phiên đấu giá nào đang mở. Bạn có thể theo dõi sảnh Đấu Giá để cập nhật lịch mới nhất.");
        }
        result.ReplyContent = sb.ToString();
    }

    private async Task HandleEscrowAndFeesAsync(decimal? extractedPrice, AiUserContext ctx, FallbackExecutionResult result, CancellationToken ct)
    {
        decimal priceToCalc = extractedPrice ?? 200_000_000m;
        await InvokeToolAsync("GetEscrowAndSafetyPolicy", new { }, ctx, result, ct);
        var feeExec = await InvokeToolAsync("CalculateTradeFees", new { price = priceToCalc }, ctx, result, ct);

        using var feeDoc = JsonDocument.Parse(feeExec.ContentJson);
        var feeRoot = feeDoc.RootElement;
        string feeRate = GetStr(feeRoot, "feeRatePercentage") ?? "3%";
        decimal feeAmount = GetDec(feeRoot, "feeAmount");
        decimal depRequired = GetDec(feeRoot, "depositRequired");
        decimal netSeller = GetDec(feeRoot, "netSellerReceives");

        var sb = new StringBuilder();
        sb.AppendLine("Chào bạn! Thông tin về **Chính sách Ký quỹ TrustSim Escrow & Biểu phí sàn**:\n");
        sb.AppendLine($"- **Mức giá tham chiếu:** {priceToCalc:N0} VNĐ");
        sb.AppendLine($"- **Tỷ lệ phí sàn:** **{feeRate}** ({feeAmount:N0} VNĐ)");
        sb.AppendLine($"- **Tiền đặt cọc giữ chân (10%):** **{depRequired:N0} VNĐ**");
        sb.AppendLine($"- **Người bán thực nhận:** **{netSeller:N0} VNĐ**\n");
        sb.AppendLine("Tiền giao dịch được giữ độc lập tại tài khoản ký quỹ và chỉ giải ngân khi hoàn tất sang tên chính chủ tại nhà mạng; hoàn 100% nếu có tranh chấp.");
        result.ReplyContent = sb.ToString();
    }

    private async Task HandleWalletAndDepositAsync(decimal? extractedPrice, AiUserContext ctx, FallbackExecutionResult result, CancellationToken ct)
    {
        decimal depositAmount = extractedPrice ?? 50_000_000m;
        var balExec = await InvokeToolAsync("GetMyWalletBalance", new { }, ctx, result, ct);
        if (balExec.PermissionDenied)
        {
            result.ReplyContent = "Chào bạn! Để bảo mật thông tin tài khoản, chức năng kiểm tra số dư ví và tạo cú pháp nạp tiền cá nhân qua SePay VietQR chỉ hỗ trợ cho **thành viên đã đăng nhập**. Vui lòng đăng nhập tài khoản TrustSim của bạn rồi thử lại nhé!";
            return;
        }

        var depExec = await InvokeToolAsync("GetSePayDepositGuideline", new { amount = depositAmount }, ctx, result, ct);
        if (depExec.PermissionDenied)
        {
            result.ReplyContent = "Vui lòng đăng nhập tài khoản TrustSim để lấy thông tin nạp tiền cá nhân.";
            return;
        }

        using var bDoc = JsonDocument.Parse(balExec.ContentJson);
        var bRoot = bDoc.RootElement;
        decimal avail = GetDec(bRoot, "balance");
        decimal locked = GetDec(bRoot, "lockedBalance");

        using var sDoc = JsonDocument.Parse(depExec.ContentJson);
        var sRoot = sDoc.RootElement;
        string bank = GetStr(sRoot, "bankName") ?? "MBBank";
        string accNum = GetStr(sRoot, "accountNumber") ?? "";
        string accHolder = GetStr(sRoot, "accountHolder") ?? "CONG TY TNHH TRUSTSIM";
        string syntax = GetStr(sRoot, "transferSyntax") ?? "TS NAPTIEN";

        var sb = new StringBuilder();
        sb.AppendLine("Chào bạn! Thông tin ví tài khoản TrustSim và hướng dẫn nạp tiền tự động **SePay VietQR**:\n");
        sb.AppendLine($"- **Số dư khả dụng:** **{avail:N0} VNĐ** (Đang khóa cọc/escrow: **{locked:N0} VNĐ**)");
        sb.AppendLine($"- **Ngân hàng thụ hưởng:** **{bank}** — STK: **{accNum}** ({accHolder})");
        sb.AppendLine($"- **Nội dung chuyển khoản bắt buộc:** `{syntax}` (Cộng tiền tự động sau 5–15 giây).");
        result.ReplyContent = sb.ToString();
    }

    private async Task HandleDefaultSearchAsync(decimal? budgetMin, decimal? budgetMax, AiUserContext ctx, FallbackExecutionResult result, CancellationToken ct)
    {
        await InvokeToolAsync("SearchAvailableSims", new { minPrice = budgetMin, maxPrice = budgetMax }, ctx, result, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Chào bạn! Cảm ơn bạn đã liên hệ với Chuyên gia Tư vấn SIM Số đẹp TrustSim.");
        sb.AppendLine("- Nên ưu tiên đầu số cổ uy tín (098 - Viettel, 090 - MobiFone, 091 - VinaPhone) và đuôi số cát tường (39, 79, 68, 86, 6789).\n");

        if (result.CollectedSims.Count > 0)
        {
            sb.AppendLine("Các số SIM thực tế đang mở bán phù hợp trên sàn TrustSim:");
            foreach (var sim in result.CollectedSims)
            {
                sb.AppendLine($"• **{sim.PhoneNumber}** ({sim.CarrierName}) — Giá: **{sim.Price:N0} đ** : {sim.HighlightReason}");
            }
        }
        else
        {
            sb.AppendLine("Hiện chưa có số SIM phù hợp tiêu chí trong kho tức thời. Bạn có thể cho biết thêm ngân sách hoặc năm sinh để tôi tư vấn chi tiết hơn.");
        }

        result.ReplyContent = sb.ToString();
    }

    private static decimal? ExtractPriceFromText(string userMessage, string lower)
    {
        var priceMatch = Regex.Match(userMessage, @"(\d+)\s*(?:triệu|tr|trieu|tỷ|ty|vnd|đ|k)", RegexOptions.IgnoreCase);
        if (!priceMatch.Success || !decimal.TryParse(priceMatch.Groups[1].Value, out var pVal))
        {
            return null;
        }
        return lower.Contains("tỷ") || lower.Contains("ty") ? pVal * 1_000_000_000m : pVal * 1_000_000m;
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

    private static string? GetStr(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static decimal GetDec(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var v) ? v : 0m;
}
