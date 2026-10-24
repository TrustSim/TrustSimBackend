using System.Linq.Expressions;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence.SortMappings;

public static class SimValuationSortMappings
{
    public static readonly IReadOnlyDictionary<SimValuationSortBy, Expression<Func<SimValuation, object>>> ValuationMappings =
        new Dictionary<SimValuationSortBy, Expression<Func<SimValuation, object>>>
        {
            [SimValuationSortBy.CreatedDate] = v => v.CreatedDate,
            [SimValuationSortBy.SuggestedPrice] = v => v.SuggestedPrice,
            [SimValuationSortBy.ConfidenceScore] = v => v.ConfidenceScore
        };
}
