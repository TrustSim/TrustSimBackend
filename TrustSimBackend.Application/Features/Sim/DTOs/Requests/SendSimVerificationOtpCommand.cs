using MediatR;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

/// <summary>
/// Command gửi mã OTP xác thực qua SMS tới số SIM để chứng minh người dùng đang cầm giữ SIM vật lý (Pha 1 - Proof of Possession).
/// </summary>
public record SendSimVerificationOtpCommand(string PhoneNumber, Guid UserId) : IRequest<SendOtpResponseDto>;
