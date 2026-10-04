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
/// Handler xử lý nghiệp vụ Admin rút tiền lợi nhuận sàn về tài khoản cá nhân.
/// Kiểm tra nghiêm ngặt số dư lợi nhuận khả dụng, ghi sổ cái kép Nợ 4010/4020 / Có 1010 và kích hoạt chi trả ngân hàng.
/// </summary>
public class AdminWithdrawProfitCommandHandler : IRequestHandler<AdminWithdrawProfitCommand, AdminProfitWithdrawResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly IMockBankPayoutGatewayService _mockBankPayoutGatewayService;
    private readonly ILogger<AdminWithdrawProfitCommandHandler> _logger;

    public AdminWithdrawProfitCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        IMockBankPayoutGatewayService mockBankPayoutGatewayService,
        ILogger<AdminWithdrawProfitCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _mockBankPayoutGatewayService = mockBankPayoutGatewayService;
        _logger = logger;
    }

    public async Task<AdminProfitWithdrawResultDto> Handle(AdminWithdrawProfitCommand request, CancellationToken cancellationToken)
    {
        var adminUser = await _unitOfWork.Users.GetByIdAsync(request.AdminUserId, cancellationToken);
        if (adminUser == null)
        {
            throw new NotFoundException($"Không tìm thấy tài khoản Admin với ID '{request.AdminUserId}'.");
        }

        // 1. Tính toán số dư các tài khoản từ Sổ cái kế toán ghi kép
        var allEntries = await _unitOfWork.LedgerEntries.GetAllOrderedBySequenceAsync(1, 100000, cancellationToken);

        decimal totalFeeRevenue = 0;
        decimal totalPenaltyRevenue = 0;
        decimal totalBankBalance = 0;

        foreach (var entry in allEntries)
        {
            var delta = entry.EntryType == LedgerEntryType.CREDIT ? entry.Amount : -entry.Amount;

            switch (entry.AccountType)
            {
                case FinancialAccountType.EXTERNAL_GATEWAY_CLEARING:
                    totalBankBalance += entry.EntryType == LedgerEntryType.DEBIT ? entry.Amount : -entry.Amount;
                    break;

                case FinancialAccountType.PLATFORM_FEE_REVENUE:
                    totalFeeRevenue += delta;
                    break;

                case FinancialAccountType.PLATFORM_PENALTY_REVENUE:
                    totalPenaltyRevenue += delta;
                    break;
            }
        }

        var availableProfit = totalFeeRevenue + totalPenaltyRevenue;

        // 2. Kiểm tra hạn mức rút tiền
        if (request.Amount > availableProfit)
        {
            throw new BadRequestException(
                $"Số tiền yêu cầu rút ({request.Amount:N0} VNĐ) vượt quá tổng lợi nhuận khả dụng của sàn ({availableProfit:N0} VNĐ).");
        }

        if (request.Amount > totalBankBalance)
        {
            throw new BadRequestException(
                $"Số dư thực tế trong tài khoản ngân hàng sàn ({totalBankBalance:N0} VNĐ) không đủ để thực hiện giao dịch {request.Amount:N0} VNĐ.");
        }

        // 3. Quyết định trừ từ tài khoản Hoa hồng (4010) hay Tiền phạt (4020)
        var sourceAccount = totalFeeRevenue >= request.Amount
            ? FinancialAccountType.PLATFORM_FEE_REVENUE
            : FinancialAccountType.PLATFORM_PENALTY_REVENUE;

        var transactionCode = $"ADM{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000, 999999)}";
        var accNumber = request.DestinationAccountNumber.Trim();
        var maskedAccount = accNumber.Length > 4
            ? new string('*', accNumber.Length - 4) + accNumber[^4..]
            : new string('*', accNumber.Length);

        var destinationInfo = $"{request.DestinationBankName.Trim()} ({maskedAccount}) - {request.DestinationAccountHolderName.Trim().ToUpperInvariant()}";

        // 4. Ghi Sổ cái Kế toán kép Bất biến (Chứng từ ADMIN_PROFIT_WITHDRAW)
        var journal = await _ledgerService.PostAdminProfitWithdrawAsync(
            request.Amount,
            transactionCode,
            destinationInfo,
            sourceAccount,
            request.IdempotencyKey,
            cancellationToken);

        // 5. Kích hoạt chi hộ ngân hàng Sandbox Mock Bank Payout
        var externalPayoutRef = await _mockBankPayoutGatewayService.RequestInstantPayoutAsync(
            request.DestinationBankCode.Trim(),
            accNumber,
            request.DestinationAccountHolderName.Trim(),
            request.Amount,
            transactionCode,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var remainingProfit = availableProfit - request.Amount;
        _logger.LogInformation(
            "[Admin Profit Withdraw] Admin '{AdminEmail}' đã rút THÀNH CÔNG {Amount:N0} VNĐ về {Dest}. Lợi nhuận sàn còn lại: {Remaining:N0} VNĐ (Chứng từ #{JournalCode})",
            adminUser.Email, request.Amount, destinationInfo, remainingProfit, journal.JournalCode);

        return new AdminProfitWithdrawResultDto
        {
            JournalId = journal.Id,
            JournalCode = journal.JournalCode,
            TransactionCode = transactionCode,
            Amount = request.Amount,
            DestinationBankName = request.DestinationBankName.Trim(),
            DestinationAccountNumberMasked = maskedAccount,
            DestinationAccountHolderName = request.DestinationAccountHolderName.Trim().ToUpperInvariant(),
            ExternalPayoutRef = externalPayoutRef,
            Status = "COMPLETED",
            RemainingAvailableProfit = remainingProfit,
            CompletedAt = DateTime.UtcNow
        };
    }
}
