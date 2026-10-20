using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository quản lý thực thể SIM (SimNumber), quyền sở hữu (SimOwnership) và xác minh SIM (SimVerification).
/// </summary>
public interface ISimRepository
{
    Task<SimNumber?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SimNumber?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<bool> ExistsPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task AddAsync(SimNumber simNumber, CancellationToken cancellationToken = default);
    void Update(SimNumber simNumber);

    // Lấy chi tiết đầy đủ kèm tất cả Navigation Properties
    Task<SimNumber?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Truy vấn phân trang cho User (Kho SIM của tôi) - Trả về (Items, TotalCount)
    Task<(List<SimOwnership> Items, int TotalCount)> GetMySimsPagedAsync(
        Guid userAccountId,
        MySimsQueryFilter filter,
        CancellationToken cancellationToken = default);

    // Truy vấn phân trang cho Admin (Toàn bộ kho SIM hệ thống) - Trả về (Items, TotalCount)
    Task<(List<SimNumber> Items, int TotalCount)> GetAdminSimsPagedAsync(
        AdminSimsQueryFilter filter,
        CancellationToken cancellationToken = default);

    // Lấy quyền sở hữu đang hoạt động
    Task<SimOwnership?> GetActiveOwnershipAsync(Guid simNumberId, CancellationToken cancellationToken = default);
}
