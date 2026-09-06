using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Infrastructure.Persistence;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ thao tác cơ sở dữ liệu hàng loạt bằng EFCore.BulkExtensions.PostgreSql (PostgreSQL COPY Binary).
/// </summary>
public class BulkDataService : IBulkDataService
{
    private readonly TrustSimDbContext _context;

    public BulkDataService(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task BulkInsertAsync<T>(IList<T> entities, Action<BulkOperationOptions>? configureOptions = null, CancellationToken cancellationToken = default) where T : class
    {
        if (entities == null || !entities.Any()) return;
        var bulkConfig = MapConfig(configureOptions);
        await _context.BulkInsertAsync(entities, bulkConfig, cancellationToken: cancellationToken);
    }

    public async Task BulkUpdateAsync<T>(IList<T> entities, Action<BulkOperationOptions>? configureOptions = null, CancellationToken cancellationToken = default) where T : class
    {
        if (entities == null || !entities.Any()) return;
        var bulkConfig = MapConfig(configureOptions);
        await _context.BulkUpdateAsync(entities, bulkConfig, cancellationToken: cancellationToken);
    }

    public async Task BulkDeleteAsync<T>(IList<T> entities, Action<BulkOperationOptions>? configureOptions = null, CancellationToken cancellationToken = default) where T : class
    {
        if (entities == null || !entities.Any()) return;
        var bulkConfig = MapConfig(configureOptions);
        await _context.BulkDeleteAsync(entities, bulkConfig, cancellationToken: cancellationToken);
    }

    public async Task BulkInsertOrUpdateAsync<T>(IList<T> entities, Action<BulkOperationOptions>? configureOptions = null, CancellationToken cancellationToken = default) where T : class
    {
        if (entities == null || !entities.Any()) return;
        var bulkConfig = MapConfig(configureOptions);
        await _context.BulkInsertOrUpdateAsync(entities, bulkConfig, cancellationToken: cancellationToken);
    }

    public async Task BulkReadAsync<T>(IList<T> entities, Action<BulkOperationOptions>? configureOptions = null, CancellationToken cancellationToken = default) where T : class
    {
        if (entities == null || !entities.Any()) return;
        var bulkConfig = MapConfig(configureOptions);
        await _context.BulkReadAsync(entities, bulkConfig, cancellationToken: cancellationToken);
    }

    public async Task TruncateAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        await _context.TruncateAsync<T>(cancellationToken: cancellationToken);
    }

    private static BulkConfig MapConfig(Action<BulkOperationOptions>? configureOptions)
    {
        var options = new BulkOperationOptions();
        configureOptions?.Invoke(options);

        return new BulkConfig
        {
            BatchSize = options.BatchSize,
            SetOutputIdentity = options.SetOutputIdentity,
            PreserveInsertOrder = options.PreserveInsertOrder,
            TrackingEntities = options.TrackingEntities,
            PropertiesToInclude = options.PropertiesToInclude,
            PropertiesToExclude = options.PropertiesToExclude,
            UpdateByProperties = options.UpdateByProperties
        };
    }
}
