using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands;

/// <summary>
/// Handler xử lý HoldEscrowDepositCommand (Ký quỹ Escrow sau thắng đấu giá - Bút toán 3 dòng nguyên tử).
/// </summary>
public class HoldEscrowDepositCommandHandler : IRequestHandler<HoldEscrowDepositCommand, LedgerJournalDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly ILogger<HoldEscrowDepositCommandHandler> _logger;

    public HoldEscrowDepositCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        ILogger<HoldEscrowDepositCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _logger = logger;
    }

    public async Task<LedgerJournalDto> Handle(HoldEscrowDepositCommand request, CancellationToken cancellationToken)
    {
        if (request.TotalEscrowAmount <= 0)
        {
            throw new BadRequestException("Tổng giá trị ký quỹ Escrow phải lớn hơn 0.");
        }

        if (request.ExistingDepositAmount < 0 || request.ExistingDepositAmount > request.TotalEscrowAmount)
        {
            throw new BadRequestException("Số tiền cọc hiện có không hợp lệ (phải từ 0 đến Tổng giá trị Escrow).");
        }

        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.WinnerUserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người mua ID '{request.WinnerUserId}'.");
        }

        if (wallet.WalletStatus != WalletStatus.ACTIVE)
        {
            throw new BadRequestException("Ví tiền người mua đang bị khóa hoặc đóng băng, không thể thực hiện ký quỹ Escrow.");
        }

        // Kiểm tra tiền cọc đã khóa trong LockedBalance
        if (request.ExistingDepositAmount > 0 && wallet.LockedBalance < request.ExistingDepositAmount)
        {
            throw new BadRequestException(
                $"Số dư phong tỏa cọc ({wallet.LockedBalance:N0} VNĐ) không đủ để chuyển vào Escrow ({request.ExistingDepositAmount:N0} VNĐ).");
        }

        // Kiểm tra số dư khả dụng bổ sung còn thiếu
        var additionalAmount = request.TotalEscrowAmount - request.ExistingDepositAmount;
        if (additionalAmount > 0 && wallet.AvailableBalance < additionalAmount)
        {
            throw new BadRequestException(
                $"Số dư khả dụng ({wallet.AvailableBalance:N0} VNĐ) không đủ để bù số tiền còn thiếu ({additionalAmount:N0} VNĐ) vào quỹ Escrow.");
        }

        // Ghi sổ cái kế toán kép 3 dòng nguyên tử
        var journal = await _ledgerService.PostEscrowHoldAsync(
            wallet.Id,
            request.TotalEscrowAmount,
            request.ExistingDepositAmount,
            request.EscrowId,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[Escrow Hold] Đã ký quỹ Escrow thành công: EscrowId='{EscrowId}', BuyerId='{BuyerId}', Tổng Escrow={Total:N0} VNĐ (Cọc: {Deposit:N0} VNĐ + Bổ sung: {Add:N0} VNĐ, Chứng từ #{JournalCode})",
            request.EscrowId, request.WinnerUserId, request.TotalEscrowAmount, request.ExistingDepositAmount, additionalAmount, journal.JournalCode);

        return LedgerJournalDto.FromEntity(journal);
    }
}
