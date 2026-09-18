using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Responses;

/// <summary>
/// DTO tóm tắt thông tin một dòng SIM trong danh sách phân trang (User & Admin).
/// </summary>
public class SimListItemDto
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public SimStatus SimStatus { get; set; }
    public string SimStatusName => SimStatus.ToString();
    public decimal Price { get; set; } = 0m;

    // Nhà mạng & Đầu số
    public string CarrierName { get; set; } = string.Empty;
    public string CarrierCode { get; set; } = string.Empty;
    public string CarrierLogo { get; set; } = string.Empty;
    public string PrefixValue { get; set; } = string.Empty;

    // Nhịp định dạng
    public Guid? FormatRhythmId { get; set; }
    public string? FormatRhythmName { get; set; }

    // Danh mục
    public List<string> CategoryNames { get; set; } = new();

    // Người sở hữu hiện tại
    public Guid? OwnerId { get; set; }
    public string? OwnerFullName { get; set; }
    public string? OwnerPhoneNumber { get; set; }

    // Trạng thái xác minh
    public VerificationStatus? VerificationStatus { get; set; }
    public string? VerificationStatusName => VerificationStatus?.ToString();

    public DateTime CreatedDate { get; set; }
}
