using TrustSimBackend.Domain.Entities.Dispute;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý Khiếu nại / Tranh chấp (DisputeTicket).
/// </summary>
public interface IDisputeTicketRepository
{
    Task<DisputeTicket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<DisputeTicket> Items, int TotalCount)> GetPagedAsync(
        Guid? userId,
        DisputeStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task AddAsync(DisputeTicket ticket, CancellationToken cancellationToken = default);
    void Update(DisputeTicket ticket);
}
