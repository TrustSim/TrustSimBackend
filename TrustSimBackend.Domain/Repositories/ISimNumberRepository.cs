using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý kho SIM số đẹp (SimNumber).
/// </summary>
public interface ISimNumberRepository
{
    Task<SimNumber?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SimNumber?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<SimNumber> Items, int TotalCount)> GetPagedAsync(
        string? keyword,
        Guid? carrierId,
        Guid? categoryId,
        Guid? prefixId,
        SimStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<Guid> GetOrCreateDefaultFormatRhythmIdAsync(CancellationToken cancellationToken = default);
    Task AddAsync(SimNumber simNumber, CancellationToken cancellationToken = default);
    void Update(SimNumber simNumber);
}
