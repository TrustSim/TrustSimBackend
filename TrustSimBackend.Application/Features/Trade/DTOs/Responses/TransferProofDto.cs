namespace TrustSimBackend.Application.Features.Trade.DTOs.Responses;

/// <summary>
/// DTO thông tin một ảnh bằng chứng nộp biên bản sang tên SIM của nhà mạng hoặc nghiệm thu của người mua.
/// </summary>
public record TransferProofDto(
    Guid Id,
    string ImageUrl,
    string? Description,
    string ProofStatus,
    DateTime VerifiedAt,
    DateTime CreatedDate,
    string ProofType = "SELLER_HANDOVER"
);
