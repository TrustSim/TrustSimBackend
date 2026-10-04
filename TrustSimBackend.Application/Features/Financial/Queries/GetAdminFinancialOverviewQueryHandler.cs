using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries;

/// <summary>
/// Handler xử lý truy vấn báo cáo tổng quan tài chính sàn dành cho Admin Dashboard.
/// </summary>
public class GetAdminFinancialOverviewQueryHandler : IRequestHandler<GetAdminFinancialOverviewQuery, AdminFinancialOverviewDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerIntegrityVerifier _ledgerIntegrityVerifier;

    public GetAdminFinancialOverviewQueryHandler(
        IUnitOfWork unitOfWork,
        ILedgerIntegrityVerifier ledgerIntegrityVerifier)
    {
        _unitOfWork = unitOfWork;
        _ledgerIntegrityVerifier = ledgerIntegrityVerifier;
    }

    public async Task<AdminFinancialOverviewDto> Handle(GetAdminFinancialOverviewQuery request, CancellationToken cancellationToken)
    {
        var allEntries = await _unitOfWork.LedgerEntries.GetAllOrderedBySequenceAsync(1, 100000, cancellationToken);

        decimal totalBankBalance = 0;
        decimal userAvailable = 0;
        decimal userLocked = 0;
        decimal platformEscrow = 0;

        decimal totalFeeRevenueCollected = 0;
        decimal totalPenaltyRevenueCollected = 0;
        decimal totalProfitWithdrawn = 0;

        foreach (var entry in allEntries)
        {
            var delta = entry.EntryType == LedgerEntryType.CREDIT ? entry.Amount : -entry.Amount;

            switch (entry.AccountType)
            {
                case FinancialAccountType.EXTERNAL_GATEWAY_CLEARING:
                    totalBankBalance += entry.EntryType == LedgerEntryType.DEBIT ? entry.Amount : -entry.Amount;
                    break;

                case FinancialAccountType.USER_AVAILABLE:
                    userAvailable += delta;
                    break;

                case FinancialAccountType.USER_LOCKED:
                    userLocked += delta;
                    break;

                case FinancialAccountType.PLATFORM_ESCROW:
                    platformEscrow += delta;
                    break;

                case FinancialAccountType.PLATFORM_FEE_REVENUE:
                    if (entry.EntryType == LedgerEntryType.CREDIT)
                    {
                        totalFeeRevenueCollected += entry.Amount;
                    }
                    else if (entry.EntryType == LedgerEntryType.DEBIT)
                    {
                        totalProfitWithdrawn += entry.Amount;
                    }
                    break;

                case FinancialAccountType.PLATFORM_PENALTY_REVENUE:
                    if (entry.EntryType == LedgerEntryType.CREDIT)
                    {
                        totalPenaltyRevenueCollected += entry.Amount;
                    }
                    else if (entry.EntryType == LedgerEntryType.DEBIT)
                    {
                        totalProfitWithdrawn += entry.Amount;
                    }
                    break;
            }
        }

        var totalCustomerFunds = userAvailable + userLocked + platformEscrow;
        var totalGrossRevenue = totalFeeRevenueCollected + totalPenaltyRevenueCollected;
        var availableProfit = totalGrossRevenue - totalProfitWithdrawn;

        // Kiểm tra nhanh tính toàn vẹn chuỗi khối
        var chainVerifyResult = await _ledgerIntegrityVerifier.VerifyChainAsync(1, 10000, cancellationToken);

        // Phương trình kế toán: Tiền Bank == Tiền nợ khách hàng + Lợi nhuận sàn còn lại
        var isBalanced = Math.Abs(totalBankBalance - (totalCustomerFunds + availableProfit)) < 0.01m;

        return new AdminFinancialOverviewDto
        {
            TotalBankBalance = totalBankBalance,
            UserObligations = new CustomerFundsBreakdownDto
            {
                UserAvailableBalance = userAvailable,
                UserLockedBalance = userLocked,
                PlatformEscrowBalance = platformEscrow,
                TotalCustomerFunds = totalCustomerFunds
            },
            PlatformRevenue = new PlatformRevenueBreakdownDto
            {
                TotalFeeRevenueCollected = totalFeeRevenueCollected,
                TotalPenaltyRevenueCollected = totalPenaltyRevenueCollected,
                TotalGrossRevenue = totalGrossRevenue,
                TotalProfitWithdrawn = totalProfitWithdrawn,
                AvailableProfitForWithdrawal = availableProfit
            },
            Integrity = new FinancialIntegritySummaryDto
            {
                IsAccountingEquationBalanced = isBalanced,
                IsChainValid = chainVerifyResult.IsChainValid && chainVerifyResult.IsSignatureValid,
                LastAuditedAt = DateTime.UtcNow
            }
        };
    }
}
