using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class FormatRhythmRepository : IFormatRhythmRepository
{
    private readonly TrustSimDbContext _context;

    public FormatRhythmRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<FormatRhythm?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FormatRhythms
            .Include(f => f.SimNumbers)
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<FormatRhythm>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FormatRhythms
            .Where(f => !f.IsDeleted)
            .OrderBy(f => f.FormatRhythmName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.FormatRhythms.Where(f => f.FormatRhythmName.ToLower() == name.ToLower() && !f.IsDeleted);
        if (excludeId.HasValue)
        {
            query = query.Where(f => f.Id != excludeId.Value);
        }
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<bool> ExistsByValueAsync(string value, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.FormatRhythms.Where(f => f.Value.ToLower() == value.ToLower() && !f.IsDeleted);
        if (excludeId.HasValue)
        {
            query = query.Where(f => f.Id != excludeId.Value);
        }
        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(FormatRhythm formatRhythm, CancellationToken cancellationToken = default)
    {
        await _context.FormatRhythms.AddAsync(formatRhythm, cancellationToken);
    }

    public Task UpdateAsync(FormatRhythm formatRhythm, CancellationToken cancellationToken = default)
    {
        _context.FormatRhythms.Update(formatRhythm);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(FormatRhythm formatRhythm, CancellationToken cancellationToken = default)
    {
        formatRhythm.IsDeleted = true;
        formatRhythm.UpdatedDate = DateTime.UtcNow;
        _context.FormatRhythms.Update(formatRhythm);
        return Task.CompletedTask;
    }
}
