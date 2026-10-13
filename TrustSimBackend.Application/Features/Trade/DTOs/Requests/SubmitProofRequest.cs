namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Yêu cầu nộp ảnh bằng chứng sang tên SIM từ nhà mạng.
/// </summary>
public record SubmitProofRequest(string ImageUrl, string? Description);
