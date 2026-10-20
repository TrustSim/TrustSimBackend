using System.Linq.Expressions;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence.SortMappings;

/// <summary>
/// Cấu hình Whitelist Mapping sắp xếp cho các truy vấn Quản lý SIM số (SimNumber, SimOwnership).
/// </summary>
public static class SimSortMappings
{
    /// <summary>
    /// Sắp xếp cho Chợ SIM công khai (SimNumber).
    /// </summary>
    public static readonly IReadOnlyDictionary<SimSortBy, Expression<Func<SimNumber, object>>> PublicSimMappings =
        new Dictionary<SimSortBy, Expression<Func<SimNumber, object>>>
        {
            [SimSortBy.CreatedDate] = s => s.CreatedDate,
            [SimSortBy.Price] = s => s.Price,
            [SimSortBy.PhoneNumber] = s => s.PhoneNumber
        };

    /// <summary>
    /// Sắp xếp cho Quản trị viên tra cứu toàn bộ kho SIM (SimNumber).
    /// </summary>
    public static readonly IReadOnlyDictionary<SimSortBy, Expression<Func<SimNumber, object>>> AdminSimMappings =
        new Dictionary<SimSortBy, Expression<Func<SimNumber, object>>>
        {
            [SimSortBy.CreatedDate] = s => s.CreatedDate,
            [SimSortBy.Price] = s => s.Price,
            [SimSortBy.PhoneNumber] = s => s.PhoneNumber,
            [SimSortBy.Status] = s => s.SimStatus
        };

    /// <summary>
    /// Sắp xếp cho Gian hàng Người bán (SimOwnership).
    /// </summary>
    public static readonly IReadOnlyDictionary<SimOwnershipSortBy, Expression<Func<SimOwnership, object>>> MySimMappings =
        new Dictionary<SimOwnershipSortBy, Expression<Func<SimOwnership, object>>>
        {
            [SimOwnershipSortBy.CreatedDate] = o => o.CreatedDate,
            [SimOwnershipSortBy.AssignedDate] = o => o.CreatedDate,
            [SimOwnershipSortBy.Status] = o => o.OwnershipStatus
        };

    /// <summary>
    /// Sắp xếp cho Danh sách quyền sở hữu SIM cá nhân (SimOwnership).
    /// </summary>
    public static readonly IReadOnlyDictionary<SimOwnershipSortBy, Expression<Func<SimOwnership, object>>> OwnershipMappings =
        new Dictionary<SimOwnershipSortBy, Expression<Func<SimOwnership, object>>>
        {
            [SimOwnershipSortBy.CreatedDate] = o => o.CreatedDate,
            [SimOwnershipSortBy.AssignedDate] = o => o.CreatedDate,
            [SimOwnershipSortBy.Status] = o => o.OwnershipStatus
        };
}
