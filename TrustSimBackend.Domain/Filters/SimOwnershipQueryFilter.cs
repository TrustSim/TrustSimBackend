using System;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc tra cứu danh sách quyền sở hữu SIM của người dùng (my-ownerships).
/// </summary>
public class SimOwnershipQueryFilter : BaseQueryFilter<SimOwnershipSortBy>
{
    /// <summary>
    /// Trạng thái quyền sở hữu (ACTIVE, REVOKED, TRANSFERRED...).
    /// </summary>
    public OwnershipStatus? Status { get; set; }

    /// <summary>
    /// ID của số SIM cụ thể (SimNumberId).
    /// </summary>
    public Guid? SimId { get; set; }
}
