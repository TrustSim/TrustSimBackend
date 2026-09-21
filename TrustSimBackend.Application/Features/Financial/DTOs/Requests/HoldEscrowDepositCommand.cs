using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command xử lý Nghiệp vụ 8: Ký quỹ Escrow sau khi thắng đấu giá (Bút toán 3 dòng nguyên tử).
/// Sổ cái ghi nhận:
/// - Nợ 2020 USER_LOCKED [Winner]: Tiền cọc đã khóa trước đó (D)
/// - Nợ 2010 USER_AVAILABLE [Winner]: Tiền còn thiếu cần thanh toán (E - D)
/// - Có 2030 PLATFORM_ESCROW: Toàn bộ số tiền bảo đảm chuyển nhượng SIM (E)
/// </summary>
public record HoldEscrowDepositCommand(
    Guid WinnerUserId,
    decimal TotalEscrowAmount,
    decimal ExistingDepositAmount,
    Guid EscrowId,
    string? Description = null
) : IRequest<LedgerJournalDto>;
