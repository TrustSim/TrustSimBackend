namespace TrustSimBackend.Application.Features.Trade.DTOs.Responses;

/// <summary>
/// DTO thông tin một hình ảnh con thuộc bộ hồ sơ bằng chứng chuyển nhượng SIM.
/// </summary>
public record TransferProofImageDto(
    Guid Id,
    string ImageUrl,
    string? FileName,
    string? ContentType,
    long? FileSizeBytes,
    int SortOrder
);
