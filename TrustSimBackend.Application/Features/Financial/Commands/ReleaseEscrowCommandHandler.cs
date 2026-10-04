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
/// Handler xử lý ReleaseEscrowCommand (Giải ngân Escrow chuyển nhượng SIM -> Nợ 2030 / Có 2010 Seller + Có 4010 Sàn).
/// </summary>
public class ReleaseEscrowCommandHandler : IRequestHandler<ReleaseEscrowCommand, LedgerJournalDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly ILogger<ReleaseEscrowCommandHandler> _logger;

    public ReleaseEscrowCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        ILogger<ReleaseEscrowCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _logger = logger;
    }

    public async Task<LedgerJournalDto> Handle(ReleaseEscrowCommand request, CancellationToken cancellationToken)
    {
        if (request.TotalEscrowAmount <= 0)
        {
            throw new BadRequestException("Tổng giá trị ký quỹ Escrow giải ngân phải lớn hơn 0.");
        }

        if (request.PlatformFeePercent < 0 || request.PlatformFeePercent >= 1)
        {
            throw new BadRequestException("Tỷ lệ phí sàn không hợp lệ (phải từ 0% đến dưới 100%).");
        }

        var sellerWallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.SellerUserId, cancellationToken);
        if (sellerWallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người bán ID '{request.SellerUserId}'.");
        }

        if (sellerWallet.WalletStatus == WalletStatus.LOCKED)
        {
            throw new BadRequestException("Ví tiền người bán đang bị khóa kỷ luật (LOCKED), không thể nhận tiền giải ngân.");
        }

        var platformFeeAmount = Math.Round(request.TotalEscrowAmount * request.PlatformFeePercent, 2);
        var escrowId = request.EscrowId == default ? Guid.NewGuid() : request.EscrowId;

        // Ghi sổ cái kế toán kép 3 chiều cân bằng
        var journal = await _ledgerService.PostEscrowReleaseAsync(
            sellerWallet.Id,
            request.TotalEscrowAmount,
            platformFeeAmount,
            escrowId,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var netPayout = request.TotalEscrowAmount - platformFeeAmount;
        _logger.LogInformation(
            "[Escrow Release] Đã giải ngân EscrowId='{EscrowId}': Tổng Escrow={Total:N0} VNĐ -> Người bán nhận={Net:N0} VNĐ, Phí sàn={Fee:N0} VNĐ (Chứng từ #{JournalCode})",
            escrowId, request.TotalEscrowAmount, netPayout, platformFeeAmount, journal.JournalCode);

        return LedgerJournalDto.FromEntity(journal);
    }
}
