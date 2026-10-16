using System.Globalization;
using System.Text;
using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using PageSize = iText.Kernel.Geom.PageSize;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Helpers;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

using System.Security.Cryptography;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Dịch vụ sinh template hợp đồng chuyển nhượng SIM 3 bên, biên bản đấu giá và biên bản thanh lý (HTML preview và xuất file PDF).
/// Tuân thủ quy chuẩn pháp lý: Mẫu số 13 Phụ lục 5 Bộ TT&TT, Mẫu TP-DGTS-18 Thông tư 19/2024/TT-BTP và Nghị định 49/2017/NĐ-CP.
/// </summary>
public class PdfContractService : IPdfContractService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICloudStorageService _cloudStorageService;

    public PdfContractService(IUnitOfWork unitOfWork, ICloudStorageService cloudStorageService)
    {
        _unitOfWork = unitOfWork;
        _cloudStorageService = cloudStorageService;
    }

    #region VĂN BẢN 2: HỢP ĐỒNG CHUYỂN NHƯỢNG QUYỀN SỬ DỤNG SỐ THUÊ BAO 3 BÊN (MẪU 13 PHỤ LỤC 5 BỘ TT&TT)

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

        var escrowAmount = escrow?.EscrowAmount ?? 0m;
        var priceStr = escrowAmount.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ";
        var priceWords = VietnameseNumberToWordsHelper.ToVietnameseWords(escrowAmount);

        var phoneStr = sim?.PhoneNumber ?? "Chưa xác định";
        var carrierStr = sim?.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông Việt Nam";

        var buyerSigned = contract?.DigitalSignatures.Any(s => s.SignatureRole == SignatureRole.BUYER) ?? false;
        var sellerSigned = contract?.DigitalSignatures.Any(s => s.SignatureRole == SignatureRole.SELLER) ?? false;
        var isSealed = !string.IsNullOrWhiteSpace(contract?.DocumentUrl);

        var sellerProfile = seller?.UserProfile;
        var buyerProfile = buyer?.UserProfile;

        var sb = new StringBuilder();
        sb.Append("<div class='contract-preview' style='font-family: Arial, sans-serif; line-height: 1.6; color: #1e293b; max-width: 850px; margin: 0 auto; padding: 32px; background: #ffffff; border-radius: 12px; box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.1); border: 1px solid #e2e8f0;'>");

        // Header Quốc hiệu & Tiêu ngữ
        sb.Append("<div style='text-align: center; margin-bottom: 24px; border-bottom: 1px solid #cbd5e1; padding-bottom: 18px;'>");
        sb.Append("<h3 style='margin: 0; font-size: 14px; font-weight: bold; text-transform: uppercase; letter-spacing: 0.5px;'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</h3>");
        sb.Append("<p style='margin: 4px 0; font-size: 13px; font-weight: bold;'>Độc lập - Tự do - Hạnh phúc</p>");
        sb.Append("<p style='margin: 0; font-size: 12px; color: #64748b;'>---------------------------------------------------</p>");
        sb.Append("<h2 style='margin-top: 18px; margin-bottom: 6px; color: #1e40af; font-size: 18px; text-transform: uppercase; font-weight: 800; line-height: 1.3;'>HỢP ĐỒNG THỎA THUẬN CHUYỂN NHƯỢNG QUYỀN SỬ DỤNG SỐ THUÊ BAO</h2>");
        sb.Append("<p style='margin: 2px 0; font-size: 12px; color: #64748b; font-style: italic;'>(Theo Mẫu số 13 Phụ lục 5 Bộ Thông tin và Truyền thông & Nghị định số 49/2017/NĐ-CP)</p>");
        sb.Append($"<p style='margin-top: 8px; font-size: 13px; color: #334155;'>Số HĐ: <strong>HĐ-CN/{trade.TradeTransactionCode}</strong> | Ngày khởi tạo: {trade.CreatedDate:dd/MM/yyyy HH:mm} (UTC)</p>");
        sb.Append("</div>");

        // Căn cứ pháp lý
        sb.Append("<div style='font-size: 12.5px; color: #475569; font-style: italic; margin-bottom: 20px; line-height: 1.5; background: #f8fafc; padding: 12px 16px; border-radius: 6px; border-left: 3px solid #3b82f6;'>");
        sb.Append("<p style='margin: 2px 0;'>- Căn cứ Bộ luật Dân sự số 91/2015/QH13 ngày 24/11/2015;</p>");
        sb.Append("<p style='margin: 2px 0;'>- Căn cứ Luật Viễn thông số 24/2023/QH15 ngày 24/11/2023;</p>");
        sb.Append("<p style='margin: 2px 0;'>- Căn cứ Luật Giao dịch điện tử số 20/2023/QH15 ngày 22/06/2023;</p>");
        sb.Append("<p style='margin: 2px 0;'>- Căn cứ Nghị định số 49/2017/NĐ-CP về sửa đổi, bổ sung Điều 15 Nghị định 25/2011/NĐ-CP về quản lý thuê bao viễn thông di động;</p>");
        sb.Append("<p style='margin: 2px 0;'>- Căn cứ Quy chế Hoạt động & Quản lý giao dịch Sàn Đấu giá Trực tuyến TrustSim.</p>");
        sb.Append("</div>");

        // Phần I: Các bên
        sb.Append("<h4 style='color: #0f172a; border-bottom: 2px solid #3b82f6; padding-bottom: 6px; font-size: 14px; text-transform: uppercase;'>I. CÁC BÊN THAM GIA GIAO KẾT</h4>");

        // Bên A (Bên Bán)
        sb.Append("<div style='margin-bottom: 14px; padding: 12px; background: #f1f5f9; border-radius: 6px;'>");
        sb.Append("<p style='margin: 0 0 6px 0; font-weight: bold; color: #1e3a8a;'>BÊN CHUYỂN QUYỀN (BÊN BÁN / BÊN A):</p>");
        sb.Append("<table style='width: 100%; font-size: 13px; line-height: 1.6;'>");
        sb.Append($"<tr><td style='width: 30%; color: #64748b;'>Họ và tên:</td><td style='font-weight: bold;'>{sellerProfile?.FullName ?? seller?.UserName ?? "Chưa cập nhật"}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Số CCCD/Định danh:</td><td style='font-weight: bold;'>{FormatIdentity(sellerProfile?.IdentityNumber)}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Ngày sinh:</td><td>{(sellerProfile?.DateOfBirth.HasValue == true ? sellerProfile.DateOfBirth.Value.ToString("dd/MM/yyyy") : "Chưa cập nhật")}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Địa chỉ thường trú:</td><td>{(string.IsNullOrWhiteSpace(sellerProfile?.Address) ? "Theo hồ sơ eKYC hệ thống" : sellerProfile.Address)}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Số điện thoại / Email:</td><td>{(string.IsNullOrWhiteSpace(sellerProfile?.PhoneNumber) ? seller?.PhoneNumber ?? "N/A" : sellerProfile.PhoneNumber)} | {seller?.Email ?? "N/A"}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Trạng thái ký số:</td><td>{(sellerSigned ? "<span style='color: #16a34a; font-weight: bold;'>✅ ĐÃ KÝ XÁC THỰC OTP</span>" : "<span style='color: #ea580c; font-weight: bold;'>⏳ CHỜ KÝ XÁC NHẬN</span>")}</td></tr>");
        sb.Append("</table>");
        sb.Append("</div>");

        // Bên B (Bên Mua)
        sb.Append("<div style='margin-bottom: 14px; padding: 12px; background: #f1f5f9; border-radius: 6px;'>");
        sb.Append("<p style='margin: 0 0 6px 0; font-weight: bold; color: #1e3a8a;'>BÊN NHẬN CHUYỂN QUYỀN (BÊN MUA / BÊN B):</p>");
        sb.Append("<table style='width: 100%; font-size: 13px; line-height: 1.6;'>");
        sb.Append($"<tr><td style='width: 30%; color: #64748b;'>Họ và tên:</td><td style='font-weight: bold;'>{buyerProfile?.FullName ?? buyer?.UserName ?? "Chưa cập nhật"}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Số CCCD/Định danh:</td><td style='font-weight: bold;'>{FormatIdentity(buyerProfile?.IdentityNumber)}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Ngày sinh:</td><td>{(buyerProfile?.DateOfBirth.HasValue == true ? buyerProfile.DateOfBirth.Value.ToString("dd/MM/yyyy") : "Chưa cập nhật")}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Địa chỉ thường trú:</td><td>{(string.IsNullOrWhiteSpace(buyerProfile?.Address) ? "Theo hồ sơ eKYC hệ thống" : buyerProfile.Address)}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Số điện thoại / Email:</td><td>{(string.IsNullOrWhiteSpace(buyerProfile?.PhoneNumber) ? buyer?.PhoneNumber ?? "N/A" : buyerProfile.PhoneNumber)} | {buyer?.Email ?? "N/A"}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Trạng thái ký số:</td><td>{(buyerSigned ? "<span style='color: #16a34a; font-weight: bold;'>✅ ĐÃ KÝ XÁC THỰC OTP</span>" : "<span style='color: #ea580c; font-weight: bold;'>⏳ CHỜ KÝ XÁC NHẬN</span>")}</td></tr>");
        sb.Append("</table>");
        sb.Append("</div>");

        // Bên C (Sàn TrustSim)
        sb.Append("<div style='margin-bottom: 16px; padding: 12px; background: #f8fafc; border-radius: 6px; border: 1px dashed #94a3b8;'>");
        sb.Append("<p style='margin: 0 0 6px 0; font-weight: bold; color: #0f172a;'>BÊN TRUNG GIAN TÍN THÁC & GIÁM SÁT (BÊN C - SÀN TRUSTSIM):</p>");
        sb.Append("<table style='width: 100%; font-size: 13px; line-height: 1.6;'>");
        sb.Append("<tr><td style='width: 30%; color: #64748b;'>Tên tổ chức:</td><td style='font-weight: bold;'>CÔNG TY CỔ PHẦN CÔNG NGHỆ & ĐẤU GIÁ TRỰC TUYẾN TRUSTSIM</td></tr>");
        sb.Append("<tr><td style='color: #64748b;'>Đại diện:</td><td>Ban Giám Đốc Điều Hành Nền Tảng TrustSim</td></tr>");
        sb.Append("<tr><td style='color: #64748b;'>Địa chỉ trụ sở:</td><td>Tòa nhà Công nghệ TrustSim, TP. Hà Nội, Việt Nam</td></tr>");
        sb.Append("<tr><td style='color: #64748b;'>Vai trò:</td><td>Quản lý tài khoản ký quỹ Escrow bảo đảm tài chính, làm chứng thông điệp dữ liệu điện tử và cấp dấu chứng thư niêm phong PAdES X.509.</td></tr>");
        sb.Append("</table>");
        sb.Append("</div>");

        // Phần II: Nội dung điều khoản
        sb.Append("<h4 style='color: #0f172a; border-bottom: 2px solid #3b82f6; padding-bottom: 6px; font-size: 14px; text-transform: uppercase; margin-top: 24px;'>II. NỘI DUNG VÀ ĐIỀU KHOẢN CHUYỂN NHƯỢNG</h4>");

        sb.Append("<div style='font-size: 13.5px; line-height: 1.7; color: #1e293b; text-align: justify;'>");
        sb.Append($"<p><strong>Điều 1. Đối tượng hợp đồng và tình trạng tài sản:</strong><br/>");
        sb.Append($"1.1. Bên A tự nguyện chuyển nhượng và Bên B đồng ý nhận chuyển quyền sử dụng số thuê bao viễn thông di động: <strong style='font-size: 15px; color: #1e40af;'>{phoneStr}</strong> thuộc mạng viễn thông: <strong>{carrierStr}</strong>.<br/>");
        sb.Append($"1.2. Bên A cam kết là chủ thể hợp pháp duy nhất của số thuê bao {phoneStr}; số thuê bao không bị tranh chấp, khiếu nại, không thuộc diện bị phong tỏa hay kê biên bởi cơ quan có thẩm quyền; không nợ cước dịch vụ viễn thông tại bất kỳ nhà mạng nào.</p>");

        sb.Append($"<p><strong>Điều 2. Giá trị chuyển nhượng và phương thức thanh toán ký quỹ Escrow:</strong><br/>");
        sb.Append($"2.1. Giá chuyển nhượng quyền sử dụng số thuê bao được xác lập thông qua kết quả đấu giá trực tuyến hợp lệ là: <strong>{priceStr}</strong> (Bằng chữ: <em>{priceWords}</em>).<br/>");
        sb.Append("2.2. Toàn bộ 100% số tiền chuyển nhượng nói trên đã được Bên B thanh toán đầy đủ và hiện đang được phong tỏa bảo đảm an toàn tuyệt đối trong Quỹ Ký quỹ Escrow do Bên C vận hành.<br/>");
        sb.Append("2.3. Bên C có nghĩa vụ bảo toàn số tiền này và chỉ thực hiện giải ngân cho Bên A khi thủ tục sang tên đổi chủ đã hoàn tất thành công và được Bên B nghiệm thu hoặc hết 72 giờ đối soát tự động không có khiếu nại.</p>");

        sb.Append("<p><strong>Điều 3. Nghĩa vụ và cam kết về cước phí viễn thông:</strong><br/>");
        sb.Append($"3.1. Bên A có trách nhiệm thanh toán dứt điểm toàn bộ cước phí viễn thông, cước dịch vụ giá trị gia tăng phát sinh liên quan đến số thuê bao {phoneStr} tính đến thời điểm hoàn tất sang tên tại điểm giao dịch nhà mạng.<br/>");
        sb.Append($"3.2. Chấm dứt hiệu lực hợp đồng cung cấp và sử dụng dịch vụ viễn thông đã ký trước đây giữa Bên A và Nhà mạng {carrierStr} kể từ thời điểm Bên B hoàn thành đăng ký thông tin thuê bao mới.<br/>");
        sb.Append("3.3. Toàn bộ các gói cước khuyến mại, chính sách ưu đãi, điểm hội viên tích lũy gắn liền với hợp đồng thuê bao cũ của Bên A sẽ tự động hủy bỏ theo quy định của nhà mạng và không được bảo lưu chuyển giao cho Bên B.</p>");

        sb.Append("<p><strong>Điều 4. Thủ tục sang tên đổi chủ và nghĩa vụ tuân thủ pháp luật:</strong><br/>");
        sb.Append("4.1. Trong thời hạn tối đa <strong>07 ngày làm việc</strong> kể từ thời điểm Hợp đồng này được niêm phong, Bên A có nghĩa vụ cung cấp mã xác nhận ủy quyền hoặc trực tiếp cùng Bên B đến Điểm cung cấp dịch vụ viễn thông được chỉ định để thực hiện thủ tục chuyển quyền sử dụng.<br/>");
        sb.Append("4.2. Bên B có nghĩa vụ xuất trình Căn cước công dân gắn chip hợp lệ và thực hiện chụp ảnh chân dung theo đúng quy định tại <strong>Nghị định số 49/2017/NĐ-CP</strong> để hoàn tất đăng ký thuê bao chính chủ mới.</p>");

        sb.Append("<p><strong>Điều 5. Cơ chế giải ngân và chế tài xử lý vi phạm:</strong><br/>");
        sb.Append("5.1. Khi Bên A cung cấp đầy đủ chứng từ sang tên và Bên B bấm Xác nhận nghiệm thu (hoặc sau 72 giờ không có tranh chấp), Bên C sẽ khấu trừ phí sàn 5% và giải ngân 95% giá trị hợp đồng còn lại cho Bên A.<br/>");
        sb.Append("5.2. Nếu Bên A từ chối sang tên hoặc vi phạm thời hạn quy định: Toàn bộ tiền cọc cam kết bán của Bên A sẽ bị tịch thu (50% bồi thường cho Bên B, 50% nộp phạt vi phạm); đồng thời Bên C sẽ hoàn trả ngay 100% số tiền Escrow lại cho Bên B.<br/>");
        sb.Append("5.3. Nếu Bên B vi phạm không hợp tác sang tên quá thời hạn mà không có lý do chính đáng: Tiền đặt trước đấu giá của Bên B sẽ bị tịch thu theo Quy chế sàn.</p>");

        sb.Append("<p><strong>Điều 6. Giá trị pháp lý và phương thức giao kết điện tử:</strong><br/>");
        sb.Append("6.1. Hợp đồng này được khởi tạo, giao kết và lưu trữ dưới dạng thông điệp dữ liệu điện tử theo quy định của Luật Giao dịch điện tử số 20/2023/QH15, có giá trị pháp lý tương đương văn bản giấy gốc.<br/>");
        sb.Append("6.2. Hợp đồng có hiệu lực kể từ thời điểm cả Bên A và Bên B hoàn tất ký xác thực OTP và được Bên C niêm phong bằng Chữ ký số PAdES X.509 có nhãn thời gian TSA.</p>");
        sb.Append("</div>");

        // Bảng ký xác nhận
        sb.Append("<h4 style='color: #0f172a; border-bottom: 2px solid #3b82f6; padding-bottom: 6px; font-size: 14px; text-transform: uppercase; margin-top: 24px;'>III. XÁC NHẬN CÁC BÊN THAM GIA</h4>");
        sb.Append("<div style='display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 12px; margin-top: 16px; text-align: center;'>");

        // Cột Bên A
        sb.Append("<div style='border: 1px solid #cbd5e1; border-radius: 8px; padding: 12px; background: #fafafa;'>");
        sb.Append("<p style='font-weight: bold; margin: 0 0 6px 0; font-size: 13px;'>BÊN CHUYỂN QUYỀN (BÊN A)</p>");
        sb.Append("<p style='font-size: 12px; color: #64748b; margin: 0 0 10px 0;'>Người có tài sản SIM</p>");
        if (sellerSigned)
        {
            sb.Append("<div style='padding: 8px; background: #dcfce7; border: 1px solid #86efac; border-radius: 6px; color: #166534; font-size: 12px; font-weight: bold;'>");
            sb.Append("✅ ĐÃ KÝ ĐIỆN TỬ<br/><span style='font-size: 10px; font-weight: normal; color: #15803d;'>Xác thực OTP thành công</span>");
            sb.Append("</div>");
        }
        else
        {
            sb.Append("<div style='padding: 8px; background: #ffedd5; border: 1px solid #fed7aa; border-radius: 6px; color: #c2410c; font-size: 12px; font-weight: bold;'>");
            sb.Append("⏳ CHỜ KÝ XÁC NHẬN<br/><span style='font-size: 10px; font-weight: normal; color: #9a3412;'>Đang chờ thao tác OTP</span>");
            sb.Append("</div>");
        }
        sb.Append($"<p style='margin: 8px 0 0 0; font-size: 12px; font-weight: bold;'>{sellerProfile?.FullName ?? seller?.UserName}</p>");
        sb.Append("</div>");

        // Cột Bên B
        sb.Append("<div style='border: 1px solid #cbd5e1; border-radius: 8px; padding: 12px; background: #fafafa;'>");
        sb.Append("<p style='font-weight: bold; margin: 0 0 6px 0; font-size: 13px;'>BÊN NHẬN CHUYỂN QUYỀN (BÊN B)</p>");
        sb.Append("<p style='font-size: 12px; color: #64748b; margin: 0 0 10px 0;'>Người trúng đấu giá</p>");
        if (buyerSigned)
        {
            sb.Append("<div style='padding: 8px; background: #dcfce7; border: 1px solid #86efac; border-radius: 6px; color: #166534; font-size: 12px; font-weight: bold;'>");
            sb.Append("✅ ĐÃ KÝ ĐIỆN TỬ<br/><span style='font-size: 10px; font-weight: normal; color: #15803d;'>Xác thực OTP thành công</span>");
            sb.Append("</div>");
        }
        else
        {
            sb.Append("<div style='padding: 8px; background: #ffedd5; border: 1px solid #fed7aa; border-radius: 6px; color: #c2410c; font-size: 12px; font-weight: bold;'>");
            sb.Append("⏳ CHỜ KÝ XÁC NHẬN<br/><span style='font-size: 10px; font-weight: normal; color: #9a3412;'>Đang chờ thao tác OTP</span>");
            sb.Append("</div>");
        }
        sb.Append($"<p style='margin: 8px 0 0 0; font-size: 12px; font-weight: bold;'>{buyerProfile?.FullName ?? buyer?.UserName}</p>");
        sb.Append("</div>");

        // Cột Bên C
        sb.Append("<div style='border: 1px solid #cbd5e1; border-radius: 8px; padding: 12px; background: #fafafa;'>");
        sb.Append("<p style='font-weight: bold; margin: 0 0 6px 0; font-size: 13px;'>SÀN TRUSTSIM (BÊN C)</p>");
        sb.Append("<p style='font-size: 12px; color: #64748b; margin: 0 0 10px 0;'>Đơn vị trung gian tín thác</p>");
        if (isSealed)
        {
            sb.Append("<div style='padding: 8px; background: #dbeafe; border: 1px solid #93c5fd; border-radius: 6px; color: #1e40af; font-size: 12px; font-weight: bold;'>");
            sb.Append("🔒 ĐÃ NIÊM PHONG PAdES<br/><span style='font-size: 10px; font-weight: normal; color: #1d4ed8;'>Timestamp TSA X.509</span>");
            sb.Append("</div>");
        }
        else
        {
            sb.Append("<div style='padding: 8px; background: #f1f5f9; border: 1px solid #cbd5e1; border-radius: 6px; color: #475569; font-size: 12px; font-weight: bold;'>");
            sb.Append("⏳ CHỜ CẢ 2 BÊN KÝ<br/><span style='font-size: 10px; font-weight: normal; color: #64748b;'>Sẽ niêm phong tự động</span>");
            sb.Append("</div>");
        }
        sb.Append("<p style='margin: 8px 0 0 0; font-size: 12px; font-weight: bold;'>TRUSTSIM PLATFORM</p>");
        sb.Append("</div>");

        sb.Append("</div>"); // đóng grid chữ ký
        sb.Append("</div>"); // đóng container

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

        var escrowAmount = escrow?.EscrowAmount ?? 0m;
        var priceStr = escrowAmount.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ";
        var priceWords = VietnameseNumberToWordsHelper.ToVietnameseWords(escrowAmount);

        var phoneStr = sim?.PhoneNumber ?? "Chưa xác định";
        var carrierStr = sim?.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông Việt Nam";

        var sellerProfile = seller?.UserProfile;
        var buyerProfile = buyer?.UserProfile;

        using var memoryStream = new MemoryStream();
        var writer = new PdfWriter(memoryStream);
        var pdf = new PdfDocument(writer);
        var document = new Document(pdf, PageSize.A4);
        document.SetMargins(30, 36, 30, 36);

        var (regularFont, boldFont, italicFont) = ResolveVietnameseFonts();
        if (regularFont != null) document.SetFont(regularFont);

        // Header Quốc hiệu & Tiêu ngữ
        var pQuocHieu = new Paragraph("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(12);
        if (boldFont != null) pQuocHieu.SetFont(boldFont); else pQuocHieu.SetBold();
        document.Add(pQuocHieu);

        var pTieuNgu = new Paragraph("Độc lập - Tự do - Hạnh phúc\n---------------------------------")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(10.5f);
        if (boldFont != null) pTieuNgu.SetFont(boldFont); else pTieuNgu.SetBold();
        document.Add(pTieuNgu);

        // Tiêu đề hợp đồng
        var pTitle = new Paragraph("HỢP ĐỒNG THỎA THUẬN CHUYỂN NHƯỢNG QUYỀN SỬ DỤNG SỐ THUÊ BAO")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(13)
            .SetFontColor(ColorConstants.BLUE)
            .SetMarginTop(10);
        if (boldFont != null) pTitle.SetFont(boldFont); else pTitle.SetBold();
        document.Add(pTitle);

        var pSubTitle = new Paragraph("(Theo Mẫu số 13 Phụ lục 5 Bộ Thông tin & Truyền thông & Nghị định số 49/2017/NĐ-CP)")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(8.5f)
            .SetFontColor(ColorConstants.DARK_GRAY);
        if (italicFont != null) pSubTitle.SetFont(italicFont); else pSubTitle.SetItalic();
        document.Add(pSubTitle);

        var pContractCode = new Paragraph($"Số HĐ: HĐ-CN/{trade.TradeTransactionCode} | Ngày lập: {trade.CreatedDate:dd/MM/yyyy HH:mm} (UTC)")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(8.5f)
            .SetFontColor(ColorConstants.GRAY)
            .SetMarginBottom(10);
        if (italicFont != null) pContractCode.SetFont(italicFont); else pContractCode.SetItalic();
        document.Add(pContractCode);

        // Căn cứ pháp lý
        var pLegal = new Paragraph("- Căn cứ Bộ luật Dân sự 2015, Luật Viễn thông 2023 và Luật Giao dịch điện tử 2023;\n- Căn cứ Nghị định 49/2017/NĐ-CP về quản lý thuê bao viễn thông và Quy chế hoạt động Sàn Đấu giá Trực tuyến TrustSim;")
            .SetFontSize(8.5f)
            .SetFontColor(ColorConstants.DARK_GRAY)
            .SetMarginBottom(8);
        if (italicFont != null) pLegal.SetFont(italicFont); else pLegal.SetItalic();
        document.Add(pLegal);

        // Phần I: Các bên
        var pSec1 = new Paragraph("I. CÁC BÊN THAM GIA GIAO KẾT").SetFontSize(10.5f);
        if (boldFont != null) pSec1.SetFont(boldFont); else pSec1.SetBold();
        document.Add(pSec1);

        document.Add(new Paragraph($"1. BÊN CHUYỂN QUYỀN (BÊN A): Ông/Bà {sellerProfile?.FullName ?? seller?.UserName} | Số CCCD/Định danh: {FormatIdentity(sellerProfile?.IdentityNumber)} | Ngày sinh: {(sellerProfile?.DateOfBirth.HasValue == true ? sellerProfile.DateOfBirth.Value.ToString("dd/MM/yyyy") : "N/A")} | Đ/c: {(string.IsNullOrWhiteSpace(sellerProfile?.Address) ? "Theo hồ sơ eKYC" : sellerProfile.Address)} | SĐT: {(string.IsNullOrWhiteSpace(sellerProfile?.PhoneNumber) ? seller?.PhoneNumber ?? "N/A" : sellerProfile.PhoneNumber)} | Email: {seller?.Email ?? "N/A"}").SetFontSize(9));
        document.Add(new Paragraph($"2. BÊN NHẬN CHUYỂN QUYỀN (BÊN B): Ông/Bà {buyerProfile?.FullName ?? buyer?.UserName} | Số CCCD/Định danh: {FormatIdentity(buyerProfile?.IdentityNumber)} | Ngày sinh: {(buyerProfile?.DateOfBirth.HasValue == true ? buyerProfile.DateOfBirth.Value.ToString("dd/MM/yyyy") : "N/A")} | Đ/c: {(string.IsNullOrWhiteSpace(buyerProfile?.Address) ? "Theo hồ sơ eKYC" : buyerProfile.Address)} | SĐT: {(string.IsNullOrWhiteSpace(buyerProfile?.PhoneNumber) ? buyer?.PhoneNumber ?? "N/A" : buyerProfile.PhoneNumber)} | Email: {buyer?.Email ?? "N/A"}").SetFontSize(9));
        document.Add(new Paragraph("3. ĐƠN VỊ TRUNG GIAN TÍN THÁC & GIÁM SÁT (BÊN C): CÔNG TY CỔ PHẦN CÔNG NGHỆ & ĐẤU GIÁ TRỰC TUYẾN TRUSTSIM | Đại diện: Ban Giám Đốc | Vai trò: Quản lý Quỹ Escrow, bảo đảm tài chính, chứng thực dữ liệu và niêm phong số PAdES X.509.").SetFontSize(9));

        // Phần II: Điều khoản
        var pSec2 = new Paragraph("\nII. ĐIỀU KHOẢN THỎA THUẬN CHUYỂN NHƯỢNG").SetFontSize(10.5f);
        if (boldFont != null) pSec2.SetFont(boldFont); else pSec2.SetBold();
        document.Add(pSec2);

        document.Add(new Paragraph($"Điều 1. Đối tượng hợp đồng: Bên A tự nguyện chuyển nhượng và Bên B đồng ý nhận chuyển quyền sử dụng số thuê bao viễn thông {phoneStr} thuộc mạng {carrierStr}. Bên A cam kết là chủ sở hữu hợp pháp duy nhất, số SIM không bị tranh chấp, không phong tỏa và không nợ cước.").SetFontSize(9));
        document.Add(new Paragraph($"Điều 2. Giá trị chuyển nhượng & Quỹ Escrow: Giá trị chuyển nhượng là {priceStr} (Bằng chữ: {priceWords}). Toàn bộ 100% số tiền đã được Bên B thanh toán đầy đủ vào Quỹ Escrow an toàn của Bên C. Bên C giải ngân cho Bên A khi thủ tục sang tên hoàn tất thành công và được nghiệm thu.").SetFontSize(9));
        document.Add(new Paragraph($"Điều 3. Nghĩa vụ cước viễn thông: Bên A cam kết thanh toán dứt điểm toàn bộ nợ cước trước thời điểm sang tên. Hợp đồng dịch vụ cũ của Bên A với Nhà mạng chấm dứt hiệu lực khi Bên B hoàn tất đăng ký mới. Mọi gói cước khuyến mại, điểm ưu đãi cũ của Bên A sẽ tự động hủy bỏ theo quy định nhà mạng.").SetFontSize(9));
        document.Add(new Paragraph($"Điều 4. Thủ tục sang tên đổi chủ: Trong thời hạn 07 ngày làm việc kể từ ngày hợp đồng có hiệu lực, Bên A có nghĩa vụ phối hợp làm thủ tục chuyển quyền sử dụng. Bên B có trách nhiệm xuất trình CCCD gắn chip và chụp ảnh chân dung theo đúng Nghị định 49/2017/NĐ-CP để đăng ký thuê bao chính chủ mới.").SetFontSize(9));
        document.Add(new Paragraph("Điều 5. Cơ chế giải ngân và chế tài: Khi sang tên thành công và nghiệm thu, Bên C khấu trừ 5% phí sàn và giải ngân 95% số tiền còn lại cho Bên A. Nếu Bên A vi phạm không sang tên, toàn bộ tiền cọc cam kết bán sẽ bị tịch thu (50% bồi thường Bên B, 50% phạt quỹ sàn) và hoàn trả 100% Escrow cho Bên B.").SetFontSize(9));
        document.Add(new Paragraph("Điều 6. Hiệu lực pháp lý: Hợp đồng được giao kết dưới hình thức thông điệp dữ liệu điện tử theo Luật Giao dịch điện tử số 20/2023/QH15, xác thực bằng OTP liên kết giao dịch và được Bên C niêm phong Chữ ký số PAdES X.509 có gắn nhãn thời gian TSA.").SetFontSize(9));

        if (isAutoEnforced)
        {
            var pAuto = new Paragraph("\n[GHI CHÚ ĐẶC BIỆT: HỢP ĐỒNG ĐƯỢC NIÊM PHONG QUA CƠ CHẾ ỦY QUYỀN CƯỠNG CHẾ DO BÊN BÁN QUÁ HẠN 24H]")
                .SetFontColor(ColorConstants.RED)
                .SetFontSize(8.5f);
            if (boldFont != null) pAuto.SetFont(boldFont); else pAuto.SetBold();
            document.Add(pAuto);
        }

        // Bảng xác nhận các bên
        var pSec3 = new Paragraph("\nIII. XÁC NHẬN CỦA CÁC BÊN").SetFontSize(10.5f).SetMarginTop(10);
        if (boldFont != null) pSec3.SetFont(boldFont); else pSec3.SetBold();
        document.Add(pSec3);

        var table = new Table(UnitValue.CreatePercentArray(new float[] { 33, 33, 34 })).UseAllAvailableWidth();
        
        var cellAHeader = new Cell().Add(CreateCellPara("BÊN CHUYỂN QUYỀN (BÊN A)", boldFont, true)).SetBorder(Border.NO_BORDER);
        var cellBHeader = new Cell().Add(CreateCellPara("BÊN NHẬN QUYỀN (BÊN B)", boldFont, true)).SetBorder(Border.NO_BORDER);
        var cellCHeader = new Cell().Add(CreateCellPara("SÀN TRUSTSIM (BÊN C)", boldFont, true)).SetBorder(Border.NO_BORDER);
        table.AddHeaderCell(cellAHeader);
        table.AddHeaderCell(cellBHeader);
        table.AddHeaderCell(cellCHeader);

        var noteA = isAutoEnforced ? "(Sàn đại diện ủy quyền)" : "(Đã xác thực OTP)";
        table.AddCell(new Cell().Add(CreateCellPara(noteA, italicFont, false, true)).SetBorder(Border.NO_BORDER));
        table.AddCell(new Cell().Add(CreateCellPara("(Đã xác thực OTP)", italicFont, false, true)).SetBorder(Border.NO_BORDER));
        table.AddCell(new Cell().Add(CreateCellPara("(Ký số PAdES niêm phong)", italicFont, false, true)).SetBorder(Border.NO_BORDER));

        document.Add(table);
        document.Close();

        return memoryStream.ToArray();
    }

    #endregion

    #region VĂN BẢN 1: PHIẾU XÁC NHẬN KẾT QUẢ ĐẤU GIÁ VÀ CHỐT GIÁ TRÚNG ĐIỆN TỬ (MẪU TP-DGTS-18)

    public async Task<byte[]> GenerateWinningNoticePdfAsync(Guid tradeId, CancellationToken ct = default)
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
        var auction = trade.Auction;

        var escrowAmount = escrow?.EscrowAmount ?? 0m;
        var winningPriceStr = escrowAmount.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ";
        var winningPriceWords = VietnameseNumberToWordsHelper.ToVietnameseWords(escrowAmount);

        var startingPrice = auction?.StartingPrice ?? 0m;
        var startingPriceStr = startingPrice.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ";
        var startingPriceWords = startingPrice > 0 ? VietnameseNumberToWordsHelper.ToVietnameseWords(startingPrice) : "Không đồng";

        var depositAmount = auction?.AuctionRule?.DepositAmount ?? 0m;
        var depositAmountStr = depositAmount.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ";
        var depositAmountWords = depositAmount > 0 ? VietnameseNumberToWordsHelper.ToVietnameseWords(depositAmount) : "Không đồng";

        var phoneStr = sim?.PhoneNumber ?? "Chưa xác định";
        var carrierStr = sim?.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông Việt Nam";

        var sellerProfile = seller?.UserProfile;
        var buyerProfile = buyer?.UserProfile;

        using var memoryStream = new MemoryStream();
        var writer = new PdfWriter(memoryStream);
        var pdf = new PdfDocument(writer);
        var document = new Document(pdf, PageSize.A4);
        document.SetMargins(30, 36, 30, 36);

        var (regularFont, boldFont, italicFont) = ResolveVietnameseFonts();
        if (regularFont != null) document.SetFont(regularFont);

        // Header Quốc hiệu & Tiêu ngữ
        var pQuocHieu = new Paragraph("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(12);
        if (boldFont != null) pQuocHieu.SetFont(boldFont); else pQuocHieu.SetBold();
        document.Add(pQuocHieu);

        var pTieuNgu = new Paragraph("Độc lập - Tự do - Hạnh phúc\n---------------------------------")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(10.5f);
        if (boldFont != null) pTieuNgu.SetFont(boldFont); else pTieuNgu.SetBold();
        document.Add(pTieuNgu);

        // Tiêu đề
        var pTitle = new Paragraph("BIÊN BẢN ĐẤU GIÁ TÀI SẢN VÀ CHỐT KẾT QUẢ ĐIỆN TỬ")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(13)
            .SetFontColor(ColorConstants.BLUE)
            .SetMarginTop(10);
        if (boldFont != null) pTitle.SetFont(boldFont); else pTitle.SetBold();
        document.Add(pTitle);

        var pSubTitle = new Paragraph("(Theo Mẫu số TP-DGTS-18 ban hành kèm theo Thông tư số 19/2024/TT-BTP của Bộ Tư pháp)")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(8.5f)
            .SetFontColor(ColorConstants.DARK_GRAY);
        if (italicFont != null) pSubTitle.SetFont(italicFont); else pSubTitle.SetItalic();
        document.Add(pSubTitle);

        var pCode = new Paragraph($"Số biên bản: BB-DG/{trade.TradeTransactionCode} | Thời gian lập: {DateTime.UtcNow:dd/MM/yyyy HH:mm} (UTC)")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(8.5f)
            .SetFontColor(ColorConstants.GRAY)
            .SetMarginBottom(10);
        if (italicFont != null) pCode.SetFont(italicFont); else pCode.SetItalic();
        document.Add(pCode);

        // Căn cứ
        var pLegal = new Paragraph("- Căn cứ Luật Đấu giá tài sản số 01/2016/QH14 và Luật sửa đổi, bổ sung một số điều của Luật Đấu giá tài sản số 37/2024/QH15;\n- Căn cứ Thông tư 19/2024/TT-BTP hướng dẫn nghiệp vụ đấu giá tài sản;\n- Căn cứ Quy chế Đấu giá Trực tuyến trên Nền tảng TrustSim.")
            .SetFontSize(8.5f)
            .SetFontColor(ColorConstants.DARK_GRAY)
            .SetMarginBottom(8);
        if (italicFont != null) pLegal.SetFont(italicFont); else pLegal.SetItalic();
        document.Add(pLegal);

        // Phần I: Thông tin tổ chức & Tài sản đấu giá
        var pSec1 = new Paragraph("I. ĐƠN VỊ TỔ CHỨC ĐẤU GIÁ VÀ TÀI SẢN ĐẤU GIÁ").SetFontSize(10.5f);
        if (boldFont != null) pSec1.SetFont(boldFont); else pSec1.SetBold();
        document.Add(pSec1);

        document.Add(new Paragraph("1. Đơn vị tổ chức đấu giá: CÔNG TY CỔ PHẦN CÔNG NGHỆ & ĐẤU GIÁ TRỰC TUYẾN TRUSTSIM\n   • Hình thức đấu giá: Trực tuyến trên Cổng thông tin Đấu giá TrustSim (https://trustsim.vn)\n   • Phương thức đấu giá: Trả giá lên theo bước giá quy định hoặc Đấu giá giảm dần (Hà Lan)").SetFontSize(9));
        document.Add(new Paragraph($"2. Người có tài sản đấu giá: Ông/Bà {sellerProfile?.FullName ?? seller?.UserName} | Số CCCD/Định danh: {FormatIdentity(sellerProfile?.IdentityNumber)} | SĐT: {(string.IsNullOrWhiteSpace(sellerProfile?.PhoneNumber) ? seller?.PhoneNumber ?? "N/A" : sellerProfile.PhoneNumber)} | Email: {seller?.Email ?? "N/A"}").SetFontSize(9));
        document.Add(new Paragraph($"3. Tài sản đấu giá: Quyền sử dụng số thuê bao viễn thông di động {phoneStr} ({carrierStr})\n   • Giá khởi điểm: {startingPriceStr} (Bằng chữ: {startingPriceWords})\n   • Tiền đặt trước đã nộp: {depositAmountStr} (Bằng chữ: {depositAmountWords})").SetFontSize(9));

        // Phần II: Diễn biến & Kết quả đấu giá
        var pSec2 = new Paragraph("\nII. DIỄN BIẾN VÀ KẾT QUẢ ĐẤU GIÁ").SetFontSize(10.5f);
        if (boldFont != null) pSec2.SetFont(boldFont); else pSec2.SetBold();
        document.Add(pSec2);

        document.Add(new Paragraph($"1. Phiên đấu giá kết thúc hợp lệ với mức giá trả cao nhất là: {winningPriceStr} (Bằng chữ: {winningPriceWords}).").SetFontSize(9));
        document.Add(new Paragraph($"2. Người trúng đấu giá: Ông/Bà {buyerProfile?.FullName ?? buyer?.UserName} | Số CCCD/Định danh: {FormatIdentity(buyerProfile?.IdentityNumber)} | Ngày sinh: {(buyerProfile?.DateOfBirth.HasValue == true ? buyerProfile.DateOfBirth.Value.ToString("dd/MM/yyyy") : "N/A")} | Đ/c: {(string.IsNullOrWhiteSpace(buyerProfile?.Address) ? "Theo hồ sơ eKYC" : buyerProfile.Address)} | SĐT: {(string.IsNullOrWhiteSpace(buyerProfile?.PhoneNumber) ? buyer?.PhoneNumber ?? "N/A" : buyerProfile.PhoneNumber)} | Email: {buyer?.Email ?? "N/A"}").SetFontSize(9));
        document.Add(new Paragraph("3. Tình trạng ký quỹ: Toàn bộ 100% số tiền trúng đấu giá đã được Người trúng đấu giá nộp đủ và phong tỏa an toàn trong Quỹ Ký quỹ Escrow của Sàn TrustSim.").SetFontSize(9));

        // Phần III: Trách nhiệm & Cam kết các bên
        var pSec3 = new Paragraph("\nIII. TRÁCH NHIỆM VÀ NGHĨA VỤ HOÀN TẤT").SetFontSize(10.5f);
        if (boldFont != null) pSec3.SetFont(boldFont); else pSec3.SetBold();
        document.Add(pSec3);

        document.Add(new Paragraph("1. Nghĩa vụ ký kết hợp đồng: Hai bên có trách nhiệm ký kết Hợp đồng chuyển nhượng quyền sử dụng số thuê bao điện tử trên hệ thống TrustSim trong thời hạn 24 giờ kể từ thời điểm chốt phiên.").SetFontSize(9));
        document.Add(new Paragraph("2. Nghĩa vụ sang tên chính chủ: Trong thời hạn 07 ngày làm việc kể từ khi Hợp đồng có hiệu lực, Người có tài sản và Người trúng đấu giá có trách nhiệm cùng thực hiện thủ tục đăng ký đổi chủ sở hữu tại Điểm giao dịch nhà mạng viễn thông theo Nghị định 49/2017/NĐ-CP.").SetFontSize(9));
        document.Add(new Paragraph("3. Chế tài vi phạm: Nếu Người trúng đấu giá hoặc Người có tài sản vi phạm thời hạn hoặc từ chối thực hiện nghĩa vụ, sẽ bị xử lý theo Luật Đấu giá tài sản và Quy chế sàn (tịch thu tiền cọc, bồi thường thiệt hại và chế tài tài khoản).").SetFontSize(9));

        // Phần IV: Xác nhận của Tổ chức đấu giá
        var pSec4 = new Paragraph("\nIV. XÁC NHẬN CỦA ĐƠN VỊ TỔ CHỨC ĐẤU GIÁ").SetFontSize(10.5f).SetMarginTop(10);
        if (boldFont != null) pSec4.SetFont(boldFont); else pSec4.SetBold();
        document.Add(pSec4);

        var pCert = new Paragraph("Chứng từ này được hệ thống Đấu giá Trực tuyến TrustSim tự động khởi tạo, bảo đảm tính toàn vẹn và niêm phong điện tử theo Luật Giao dịch điện tử số 20/2023/QH15.")
            .SetFontSize(8.5f);
        if (italicFont != null) pCert.SetFont(italicFont); else pCert.SetItalic();
        document.Add(pCert);

        var pStamp = new Paragraph("[ĐÃ CHỨNG THỰC ĐIỆN TỬ BỞI NỀN TẢNG TRUSTSIM - HỆ THỐNG GIAO DỊCH BẢO ĐẢM]")
            .SetFontColor(ColorConstants.BLUE)
            .SetFontSize(9)
            .SetMarginTop(8);
        if (boldFont != null) pStamp.SetFont(boldFont); else pStamp.SetBold();
        document.Add(pStamp);

        document.Close();
        return memoryStream.ToArray();
    }

    public async Task<string> EnsureWinningNoticeUploadedAsync(Guid tradeId, CancellationToken ct = default)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(tradeId, ct);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch với ID '{tradeId}'.");
        }

        if (trade.TransferContract != null && !string.IsNullOrWhiteSpace(trade.TransferContract.WinningNoticeUrl))
        {
            return trade.TransferContract.WinningNoticeUrl;
        }

        var noticeBytes = await GenerateWinningNoticePdfAsync(trade.Id, ct);
        var simPhone = trade.SimNumber?.PhoneNumber?.Replace("+", "") ?? "SIM";
        var noticeFileName = $"PhieuChotGia_{trade.TradeTransactionCode}_{simPhone}.pdf";
        var noticeUrl = await _cloudStorageService.UploadImageBytesAsync(noticeBytes, noticeFileName, "contracts", "application/pdf", ct);

        using var sha = SHA256.Create();
        var noticeHash = Convert.ToHexString(sha.ComputeHash(noticeBytes)).ToLowerInvariant();

        if (trade.TransferContract == null)
        {
            trade.TransferContract = new TransferContract
            {
                Id = Guid.NewGuid(),
                TradeTransactionId = trade.Id,
                ContractStatus = ContractStatus.DRAFT,
                DocumentUrl = string.Empty,
                DocumentHash = string.Empty
            };
        }

        trade.TransferContract.WinningNoticeUrl = noticeUrl;
        trade.TransferContract.WinningNoticeHash = noticeHash;
        await _unitOfWork.SaveChangesAsync(ct);

        return noticeUrl;
    }

    #endregion

    #region VĂN BẢN 3: BIÊN BẢN NGHIỆM THU VÀ THANH LÝ GIAO DỊCH ĐIỆN TỬ (MẪU THANH LÝ HỢP ĐỒNG)

    public async Task<string> GetLiquidationPreviewHtmlAsync(Guid tradeId, Guid currentUserId, CancellationToken ct = default)
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

        var escrowAmount = escrow?.EscrowAmount ?? 0m;
        var platformFeePercent = 0.05m;
        var platformFee = Math.Round(escrowAmount * platformFeePercent, 2);
        var sellerPayout = escrowAmount - platformFee;

        var priceStr = escrowAmount.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ";
        var priceWords = VietnameseNumberToWordsHelper.ToVietnameseWords(escrowAmount);

        var feeStr = platformFee.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ";
        var feeWords = VietnameseNumberToWordsHelper.ToVietnameseWords(platformFee);

        var payoutStr = sellerPayout.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ";
        var payoutWords = VietnameseNumberToWordsHelper.ToVietnameseWords(sellerPayout);

        var phoneStr = sim?.PhoneNumber ?? "Chưa xác định";
        var carrierStr = sim?.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông Việt Nam";
        var isSealed = !string.IsNullOrWhiteSpace(contract?.LiquidationDocumentUrl) || contract?.LiquidationSealedAt.HasValue == true;

        var sellerProfile = seller?.UserProfile;
        var buyerProfile = buyer?.UserProfile;

        var sb = new StringBuilder();
        sb.Append("<div class='liquidation-preview' style='font-family: Arial, sans-serif; line-height: 1.6; color: #1e293b; max-width: 850px; margin: 0 auto; padding: 32px; background: #ffffff; border-radius: 12px; box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.1); border: 1px solid #e2e8f0;'>");

        // Header
        sb.Append("<div style='text-align: center; margin-bottom: 24px; border-bottom: 1px solid #cbd5e1; padding-bottom: 18px;'>");
        sb.Append("<h3 style='margin: 0; font-size: 14px; font-weight: bold; text-transform: uppercase; letter-spacing: 0.5px;'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</h3>");
        sb.Append("<p style='margin: 4px 0; font-size: 13px; font-weight: bold;'>Độc lập - Tự do - Hạnh phúc</p>");
        sb.Append("<p style='margin: 0; font-size: 12px; color: #64748b;'>---------------------------------------------------</p>");
        sb.Append("<h2 style='margin-top: 18px; margin-bottom: 6px; color: #15803d; font-size: 18px; text-transform: uppercase; font-weight: 800; line-height: 1.3;'>BIÊN BẢN NGHIỆM THU BÀN GIAO VÀ THANH LÝ HỢP ĐỒNG ĐIỆN TỬ</h2>");
        sb.Append($"<p style='margin: 2px 0; font-size: 12.5px; color: #475569; font-style: italic;'>V/v: Nghiệm thu hoàn thành chuyển quyền sử dụng SIM và thanh lý Hợp đồng số: <strong>{trade.TradeTransactionCode}</strong></p>");
        sb.Append($"<p style='margin-top: 8px; font-size: 13px; color: #334155;'>Số BB: <strong>TL-HD/{trade.TradeTransactionCode}</strong> | Ngày lập: {DateTime.UtcNow:dd/MM/yyyy HH:mm} (UTC)</p>");
        sb.Append("</div>");

        // Phần I: Các bên
        sb.Append("<h4 style='color: #0f172a; border-bottom: 2px solid #16a34a; padding-bottom: 6px; font-size: 14px; text-transform: uppercase;'>I. CÁC BÊN THAM GIA GIAO KẾT THANH LÝ</h4>");

        // Bên A (Bên Mua)
        sb.Append("<div style='margin-bottom: 14px; padding: 12px; background: #f0fdf4; border-radius: 6px; border: 1px solid #bbf7d0;'>");
        sb.Append("<p style='margin: 0 0 6px 0; font-weight: bold; color: #166534;'>BÊN NHẬN CHUYỂN QUYỀN (BÊN MUA / BÊN A - ĐẠI DIỆN NGHIỆM THU):</p>");
        sb.Append("<table style='width: 100%; font-size: 13px; line-height: 1.6;'>");
        sb.Append($"<tr><td style='width: 30%; color: #64748b;'>Họ và tên:</td><td style='font-weight: bold;'>{buyerProfile?.FullName ?? buyer?.UserName ?? "Chưa cập nhật"}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Số CCCD/Định danh:</td><td style='font-weight: bold;'>{FormatIdentity(buyerProfile?.IdentityNumber)}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Số điện thoại / Email:</td><td>{(string.IsNullOrWhiteSpace(buyerProfile?.PhoneNumber) ? buyer?.PhoneNumber ?? "N/A" : buyerProfile.PhoneNumber)} | {buyer?.Email ?? "N/A"}</td></tr>");
        sb.Append("<tr><td style='color: #64748b;'>Kết quả nghiệm thu:</td><td><span style='color: #16a34a; font-weight: bold;'>✅ ĐÃ XÁC NHẬN NHẬN SIM CHÍNH CHỦ, SÓNG ỔN ĐỊNH</span></td></tr>");
        sb.Append("</table>");
        sb.Append("</div>");

        // Bên B (Bên Bán)
        sb.Append("<div style='margin-bottom: 14px; padding: 12px; background: #f8fafc; border-radius: 6px; border: 1px solid #e2e8f0;'>");
        sb.Append("<p style='margin: 0 0 6px 0; font-weight: bold; color: #1e3a8a;'>BÊN CHUYỂN QUYỀN (BÊN BÁN / BÊN B - BÊN BÀN GIAO):</p>");
        sb.Append("<table style='width: 100%; font-size: 13px; line-height: 1.6;'>");
        sb.Append($"<tr><td style='width: 30%; color: #64748b;'>Họ và tên:</td><td style='font-weight: bold;'>{sellerProfile?.FullName ?? seller?.UserName ?? "Chưa cập nhật"}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Số CCCD/Định danh:</td><td style='font-weight: bold;'>{FormatIdentity(sellerProfile?.IdentityNumber)}</td></tr>");
        sb.Append($"<tr><td style='color: #64748b;'>Số điện thoại / Email:</td><td>{(string.IsNullOrWhiteSpace(sellerProfile?.PhoneNumber) ? seller?.PhoneNumber ?? "N/A" : sellerProfile.PhoneNumber)} | {seller?.Email ?? "N/A"}</td></tr>");
        sb.Append("<tr><td style='color: #64748b;'>Tình trạng bàn giao:</td><td><span style='color: #16a34a; font-weight: bold;'>✅ ĐÃ HOÀN TẤT THỦ TỤC SANG TÊN TẠI NHÀ MẠNG</span></td></tr>");
        sb.Append("</table>");
        sb.Append("</div>");

        // Bên C (Sàn TrustSim)
        sb.Append("<div style='margin-bottom: 16px; padding: 12px; background: #f8fafc; border-radius: 6px; border: 1px dashed #94a3b8;'>");
        sb.Append("<p style='margin: 0 0 6px 0; font-weight: bold; color: #0f172a;'>BÊN TRUNG GIAN TÍN THÁC & TẤT TOÁN ESCROW (BÊN C - SÀN TRUSTSIM):</p>");
        sb.Append("<table style='width: 100%; font-size: 13px; line-height: 1.6;'>");
        sb.Append("<tr><td style='width: 30%; color: #64748b;'>Tên tổ chức:</td><td style='font-weight: bold;'>CÔNG TY CỔ PHẦN CÔNG NGHỆ & ĐẤU GIÁ TRỰC TUYẾN TRUSTSIM</td></tr>");
        sb.Append("<tr><td style='color: #64748b;'>Trách nhiệm:</td><td>Tất toán Quỹ Ký quỹ Escrow, giải ngân tài chính cho Bên Bán và niêm phong số PAdES X.509.</td></tr>");
        sb.Append("</table>");
        sb.Append("</div>");

        // Phần II: Nội dung nghiệm thu và thanh lý
        sb.Append("<h4 style='color: #0f172a; border-bottom: 2px solid #16a34a; padding-bottom: 6px; font-size: 14px; text-transform: uppercase; margin-top: 24px;'>II. NỘI DUNG NGHIỆM THU VÀ QUYẾT TOÁN TÀI CHÍNH</h4>");

        sb.Append("<div style='font-size: 13.5px; line-height: 1.7; color: #1e293b; text-align: justify;'>");
        sb.Append($"<p><strong>Điều 1. Nghiệm thu hoàn tất việc chuyển giao quyền sử dụng:</strong><br/>");
        sb.Append($"1.1. Bên A xác nhận Bên B đã phối hợp hoàn tất toàn bộ thủ tục sang tên đổi chủ quyền sử dụng số thuê bao viễn thông: <strong style='font-size: 15px; color: #166534;'>{phoneStr}</strong> thuộc mạng <strong>{carrierStr}</strong> tại Điểm giao dịch nhà mạng theo đúng quy định tại Nghị định số 49/2017/NĐ-CP.<br/>");
        sb.Append($"1.2. Bên A xác nhận đã nhận bàn giao SIM, đã kích hoạt sử dụng có sóng, nghe gọi/SMS bình thường; tra cứu thông tin chính chủ (qua đầu số 1414) khớp hoàn toàn với thông tin Căn cước công dân của Bên A.<br/>");
        sb.Append("1.3. Hai bên thống nhất nghiệm thu đạt yêu cầu 100%, không có bất kỳ tranh chấp hay khiếu nại nào về quyền sử dụng thuê bao.</p>");

        sb.Append($"<p><strong>Điều 2. Quyết toán tài chính và giải tỏa Quỹ Ký quỹ Escrow:</strong><br/>");
        sb.Append($"2.1. Tổng giá trị chuyển nhượng theo Hợp đồng: <strong>{priceStr}</strong> (Bằng chữ: <em>{priceWords}</em>).<br/>");
        sb.Append($"2.2. Phí dịch vụ sàn giao dịch TrustSim (5%): <strong>{feeStr}</strong> (Bằng chữ: <em>{feeWords}</em>).<br/>");
        sb.Append($"2.3. Số tiền thực nhận giải ngân cho Bên Bán (Bên B): <strong style='font-size: 15px; color: #15803d;'>{payoutStr}</strong> (Bằng chữ: <em>{payoutWords}</em>).<br/>");
        sb.Append("2.4. Căn cứ kết quả nghiệm thu thành công, Bên C đã thực hiện giải tỏa Quỹ Ký quỹ Escrow, ghi có số tiền thực nhận vào Số dư ví khả dụng của Bên B và hoàn tất quyết toán phí dịch vụ.</p>");

        sb.Append("<p><strong>Điều 3. Chấm dứt hiệu lực hợp đồng và cam kết dứt điểm:</strong><br/>");
        sb.Append($"3.1. Kể từ thời điểm Biên bản này được ký và niêm phong, Hợp đồng chuyển nhượng quyền sử dụng số thuê bao số <strong>{trade.TradeTransactionCode}</strong> chính thức hết hiệu lực và chấm dứt mọi quyền, nghĩa vụ giữa các bên.<br/>");
        sb.Append("3.2. Hai bên cam kết không còn bất kỳ khiếu nại, khiếu kiện hay đòi hỏi bồi thường tài chính nào khác liên quan đến giao dịch này.<br/>");
        sb.Append("3.3. Bên B cam kết bảo mật toàn bộ dữ liệu cá nhân của Bên A và không thực hiện bất kỳ hành vi nào gây ảnh hưởng đến quyền sử dụng số thuê bao của Bên A trong tương lai.</p>");

        sb.Append("<p><strong>Điều 4. Giá trị pháp lý điện tử:</strong><br/>");
        sb.Append("Biên bản này được lập dưới dạng chứng từ điện tử theo quy định của Luật Giao dịch điện tử số 20/2023/QH15, có giá trị pháp lý tương đương văn bản giấy gốc và được niêm phong xác thực bằng Chữ ký số PAdES X.509 có gắn nhãn thời gian TSA của Sàn TrustSim.</p>");
        sb.Append("</div>");

        // Bảng xác nhận
        sb.Append("<h4 style='color: #0f172a; border-bottom: 2px solid #16a34a; padding-bottom: 6px; font-size: 14px; text-transform: uppercase; margin-top: 24px;'>III. XÁC NHẬN CỦA CÁC BÊN</h4>");
        sb.Append("<div style='display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 12px; margin-top: 16px; text-align: center;'>");

        sb.Append("<div style='border: 1px solid #cbd5e1; border-radius: 8px; padding: 12px; background: #fafafa;'>");
        sb.Append("<p style='font-weight: bold; margin: 0 0 4px 0; font-size: 13px;'>BÊN MUA (BÊN A)</p>");
        sb.Append("<p style='font-size: 11px; color: #64748b; margin: 0 0 10px 0;'>Bên nghiệm thu nhận SIM</p>");
        sb.Append("<div style='padding: 8px; background: #dcfce7; border: 1px solid #86efac; border-radius: 6px; color: #166534; font-size: 12px; font-weight: bold;'>✅ ĐÃ NGHIỆM THU<br/><span style='font-size: 10px; font-weight: normal; color: #15803d;'>Xác nhận nhận SIM chính chủ</span></div>");
        sb.Append($"<p style='margin: 8px 0 0 0; font-size: 12px; font-weight: bold;'>{buyerProfile?.FullName ?? buyer?.UserName}</p>");
        sb.Append("</div>");

        sb.Append("<div style='border: 1px solid #cbd5e1; border-radius: 8px; padding: 12px; background: #fafafa;'>");
        sb.Append("<p style='font-weight: bold; margin: 0 0 4px 0; font-size: 13px;'>BÊN BÁN (BÊN B)</p>");
        sb.Append("<p style='font-size: 11px; color: #64748b; margin: 0 0 10px 0;'>Bên bàn giao quyền sở hữu</p>");
        sb.Append("<div style='padding: 8px; background: #dcfce7; border: 1px solid #86efac; border-radius: 6px; color: #166534; font-size: 12px; font-weight: bold;'>✅ ĐÃ BÀN GIAO<br/><span style='font-size: 10px; font-weight: normal; color: #15803d;'>Hoàn tất sang tên viễn thông</span></div>");
        sb.Append($"<p style='margin: 8px 0 0 0; font-size: 12px; font-weight: bold;'>{sellerProfile?.FullName ?? seller?.UserName}</p>");
        sb.Append("</div>");

        sb.Append("<div style='border: 1px solid #cbd5e1; border-radius: 8px; padding: 12px; background: #fafafa;'>");
        sb.Append("<p style='font-weight: bold; margin: 0 0 4px 0; font-size: 13px;'>SÀN TRUSTSIM (BÊN C)</p>");
        sb.Append("<p style='font-size: 11px; color: #64748b; margin: 0 0 10px 0;'>Đơn vị trung gian tất toán</p>");
        sb.Append("<div style='padding: 8px; background: #dbeafe; border: 1px solid #93c5fd; border-radius: 6px; color: #1e40af; font-size: 12px; font-weight: bold;'>🔒 NIÊM PHONG PAdES<br/><span style='font-size: 10px; font-weight: normal; color: #1d4ed8;'>Giải ngân & Đóng giao dịch</span></div>");
        sb.Append("<p style='margin: 8px 0 0 0; font-size: 12px; font-weight: bold;'>TRUSTSIM PLATFORM</p>");
        sb.Append("</div>");

        sb.Append("</div>"); // đóng grid
        sb.Append("</div>"); // đóng container

        return sb.ToString();
    }

    public async Task<byte[]> GenerateLiquidationMinutePdfAsync(Guid tradeId, CancellationToken ct = default)
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

        var escrowAmount = escrow?.EscrowAmount ?? 0m;
        var platformFeePercent = 0.05m;
        var platformFee = Math.Round(escrowAmount * platformFeePercent, 2);
        var sellerPayout = escrowAmount - platformFee;

        var priceStr = escrowAmount.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ";
        var priceWords = VietnameseNumberToWordsHelper.ToVietnameseWords(escrowAmount);

        var feeStr = platformFee.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ";
        var feeWords = VietnameseNumberToWordsHelper.ToVietnameseWords(platformFee);

        var payoutStr = sellerPayout.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ";
        var payoutWords = VietnameseNumberToWordsHelper.ToVietnameseWords(sellerPayout);

        var phoneStr = sim?.PhoneNumber ?? "Chưa xác định";
        var carrierStr = sim?.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông Việt Nam";

        var sellerProfile = seller?.UserProfile;
        var buyerProfile = buyer?.UserProfile;

        using var memoryStream = new MemoryStream();
        var writer = new PdfWriter(memoryStream);
        var pdf = new PdfDocument(writer);
        var document = new Document(pdf, PageSize.A4);
        document.SetMargins(30, 36, 30, 36);

        var (regularFont, boldFont, italicFont) = ResolveVietnameseFonts();
        if (regularFont != null) document.SetFont(regularFont);

        // Header Quốc hiệu & Tiêu ngữ
        var pQuocHieu = new Paragraph("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(12);
        if (boldFont != null) pQuocHieu.SetFont(boldFont); else pQuocHieu.SetBold();
        document.Add(pQuocHieu);

        var pTieuNgu = new Paragraph("Độc lập - Tự do - Hạnh phúc\n---------------------------------")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(10.5f);
        if (boldFont != null) pTieuNgu.SetFont(boldFont); else pTieuNgu.SetBold();
        document.Add(pTieuNgu);

        // Tiêu đề
        var pTitle = new Paragraph("BIÊN BẢN NGHIỆM THU BÀN GIAO VÀ THANH LÝ HỢP ĐỒNG ĐIỆN TỬ")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(13)
            .SetFontColor(ColorConstants.DARK_GRAY)
            .SetMarginTop(10);
        if (boldFont != null) pTitle.SetFont(boldFont); else pTitle.SetBold();
        document.Add(pTitle);

        var pSubTitle = new Paragraph($"V/v: Nghiệm thu hoàn tất chuyển quyền sử dụng SIM và thanh lý Hợp đồng số: {trade.TradeTransactionCode}")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(8.5f)
            .SetFontColor(ColorConstants.DARK_GRAY);
        if (italicFont != null) pSubTitle.SetFont(italicFont); else pSubTitle.SetItalic();
        document.Add(pSubTitle);

        var pCode = new Paragraph($"Số BB: TL-HD/{trade.TradeTransactionCode} | Ngày lập: {DateTime.UtcNow:dd/MM/yyyy HH:mm} (UTC)")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(8.5f)
            .SetFontColor(ColorConstants.GRAY)
            .SetMarginBottom(10);
        if (italicFont != null) pCode.SetFont(italicFont); else pCode.SetItalic();
        document.Add(pCode);

        // Phần I: Các bên
        var pSec1 = new Paragraph("I. CÁC BÊN THAM GIA GIAO KẾT THANH LÝ").SetFontSize(10.5f);
        if (boldFont != null) pSec1.SetFont(boldFont); else pSec1.SetBold();
        document.Add(pSec1);

        document.Add(new Paragraph($"1. BÊN NHẬN QUYỀN (BÊN MUA / BÊN A): Ông/Bà {buyerProfile?.FullName ?? buyer?.UserName} | Số CCCD/Định danh: {FormatIdentity(buyerProfile?.IdentityNumber)} | SĐT: {(string.IsNullOrWhiteSpace(buyerProfile?.PhoneNumber) ? buyer?.PhoneNumber ?? "N/A" : buyerProfile.PhoneNumber)} | Email: {buyer?.Email ?? "N/A"}").SetFontSize(9));
        document.Add(new Paragraph($"2. BÊN CHUYỂN QUYỀN (BÊN BÁN / BÊN B): Ông/Bà {sellerProfile?.FullName ?? seller?.UserName} | Số CCCD/Định danh: {FormatIdentity(sellerProfile?.IdentityNumber)} | SĐT: {(string.IsNullOrWhiteSpace(sellerProfile?.PhoneNumber) ? seller?.PhoneNumber ?? "N/A" : sellerProfile.PhoneNumber)} | Email: {seller?.Email ?? "N/A"}").SetFontSize(9));
        document.Add(new Paragraph("3. ĐƠN VỊ TRUNG GIAN TÍN THÁC & NIÊM PHONG (BÊN C): CÔNG TY CỔ PHẦN CÔNG NGHỆ & ĐẤU GIÁ TRỰC TUYẾN TRUSTSIM | Đại diện: Ban Giám Đốc | Vai trò: Tất toán Quỹ Escrow, giải ngân tài chính và niêm phong số PAdES X.509.").SetFontSize(9));

        // Phần II: Điều khoản
        var pSec2 = new Paragraph("\nII. ĐIỀU KHOẢN NGHIỆM THU VÀ THANH LÝ").SetFontSize(10.5f);
        if (boldFont != null) pSec2.SetFont(boldFont); else pSec2.SetBold();
        document.Add(pSec2);

        document.Add(new Paragraph($"Điều 1. Nghiệm thu hoàn tất: Bên A xác nhận Bên B đã hoàn tất thủ tục sang tên đổi chủ số thuê bao {phoneStr} ({carrierStr}) tại nhà mạng theo Nghị định 49/2017/NĐ-CP. Bên A đã nhận bàn giao SIM chính chủ, sóng ổn định, tra cứu 1414 hiển thị đúng thông tin cá nhân và không có khiếu nại.").SetFontSize(9));
        document.Add(new Paragraph($"Điều 2. Quyết toán tài chính Escrow: Tổng giá trị hợp đồng: {priceStr} (Bằng chữ: {priceWords}). Phí sàn 5%: {feeStr} (Bằng chữ: {feeWords}). Số tiền thực nhận giải ngân cho Bên B: {payoutStr} (Bằng chữ: {payoutWords}). Bên C giải tỏa Quỹ Escrow và giải ngân vào ví Bên B.").SetFontSize(9));
        document.Add(new Paragraph($"Điều 3. Chấm dứt hiệu lực hợp đồng: Hai bên thống nhất chấm dứt toàn bộ các quyền và nghĩa vụ phát sinh từ Hợp đồng số {trade.TradeTransactionCode}. Các bên cam kết không còn bất kỳ khiếu nại hay tranh chấp tài chính nào.").SetFontSize(9));
        document.Add(new Paragraph("Điều 4. Giá trị pháp lý điện tử: Văn bản này được xác thực và niêm phong bằng Chữ ký số PAdES X.509 của Sàn TrustSim theo Luật Giao dịch điện tử số 20/2023/QH15, có giá trị pháp lý tương đương văn bản giấy gốc.").SetFontSize(9));

        // Phần III: Bảng xác nhận
        var pSec3 = new Paragraph("\nIII. XÁC NHẬN CÁC BÊN").SetFontSize(10.5f).SetMarginTop(10);
        if (boldFont != null) pSec3.SetFont(boldFont); else pSec3.SetBold();
        document.Add(pSec3);

        var table = new Table(UnitValue.CreatePercentArray(new float[] { 33, 33, 34 })).UseAllAvailableWidth();
        var cellAHeader = new Cell().Add(CreateCellPara("BÊN MUA (BÊN A)", boldFont, true)).SetBorder(Border.NO_BORDER);
        var cellBHeader = new Cell().Add(CreateCellPara("BÊN BÁN (BÊN B)", boldFont, true)).SetBorder(Border.NO_BORDER);
        var cellCHeader = new Cell().Add(CreateCellPara("SÀN TRUSTSIM (BÊN C)", boldFont, true)).SetBorder(Border.NO_BORDER);
        table.AddHeaderCell(cellAHeader);
        table.AddHeaderCell(cellBHeader);
        table.AddHeaderCell(cellCHeader);

        table.AddCell(new Cell().Add(CreateCellPara("(Đã nghiệm thu nhận SIM)", italicFont, false, true)).SetBorder(Border.NO_BORDER));
        table.AddCell(new Cell().Add(CreateCellPara("(Đã bàn giao chính chủ)", italicFont, false, true)).SetBorder(Border.NO_BORDER));
        table.AddCell(new Cell().Add(CreateCellPara("(Ký số PAdES giải ngân)", italicFont, false, true)).SetBorder(Border.NO_BORDER));

        document.Add(table);
        document.Close();

        return memoryStream.ToArray();
    }

    #endregion

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
                FormatIdentity(trade.Buyer?.UserProfile?.IdentityNumber),
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
                FormatIdentity(trade.Seller?.UserProfile?.IdentityNumber),
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
                FormatIdentity(trade.Seller?.UserProfile?.IdentityNumber),
                DateTime.UtcNow,
                "127.0.0.1 (System)",
                "POWER_OF_ATTORNEY_FORCED",
                true));
        }

        return list;
    }

    private static string FormatIdentity(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "Chưa cập nhật";
        return id.Trim();
    }

    private static (PdfFont? Regular, PdfFont? Bold, PdfFont? Italic) ResolveVietnameseFonts()
    {
        try
        {
            var regularCandidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "times.ttf"),
                @"C:\Windows\Fonts\times.ttf",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
                @"C:\Windows\Fonts\arial.ttf",
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf"
            };

            var boldCandidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "timesbd.ttf"),
                @"C:\Windows\Fonts\timesbd.ttf",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arialbd.ttf"),
                @"C:\Windows\Fonts\arialbd.ttf",
                "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"
            };

            var italicCandidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "timesi.ttf"),
                @"C:\Windows\Fonts\timesi.ttf",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "ariali.ttf"),
                @"C:\Windows\Fonts\ariali.ttf",
                "/usr/share/fonts/truetype/dejavu/DejaVuSans-Oblique.ttf"
            };

            string? regPath = regularCandidates.FirstOrDefault(p => File.Exists(p));
            string? boldPath = boldCandidates.FirstOrDefault(p => File.Exists(p));
            string? italicPath = italicCandidates.FirstOrDefault(p => File.Exists(p));

            PdfFont? regular = regPath != null ? PdfFontFactory.CreateFont(regPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED) : null;
            PdfFont? bold = boldPath != null ? PdfFontFactory.CreateFont(boldPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED) : regular;
            PdfFont? italic = italicPath != null ? PdfFontFactory.CreateFont(italicPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED) : regular;

            return (regular, bold, italic);
        }
        catch
        {
            return (null, null, null);
        }
    }

    private static Paragraph CreateCellPara(string text, PdfFont? font, bool isBold = false, bool isItalic = false)
    {
        var p = new Paragraph(text).SetTextAlignment(TextAlignment.CENTER).SetFontSize(9);
        if (font != null)
        {
            p.SetFont(font);
        }
        else
        {
            if (isBold) p.SetBold();
            if (isItalic) p.SetItalic();
        }
        return p;
    }
}
