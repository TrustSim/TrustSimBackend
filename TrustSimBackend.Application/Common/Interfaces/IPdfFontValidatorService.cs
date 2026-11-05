using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ kiểm định cấu trúc tệp PDF, đảm bảo 100% font chữ được nhúng (Embedded)
/// và có bảng ánh xạ Unicode (/ToUnicode CMap) để hiển thị chính xác tiếng Việt trên mọi thiết bị.
/// </summary>
public interface IPdfFontValidatorService
{
    Task<PdfFontAuditResultDto> AuditPdfStreamAsync(Stream pdfStream, CancellationToken cancellationToken = default);
    Task EnsureCrossDeviceCompatibleAsync(Stream pdfStream, string fileName, CancellationToken cancellationToken = default);
}
