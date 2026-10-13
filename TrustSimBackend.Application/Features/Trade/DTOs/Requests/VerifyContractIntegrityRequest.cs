namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Yêu cầu kiểm tra tính toàn vẹn hợp đồng với mã SHA-256 do client tính từ tệp PDF.
/// </summary>
public record VerifyContractIntegrityRequest(string? ClientSha256);
