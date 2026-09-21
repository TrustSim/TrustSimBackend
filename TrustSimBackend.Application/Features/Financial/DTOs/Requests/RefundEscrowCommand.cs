using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command xử lý Nghiệp vụ 10: Hủy giao dịch SIM hoặc Khiếu nại thành công -> Hoàn trả 100% tiền ký quỹ Escrow về ví người mua.
/// Ghi nhận Sổ cái kép:
/// - Nợ 2030 PLATFORM_ESCROW: Giải phóng toàn bộ quỹ ký quỹ đã giữ (E)
/// - Có 2010 USER_AVAILABLE [Buyer]: Hoàn 100% tiền về ví khả dụng của người mua (E)
/// </summary>
public record RefundEscrowCommand(
    Guid BuyerUserId,
    decimal TotalEscrowAmount,
    Guid EscrowId,
    string? RefundReason = null
) : IRequest<LedgerJournalDto>;
