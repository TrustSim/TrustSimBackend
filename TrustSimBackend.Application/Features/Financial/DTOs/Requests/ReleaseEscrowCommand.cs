using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command xử lý Nghiệp vụ 9: Giải ngân Escrow sau khi hoàn tất chuyển quyền sở hữu SIM (Nợ 2030 / Có 2010 Seller + Có 4010 Sàn).
/// Ghi nhận Sổ cái kép:
/// - Nợ 2030 PLATFORM_ESCROW: Toàn bộ số tiền ký quỹ đã giữ (E)
/// - Có 2010 USER_AVAILABLE [Seller]: Tiền thực nhận của người bán (E - Phí sàn)
/// - Có 4010 PLATFORM_FEE_REVENUE: Doanh thu phí dịch vụ sàn TrustSim (Phí sàn)
/// </summary>
public record ReleaseEscrowCommand(
    Guid SellerUserId,
    decimal TotalEscrowAmount,
    decimal PlatformFeePercent = 0.05m,
    Guid EscrowId = default,
    string? Description = null
) : IRequest<LedgerJournalDto>;
