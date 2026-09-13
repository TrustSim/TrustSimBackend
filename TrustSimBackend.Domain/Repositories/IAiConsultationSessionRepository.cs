using TrustSimBackend.Domain.Entities.Ai;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý Phiên tư vấn và Tin nhắn AI (AiConsultationSession & AiConsultationMessage)
/// </summary>
public interface IAiConsultationSessionRepository
{
    Task<AiConsultationSession?> GetByIdAsync(Guid id, bool includeMessages = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiConsultationSession>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddSessionAsync(AiConsultationSession session, CancellationToken cancellationToken = default);
    Task AddMessageAsync(AiConsultationMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiConsultationMessage>> GetRecentMessagesAsync(Guid sessionId, int count = 10, CancellationToken cancellationToken = default);
    void UpdateSession(AiConsultationSession session);
}
