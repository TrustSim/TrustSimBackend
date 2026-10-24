using System.Linq.Expressions;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence.SortMappings;

public static class AiConsultantSortMappings
{
    public static readonly IReadOnlyDictionary<ConsultationSessionSortBy, Expression<Func<AiConsultationSession, object>>> SessionMappings =
        new Dictionary<ConsultationSessionSortBy, Expression<Func<AiConsultationSession, object>>>
        {
            [ConsultationSessionSortBy.CreatedDate] = s => s.CreatedDate,
            [ConsultationSessionSortBy.SessionTitle] = s => s.SessionTitle
        };

    public static readonly IReadOnlyDictionary<ConsultationMessageSortBy, Expression<Func<AiConsultationMessage, object>>> MessageMappings =
        new Dictionary<ConsultationMessageSortBy, Expression<Func<AiConsultationMessage, object>>>
        {
            [ConsultationMessageSortBy.CreatedDate] = m => m.CreatedDate
        };
}
