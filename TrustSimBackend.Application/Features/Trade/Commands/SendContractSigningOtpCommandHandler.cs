using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

/// <summary>
/// Handler xử lý gửi mã OTP ký hợp đồng qua Email cho Bên Mua hoặc Bên Bán.
/// </summary>
public class SendContractSigningOtpCommandHandler : IRequestHandler<SendContractSigningOtpCommand, SendOtpResponseDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOtpService _otpService;
    private readonly ILogger<SendContractSigningOtpCommandHandler> _logger;

    public SendContractSigningOtpCommandHandler(
        IUnitOfWork unitOfWork,
        IOtpService otpService,
        ILogger<SendContractSigningOtpCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _otpService = otpService;
        _logger = logger;
    }

    public async Task<SendOtpResponseDto> Handle(SendContractSigningOtpCommand request, CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{request.TradeId}'.");
        }

        if (trade.TradeTransactionStatus != TradeTransactionStatus.PendingSignature)
        {
            throw new BadRequestException("Giao dịch không ở trạng thái chờ ký hợp đồng.");
        }

        var isBuyer = trade.BuyerId == request.UserId;
        var isSeller = trade.SellerId == request.UserId;

        if (!isBuyer && !isSeller)
        {
            throw new ForbiddenAccessException("Bạn không phải là Bên Mua hoặc Bên Bán của giao dịch này.");
        }

        var user = isBuyer ? trade.Buyer : trade.Seller;
        if (user == null || string.IsNullOrWhiteSpace(user.Email))
        {
            throw new BadRequestException("Không tìm thấy địa chỉ email hợp lệ của tài khoản để gửi mã OTP.");
        }

        var existingSignature = trade.TransferContract?.DigitalSignatures
            .FirstOrDefault(s => s.SignatureRole == (isBuyer ? SignatureRole.BUYER : SignatureRole.SELLER));

        if (existingSignature != null)
        {
            throw new BadRequestException("Bạn đã ký xác nhận hợp đồng này rồi, không cần gửi lại OTP.");
        }

        _logger.LogInformation(
            "[ContractOtp] Gửi mã OTP ký hợp đồng giao dịch {TradeCode} tới email {Email} (Role: {Role})",
            trade.TradeTransactionCode, user.Email, isBuyer ? "BUYER" : "SELLER");

        var response = await _otpService.GenerateAndSendEmailOtpAsync(
            user.Email,
            OtpPurpose.ContractSigning.ToString(),
            expiryMinutes: 5,
            cancellationToken: cancellationToken);

        return response;
    }
}
