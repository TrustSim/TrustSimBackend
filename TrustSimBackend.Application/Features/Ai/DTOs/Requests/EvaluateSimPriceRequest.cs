namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public class EvaluateSimPriceRequest
{
    /// <summary>ID của số SIM đã có trong hệ thống</summary>
    public Guid? SimNumberId { get; set; }

    /// <summary>Hoặc số điện thoại cần định giá trực tiếp (nếu chưa lưu trong DB)</summary>
    public string? PhoneNumber { get; set; }
}
