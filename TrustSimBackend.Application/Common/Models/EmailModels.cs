namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// DTO chứa nội dung cấu trúc của một email gửi đi.
/// </summary>
public class EmailMessage
{
    /// <summary>Danh sách địa chỉ email người nhận chính (To).</summary>
    public List<string> To { get; set; } = new();

    /// <summary>Danh sách địa chỉ email nhận bản sao (Cc).</summary>
    public List<string> Cc { get; set; } = new();

    /// <summary>Danh sách địa chỉ email nhận bản sao ẩn (Bcc).</summary>
    public List<string> Bcc { get; set; } = new();

    /// <summary>Tiêu đề email.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Nội dung email định dạng HTML.</summary>
    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>Nội dung email định dạng văn bản thuần (PlainText fallback).</summary>
    public string? PlainTextBody { get; set; }

    /// <summary>Danh sách file đính kèm (nếu có).</summary>
    public List<EmailAttachment>? Attachments { get; set; }
}

/// <summary>
/// Đại diện cho một file đính kèm trong email.
/// </summary>
public class EmailAttachment
{
    /// <summary>Tên file hiển thị (kèm phần mở rộng, ví dụ report.pdf).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Dữ liệu nhị phân của file.</summary>
    public byte[] Content { get; set; } = Array.Empty<byte>();

    /// <summary>MIME Type của file (mặc định application/octet-stream).</summary>
    public string ContentType { get; set; } = "application/octet-stream";
}
