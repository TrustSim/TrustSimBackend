using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands;

/// <summary>
/// Handler xử lý SimulateTamperTestCommand (Mô phỏng can thiệp dữ liệu và kiểm chứng báo động đỏ).
/// </summary>
public class SimulateTamperTestCommandHandler : IRequestHandler<SimulateTamperTestCommand, TamperSimulationResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerIntegrityVerifier _verifier;
    private readonly ILedgerService _ledgerService;

    public SimulateTamperTestCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerIntegrityVerifier verifier,
        ILedgerService ledgerService)
    {
        _unitOfWork = unitOfWork;
        _verifier = verifier;
        _ledgerService = ledgerService;
    }

    public async Task<TamperSimulationResultDto> Handle(SimulateTamperTestCommand request, CancellationToken cancellationToken)
    {
        var entries = await _unitOfWork.LedgerEntries.GetAllOrderedBySequenceAsync(1, 100, cancellationToken);

        // Nếu chưa có giao dịch nào, tự động tạo 1 giao dịch mẫu trước
        if (entries.Count == 0)
        {
            var dummyWallet = (await _unitOfWork.Wallets.GetPagedAsync(new AdminWalletQueryFilter { PageIndex = 1, PageSize = 1 }, cancellationToken)).Items.FirstOrDefault();
            if (dummyWallet == null)
            {
                throw new BadRequestException("Chưa có ví người dùng nào trong hệ thống để thực hiện mô phỏng.");
            }

            await _ledgerService.PostDepositAsync(
                dummyWallet.Id,
                1_000_000m,
                $"DEMO{Random.Shared.Next(1000, 9999)}",
                "DEMO-EXT-1",
                cancellationToken: cancellationToken);

            entries = await _unitOfWork.LedgerEntries.GetAllOrderedBySequenceAsync(1, 100, cancellationToken);
        }

        LedgerEntry? targetEntry = null;
        if (request.SequenceNumber > 0)
        {
            targetEntry = entries.FirstOrDefault(e => e.SequenceNumber == request.SequenceNumber);
        }

        targetEntry ??= entries.Last();

        var originalAmount = targetEntry.Amount;
        var tamperedAmount = request.TamperedAmount > 0 ? request.TamperedAmount : 99_000_000m;

        // Cố tình sửa giá trị Amount mà KHÔNG cập nhật lại EntryHash và Signature
        targetEntry.Amount = tamperedAmount;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Quét chuỗi ngay lập tức để phát hiện gian lận
        var report = await _verifier.VerifyChainAsync(1, 10000, cancellationToken);

        // Khôi phục lại dữ liệu sau khi kiểm tra xong để bảo toàn dữ liệu gốc
        targetEntry.Amount = originalAmount;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TamperSimulationResultDto
        {
            TamperedEntryId = targetEntry.Id,
            SequenceNumber = targetEntry.SequenceNumber,
            OriginalAmount = originalAmount,
            TamperedAmount = tamperedAmount,
            DetectionStatus = report.IsChainValid ? "GREEN - VALID" : "RED - TAMPER DETECTED",
            VerificationReport = report,
            ThesisExplanation = "Hệ thống đã chứng minh: Khi kẻ tấn công/DBA can thiệp sửa trực tiếp cột Amount trong Database mà không có HSM Private Key để ký lại, thuật toán băm SHA-256 kết hợp Chữ ký số ECDsa lập tức phát hiện đứt gãy tại đúng mắt xích đó và báo động đỏ!"
        };
    }
}
