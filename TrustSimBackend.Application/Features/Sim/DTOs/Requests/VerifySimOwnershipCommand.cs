using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

/// <summary>
/// Command xác thực mã OTP và đối soát thông tin với Cổng viễn thông nhà mạng giả lập (Pha 2 - Proof of Legal Ownership).
/// </summary>
public record VerifySimOwnershipCommand(string PhoneNumber, string OtpCode, Guid UserId) : IRequest<SimOwnershipVerificationResultDto>;
