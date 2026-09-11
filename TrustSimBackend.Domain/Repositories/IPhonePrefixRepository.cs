using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý Đầu số nhà mạng (PhonePrefix).
/// </summary>
public interface IPhonePrefixRepository
{
    Task<IReadOnlyList<PhonePrefix>> GetAllAsync(Guid? carrierId = null, CancellationToken cancellationToken = default);
    Task<PhonePrefix?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PhonePrefix?> GetByValueAsync(string prefixValue, CancellationToken cancellationToken = default);
    Task<bool> ExistsByValueAsync(string prefixValue, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(PhonePrefix prefix, CancellationToken cancellationToken = default);
    void Update(PhonePrefix prefix);
}
