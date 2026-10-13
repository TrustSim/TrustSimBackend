namespace TrustSimBackend.Application.Features.Trade.DTOs.Responses;

/// <summary>
/// Kết quả kiểm định tính toàn vẹn và niêm phong mật mã của hợp đồng PDF.
/// </summary>
public record VerifyContractIntegrityResponse(
    bool IsValid,
    string DocumentStatus,
    string? ExpectedDocumentHash,
    string? ProvidedSha256,
    bool HashMatched,
    string? PadesSignatureSerial,
    string? PadesSignatureHash,
    DateTime? TsaTimestamp,
    string? TsaAuthority,
    DateTime? SealedAt,
    bool IsAutoEnforced,
    int TotalSignatures,
    string Message
);
