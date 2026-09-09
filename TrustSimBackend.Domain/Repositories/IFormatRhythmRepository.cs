using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Domain.Repositories;

public interface IFormatRhythmRepository
{
    Task<FormatRhythm?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormatRhythm>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsByValueAsync(string value, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(FormatRhythm formatRhythm, CancellationToken cancellationToken = default);
    Task UpdateAsync(FormatRhythm formatRhythm, CancellationToken cancellationToken = default);
    Task DeleteAsync(FormatRhythm formatRhythm, CancellationToken cancellationToken = default);
}
