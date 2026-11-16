namespace TrustSimBackend.Application.Features.Trade.DTOs.Responses;

/// <summary>
/// DTO thông tin một bộ hồ sơ bằng chứng chuyển nhượng SIM (1 TransferProof chứa nhiều hình ảnh con).
/// </summary>
public record TransferProofDto(
    Guid Id,
    Guid AuctionId,
    Guid? SimTransferId,
    string ProofType,
    string ProofStatus,
    string? Description,
    string? RejectionReason,
    Guid SubmittedByUserId,
    Guid? VerifiedByAdminId,
    DateTime? VerifiedAt,
    DateTime? WithdrawnAt,
    DateTime CreatedDate,
    IReadOnlyList<TransferProofImageDto> Images
);
