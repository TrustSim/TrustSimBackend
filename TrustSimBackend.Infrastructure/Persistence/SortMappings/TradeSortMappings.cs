using System.Linq.Expressions;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence.SortMappings;

/// <summary>
/// Cấu hình Whitelist Mapping sắp xếp cho các truy vấn Giao dịch Chuyển nhượng (TradeTransaction).
/// </summary>
public static class TradeSortMappings
{
    public static readonly IReadOnlyDictionary<TradeTransactionSortBy, Expression<Func<TradeTransaction, object>>> Mappings =
        new Dictionary<TradeTransactionSortBy, Expression<Func<TradeTransaction, object>>>
        {
            [TradeTransactionSortBy.CreatedDate] = t => t.CreatedDate,
            [TradeTransactionSortBy.EscrowAmount] = t => t.Escrow != null ? t.Escrow.EscrowAmount : 0m,
            [TradeTransactionSortBy.Status] = t => t.TradeTransactionStatus,
            [TradeTransactionSortBy.TradeTransactionCode] = t => t.TradeTransactionCode
        };
}
