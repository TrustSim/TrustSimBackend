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
/// Handler xử lý RefundEscrowCommand (Hủy giao dịch SIM -> Hoàn 100% Escrow về Buyer -> Nợ 2030 / Có 2010).
/// </summary>
public class RefundEscrowCommandHandler : IRequestHandler<RefundEscrowCommand, LedgerJournalDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly ILogger<RefundEscrowCommandHandler> _logger;

    public RefundEscrowCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        ILogger<RefundEscrowCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _logger = logger;
    }

    public async Task<LedgerJournalDto> Handle(RefundEscrowCommand request, CancellationToken cancellationToken)
    {
        if (request.TotalEscrowAmount <= 0)
        {
            throw new BadRequestException("Số tiền hoàn trả Escrow phải lớn hơn 0.");
        }

        var buyerWallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.BuyerUserId, cancellationToken);
        if (buyerWallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người mua ID '{request.BuyerUserId}'.");
        }

        if (buyerWallet.WalletStatus == WalletStatus.LOCKED)
        {
            throw new BadRequestException("Ví tiền người mua đang bị khóa toàn phần kỷ luật, không thể nhận tiền hoàn.");
        }

        var reason = request.RefundReason ?? "Hủy giao dịch chuyển nhượng SIM";

        // Ghi sổ cái kép hoàn trả 100% Escrow về tài khoản khả dụng người mua
        var journal = await _ledgerService.PostEscrowRefundAsync(
            buyerWallet.Id,
            request.TotalEscrowAmount,
            request.EscrowId,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[Escrow Refund] Đã hoàn trả {Amount:N0} VNĐ từ EscrowId='{EscrowId}' về BuyerId='{BuyerId}', Lý do='{Reason}' (Chứng từ #{JournalCode})",
            request.TotalEscrowAmount, request.EscrowId, request.BuyerUserId, reason, journal.JournalCode);

        return LedgerJournalDto.FromEntity(journal);
    }
}
