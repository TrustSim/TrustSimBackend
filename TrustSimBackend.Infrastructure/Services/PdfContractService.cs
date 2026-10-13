using System.Globalization;
using System.Text;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Dịch vụ sinh template hợp đồng chuyển nhượng SIM 3 bên (HTML preview và xuất file PDF).
/// </summary>
public class PdfContractService : IPdfContractService
{
    private readonly IUnitOfWork _unitOfWork;

    public PdfContractService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<string> GetContractPreviewHtmlAsync(Guid tradeId, Guid currentUserId, CancellationToken ct = default)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(tradeId, ct);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch với ID '{tradeId}'.");
        }

        var seller = trade.Seller;
        var buyer = trade.Buyer;
        var sim = trade.SimNumber;
        var escrow = trade.Escrow;
        var contract = trade.TransferContract;

        var priceStr = escrow != null ? escrow.EscrowAmount.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ" : "0 VNĐ";
        var phoneStr = sim?.PhoneNumber ?? "Chưa xác định";
        var carrierStr = sim?.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông Việt Nam";

        var buyerSigned = contract?.DigitalSignatures.Any(s => s.SignatureRole == SignatureRole.BUYER) ?? false;
        var sellerSigned = contract?.DigitalSignatures.Any(s => s.SignatureRole == SignatureRole.SELLER) ?? false;

        var sb = new StringBuilder();
        sb.Append("<div class='contract-preview' style='font-family: Arial, sans-serif; line-height: 1.6; color: #222; max-width: 800px; margin: 0 auto; padding: 25px; background: #fff; border-radius: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.08);'>");
        sb.Append("<div style='text-align: center; margin-bottom: 25px;'>");
        sb.Append("<h3 style='margin: 0; text-transform: uppercase;'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</h3>");
        sb.Append("<p style='margin: 5px 0; font-weight: bold;'>Độc lập - Tự do - Hạnh phúc</p>");
        sb.Append("<p style='margin: 0;'>---------------------------------</p>");
        sb.Append("<h2 style='margin-top: 20px; color: #1e3a8a; text-transform: uppercase;'>HỢP ĐỒNG THỎA THUẬN CHUYỂN NHƯỢNG QUYỀN SỬ DỤNG SỐ THUÊ BAO</h2>");
        sb.Append($"<p style='font-style: italic; color: #64748b;'>Mã hợp đồng: <strong>{trade.TradeTransactionCode}</strong> | Ngày tạo: {trade.CreatedDate:dd/MM/yyyy HH:mm} UTC</p>");
        sb.Append("</div>");

        sb.Append("<h4 style='color: #0f172a; border-bottom: 2px solid #e2e8f0; padding-bottom: 6px;'>I. CÁC BÊN THAM GIA GIAO KẾT</h4>");
        
        // Bên A: Người bán
        sb.Append("<p><strong>BÊN A (BÊN BÁN / CHỦ THUÊ BAO):</strong></p>");
        sb.Append("<ul>");
        sb.Append($"<li>Họ và tên: <strong>{seller?.UserProfile?.FullName ?? seller?.UserName ?? "Chưa cập nhật"}</strong></li>");
        sb.Append($"<li>Mã định danh (CCCD): <strong>{MaskIdentity(seller?.UserProfile?.IdentityNumber)}</strong></li>");
        sb.Append($"<li>Email liên hệ: {seller?.Email ?? "N/A"}</li>");
        sb.Append($"<li>Trạng thái xác thực: {(sellerSigned ? "<span style='color: #16a34a; font-weight: bold;'>✅ ĐÃ KÝ XÁC NHẬN</span>" : "<span style='color: #ea580c; font-weight: bold;'>⏳ CHỜ KÝ XÁC NHẬN</span>")}</li>");
        sb.Append("</ul>");

        // Bên B: Người mua
        sb.Append("<p><strong>BÊN B (BÊN MUA / NGƯỜI TRÚNG ĐẤU GIÁ):</strong></p>");
        sb.Append("<ul>");
        sb.Append($"<li>Họ và tên: <strong>{buyer?.UserProfile?.FullName ?? buyer?.UserName ?? "Chưa cập nhật"}</strong></li>");
        sb.Append($"<li>Mã định danh (CCCD): <strong>{MaskIdentity(buyer?.UserProfile?.IdentityNumber)}</strong></li>");
        sb.Append($"<li>Email liên hệ: {buyer?.Email ?? "N/A"}</li>");
        sb.Append($"<li>Trạng thái xác thực: {(buyerSigned ? "<span style='color: #16a34a; font-weight: bold;'>✅ ĐÃ KÝ XÁC NHẬN</span>" : "<span style='color: #ea580c; font-weight: bold;'>⏳ CHỜ KÝ XÁC NHẬN</span>")}</li>");
        sb.Append("</ul>");

        // Bên C: Sàn
        sb.Append("<p><strong>BÊN C (ĐƠN VỊ TRUNG GIAN LÀM CHỨNG & BẢO ĐẢM TÀI CHÍNH):</strong></p>");
        sb.Append("<ul>");
        sb.Append("<li>Tên đơn vị: <strong>Nền tảng Đấu giá Trực tuyến TrustSim</strong></li>");
        sb.Append("<li>Vai trò: Quản lý Quỹ ký quỹ Escrow, niêm phong chữ ký số PAdES X.509 và lưu vết kiểm toán pháp lý.</li>");
        sb.Append("</ul>");

        sb.Append("<h4 style='color: #0f172a; border-bottom: 2px solid #e2e8f0; padding-bottom: 6px; margin-top: 20px;'>II. NỘI DUNG VÀ ĐIỀU KHOẢN CHUYỂN NHƯỢNG</h4>");
        sb.Append($"<p><strong>Điều 1. Đối tượng giao dịch:</strong> Quyền sử dụng số thuê bao viễn thông <strong>{phoneStr}</strong> thuộc mạng <strong>{carrierStr}</strong>.</p>");
        sb.Append($"<p><strong>Điều 2. Giá trị chuyển nhượng:</strong> Mức giá thắng đấu giá cuối cùng là <strong>{priceStr}</strong>. Toàn bộ số tiền đã được Bên B thanh toán đầy đủ và phong tỏa an toàn trong Quỹ Ký Quỹ Escrow của Bên C.</p>");
        sb.Append("<p><strong>Điều 3. Nghĩa vụ sang tên chính chủ:</strong> Trong thời hạn quy định, Bên A có nghĩa vụ phối hợp với Bên B thực hiện thủ tục đổi chủ thuê bao tại cửa hàng giao dịch của nhà mạng viễn thông.</p>");
        sb.Append("<p><strong>Điều 4. Chế tài vi phạm:</strong> Nếu Bên A từ chối sang tên hoặc vi phạm thời hạn, tiền cọc cam kết bán sẽ bị tịch thu bồi thường 50% cho Bên B và hoàn trả 100% tiền Escrow cho Bên B.</p>");
        sb.Append("<p><strong>Điều 5. Giá trị pháp lý điện tử:</strong> Hợp đồng này được giao kết dưới hình thức thông điệp dữ liệu điện tử, xác thực bằng mã OTP theo ngữ cảnh (Transaction Binding) và được niêm phong bằng Chữ ký số PAdES của Bên C theo Luật Giao dịch điện tử.</p>");

        sb.Append("</div>");
        return sb.ToString();
    }

    public async Task<byte[]> GenerateRawPdfBytesAsync(Guid tradeId, bool isAutoEnforced, CancellationToken ct = default)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(tradeId, ct);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch với ID '{tradeId}'.");
        }

        var seller = trade.Seller;
        var buyer = trade.Buyer;
        var sim = trade.SimNumber;
        var escrow = trade.Escrow;

        var priceStr = escrow != null ? escrow.EscrowAmount.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ" : "0 VNĐ";
        var phoneStr = sim?.PhoneNumber ?? "Chưa xác định";
        var carrierStr = sim?.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông Việt Nam";

        using var memoryStream = new MemoryStream();
        var writer = new PdfWriter(memoryStream);
        var pdf = new PdfDocument(writer);
        var document = new Document(pdf, PageSize.A4);
        document.SetMargins(36, 36, 36, 36);

        // Header Quốc hiệu
        document.Add(new Paragraph("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetBold()
            .SetFontSize(13));
        document.Add(new Paragraph("Độc lập - Tự do - Hạnh phúc\n------------------------")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetBold()
            .SetFontSize(11));

        document.Add(new Paragraph("HỢP ĐỒNG THỎA THUẬN CHUYỂN NHƯỢNG QUYỀN SỬ DỤNG SỐ THUÊ BAO")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetBold()
            .SetFontSize(14)
            .SetFontColor(ColorConstants.BLUE)
            .SetMarginTop(15));

        document.Add(new Paragraph($"Mã hợp đồng: {trade.TradeTransactionCode} | Ngày tạo: {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetItalic()
            .SetFontSize(9)
            .SetFontColor(ColorConstants.GRAY)
            .SetMarginBottom(15));

        // Phần I: Các bên
        document.Add(new Paragraph("I. CÁC BÊN THAM GIA GIAO KẾT").SetBold().SetFontSize(11));
        document.Add(new Paragraph($"1. BÊN BÁN (BÊN A): {seller?.UserProfile?.FullName ?? seller?.UserName} - CCCD: {MaskIdentity(seller?.UserProfile?.IdentityNumber)} - Email: {seller?.Email}"));
        document.Add(new Paragraph($"2. BÊN MUA (BÊN B): {buyer?.UserProfile?.FullName ?? buyer?.UserName} - CCCD: {MaskIdentity(buyer?.UserProfile?.IdentityNumber)} - Email: {buyer?.Email}"));
        document.Add(new Paragraph("3. ĐƠN VỊ LÀM CHỨNG & BẢO ĐẢM (BÊN C): SÀN ĐẤU GIÁ TRỰC TUYẾN TRUSTSIM"));

        // Phần II: Điều khoản
        document.Add(new Paragraph("\nII. ĐIỀU KHOẢN THỎA THUẬN").SetBold().SetFontSize(11));
        document.Add(new Paragraph($"Điều 1. Đối tượng hợp đồng là quyền sử dụng SIM số đẹp {phoneStr} ({carrierStr})."));
        document.Add(new Paragraph($"Điều 2. Giá trị chuyển nhượng là {priceStr}, đã thanh toán 100% vào Quỹ Escrow an toàn của Sàn TrustSim."));
        document.Add(new Paragraph("Điều 3. Bên A cam kết tiến hành thủ tục sang tên đổi chủ chính thức tại nhà mạng viễn thông đúng thời hạn."));
        document.Add(new Paragraph("Điều 4. Nếu Bên A vi phạm không sang tên, toàn bộ tiền cọc cam kết bán sẽ bị tịch thu bồi thường 50% cho Bên B và hoàn 100% Escrow cho Bên B."));
        document.Add(new Paragraph("Điều 5. Hợp đồng được giao kết bằng chữ ký điện tử có giá trị pháp lý ràng buộc tuyệt đối theo Luật Giao dịch điện tử."));

        if (isAutoEnforced)
        {
            document.Add(new Paragraph("\n[GHI CHÚ ĐẶC BIỆT: HỢP ĐỒNG ĐƯỢC NIÊM PHONG QUA CƠ CHẾ ỦY QUYỀN CƯỠNG CHẾ DO BÊN BÁN QUÁ HẠN 24H]")
                .SetFontColor(ColorConstants.RED)
                .SetBold()
                .SetFontSize(10));
        }

        // Bảng hộp dấu đại diện các bên
        document.Add(new Paragraph("\nIII. XÁC NHẬN CÁC BÊN").SetBold().SetFontSize(11).SetMarginTop(20));

        var table = new Table(UnitValue.CreatePercentArray(new float[] { 33, 33, 34 })).UseAllAvailableWidth();
        table.AddHeaderCell(new Cell().Add(new Paragraph("BÊN BÁN (BÊN A)").SetBold().SetTextAlignment(TextAlignment.CENTER)).SetBorder(Border.NO_BORDER));
        table.AddHeaderCell(new Cell().Add(new Paragraph("BÊN MUA (BÊN B)").SetBold().SetTextAlignment(TextAlignment.CENTER)).SetBorder(Border.NO_BORDER));
        table.AddHeaderCell(new Cell().Add(new Paragraph("SÀN TRUSTSIM (BÊN C)").SetBold().SetTextAlignment(TextAlignment.CENTER)).SetBorder(Border.NO_BORDER));

        table.AddCell(new Cell().Add(new Paragraph(isAutoEnforced ? "(Sàn đại diện ủy quyền)" : "(Đã xác thực OTP)").SetItalic().SetTextAlignment(TextAlignment.CENTER)).SetBorder(Border.NO_BORDER));
        table.AddCell(new Cell().Add(new Paragraph("(Đã xác thực OTP)").SetItalic().SetTextAlignment(TextAlignment.CENTER)).SetBorder(Border.NO_BORDER));
        table.AddCell(new Cell().Add(new Paragraph("(Ký số PAdES niêm phong)").SetItalic().SetTextAlignment(TextAlignment.CENTER)).SetBorder(Border.NO_BORDER));

        document.Add(table);
        document.Close();

        return memoryStream.ToArray();
    }

    public async Task<IReadOnlyList<SignatureStampBoxDto>> GetStampBoxesAsync(Guid tradeId, bool isAutoEnforced, CancellationToken ct = default)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(tradeId, ct);
        if (trade == null) return Array.Empty<SignatureStampBoxDto>();

        var list = new List<SignatureStampBoxDto>();
        var signatures = trade.TransferContract?.DigitalSignatures?.ToList() ?? new List<DigitalSignature>();

        var buyerSig = signatures.FirstOrDefault(s => s.SignatureRole == SignatureRole.BUYER);
        if (buyerSig != null)
        {
            list.Add(new SignatureStampBoxDto(
                "BÊN MUA (BUYER)",
                trade.Buyer?.UserProfile?.FullName ?? trade.Buyer?.UserName ?? "Người Mua",
                MaskIdentity(trade.Buyer?.UserProfile?.IdentityNumber),
                buyerSig.OtpVerifiedAt,
                buyerSig.IpAddress,
                buyerSig.AuthMethod,
                false));
        }

        var sellerSig = signatures.FirstOrDefault(s => s.SignatureRole == SignatureRole.SELLER);
        if (sellerSig != null)
        {
            list.Add(new SignatureStampBoxDto(
                "BÊN BÁN (SELLER)",
                trade.Seller?.UserProfile?.FullName ?? trade.Seller?.UserName ?? "Người Bán",
                MaskIdentity(trade.Seller?.UserProfile?.IdentityNumber),
                sellerSig.OtpVerifiedAt,
                sellerSig.IpAddress,
                sellerSig.AuthMethod,
                false));
        }
        else if (isAutoEnforced)
        {
            list.Add(new SignatureStampBoxDto(
                "BÊN BÁN (SELLER)",
                trade.Seller?.UserProfile?.FullName ?? trade.Seller?.UserName ?? "Người Bán",
                MaskIdentity(trade.Seller?.UserProfile?.IdentityNumber),
                DateTime.UtcNow,
                "127.0.0.1 (System)",
                "POWER_OF_ATTORNEY_FORCED",
                true));
        }

        return list;
    }

    private static string MaskIdentity(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length < 6) return "******";
        return id.Substring(0, 3) + "******" + id.Substring(id.Length - 3);
    }
}
