using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Sim.DTOs;

/// <summary>
/// DTO tóm tắt thông tin một dòng SIM trong danh sách phân trang (User & Admin).
/// </summary>
public class SimListItemDto
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public SimStatus SimStatus { get; set; }
    public string SimStatusName => SimStatus.ToString();

    // Nhà mạng & Đầu số
    public string CarrierName { get; set; } = string.Empty;
    public string CarrierCode { get; set; } = string.Empty;
    public string CarrierLogo { get; set; } = string.Empty;
    public string PrefixValue { get; set; } = string.Empty;

    // Nhịp định dạng
    public string FormatRhythmName { get; set; } = string.Empty;

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

/// <summary>
/// DTO chi tiết đầy đủ thông tin một số SIM.
/// </summary>
public class SimDetailDto
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public SimStatus SimStatus { get; set; }
    public string SimStatusName => SimStatus.ToString();

    // Chi tiết Nhà mạng & Đầu số
    public Guid PhonePrefixId { get; set; }
    public string PrefixValue { get; set; } = string.Empty;
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string CarrierCode { get; set; } = string.Empty;
    public string CarrierLogo { get; set; } = string.Empty;

    // Chi tiết Nhịp định dạng
    public Guid FormatRhythmId { get; set; }
    public string FormatRhythmName { get; set; } = string.Empty;
    public string FormatRhythmValue { get; set; } = string.Empty;

    // Chi tiết Danh mục & Hệ số định giá
    public List<SimCategoryDto> Categories { get; set; } = new();

    // Thông tin người sở hữu hiện tại
    public Guid? OwnerId { get; set; }
    public string? OwnerUserName { get; set; }
    public string? OwnerFullName { get; set; }
    public string? OwnerEmail { get; set; }
    public string? OwnerPhoneNumber { get; set; }

    // Thông tin trạng thái sở hữu & xác minh
    public OwnershipStatus? OwnershipStatus { get; set; }
    public VerificationStatus? VerificationStatus { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
