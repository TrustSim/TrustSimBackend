using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core Interceptor tự động cập nhật thông tin Audit, Soft Delete và ghi AuditLog cho các thực thể kế thừa AuditableEntity.
/// </summary>
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly Guid SystemAdminId = Guid.Parse("11000000-0000-0000-0000-000000000001");

    public AuditableEntityInterceptor(
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor)
    {
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ProcessAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ProcessAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ProcessAuditEntries(DbContext? context)
    {
        if (context == null) return;

        var utcNow = DateTime.UtcNow;
        var currentUserId = _currentUserService.UserId;
        var currentUserName = _currentUserService.UserName ?? "System";
        var httpContext = _httpContextAccessor.HttpContext;

        var ipAddress = httpContext?.Request?.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? httpContext?.Connection?.RemoteIpAddress?.ToString()
            ?? "127.0.0.1";

        var actorId = (currentUserId.HasValue && currentUserId.Value != Guid.Empty)
            ? currentUserId.Value
            : SystemAdminId;

        var auditLogs = new List<AuditLog>();
        var entries = context.ChangeTracker.Entries().ToList();

        var canCreateAuditLogs = false;
        if (currentUserId.HasValue && currentUserId.Value != Guid.Empty)
        {
            canCreateAuditLogs = true;
        }
        else
        {
            try
            {
                canCreateAuditLogs = context.Set<UserAccount>().Local.Any(u => u.Id == SystemAdminId)
                    || context.Set<UserAccount>().Any(u => u.Id == SystemAdminId);
            }
            catch
            {
                canCreateAuditLogs = false;
            }
        }

        foreach (var entry in entries)
        {
            // 1. Cập nhật các trường thời gian và người thao tác (IAuditlog)
            if (entry.Entity is IAuditlog audit)
            {
                if (entry.State == EntityState.Added)
                {
                    audit.CreatedDate = utcNow;
                    audit.CreatedBy ??= currentUserName;
                    audit.UpdatedDate = utcNow;
                    audit.UpdatedBy ??= currentUserName;
                }
                else if (entry.State == EntityState.Modified)
                {
                    audit.UpdatedDate = utcNow;
                    audit.UpdatedBy = currentUserName;
                }
            }

            // 2. Tự động áp dụng Soft Delete cho các thực thể kế thừa ISoftDelete
            if (entry.Entity is ISoftDelete softDelete)
            {
                if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    softDelete.IsDeleted = true;
                    softDelete.DeletedDate = utcNow;
                    softDelete.DeletedBy = currentUserName;
                }
                else if (entry.State == EntityState.Modified)
                {
                    var isDeletedProp = entry.Property(nameof(ISoftDelete.IsDeleted));
                    if (isDeletedProp.IsModified && softDelete.IsDeleted)
                    {
                        softDelete.DeletedDate = utcNow;
                        softDelete.DeletedBy = currentUserName;
                    }
                }
            }

            // 3. Tự động sinh bản ghi AuditLog cho các thực thể kế thừa AuditableEntity (loại trừ chính AuditLog)
            if (canCreateAuditLogs && entry.Entity is AuditableEntity auditableEntity && entry.Entity is not AuditLog)
            {
                var entityName = entry.Metadata.ClrType.Name;
                if (auditableEntity.Id == Guid.Empty)
                {
                    auditableEntity.Id = Guid.NewGuid();
                }
                var entityId = auditableEntity.Id;

                if (entry.State == EntityState.Added)
                {
                    var newValues = new Dictionary<string, object?>();
                    foreach (var prop in entry.Properties)
                    {
                        if (prop.Metadata.IsShadowProperty()) continue;
                        newValues[prop.Metadata.Name] = prop.CurrentValue;
                    }

                    auditLogs.Add(new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        AuditLogName = $"Thêm mới {entityName}",
                        ActionType = ActionType.CREATE,
                        EntityName = entityName,
                        EntityId = entityId,
                        ActorId = actorId,
                        IPAddress = ipAddress,
                        NewData = JsonSerializer.Serialize(newValues),
                        OldData = "{}",
                        CreatedDate = utcNow,
                        CreatedBy = currentUserName,
                        UpdatedDate = utcNow,
                        UpdatedBy = currentUserName
                    });
                }
                else if (entry.State == EntityState.Modified)
                {
                    var oldValues = new Dictionary<string, object?>();
                    var newValues = new Dictionary<string, object?>();

                    foreach (var prop in entry.Properties)
                    {
                        if (prop.Metadata.IsShadowProperty()) continue;
                        if (prop.IsModified
                            && !string.Equals(prop.Metadata.Name, "UpdatedDate", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(prop.Metadata.Name, "UpdatedBy", StringComparison.OrdinalIgnoreCase))
                        {
                            oldValues[prop.Metadata.Name] = prop.OriginalValue;
                            newValues[prop.Metadata.Name] = prop.CurrentValue;
                        }
                    }

                    if (oldValues.Count > 0)
                    {
                        auditLogs.Add(new AuditLog
                        {
                            Id = Guid.NewGuid(),
                            AuditLogName = $"Cập nhật {entityName}",
                            ActionType = ActionType.UPDATE,
                            EntityName = entityName,
                            EntityId = entityId,
                            ActorId = actorId,
                            IPAddress = ipAddress,
                            NewData = JsonSerializer.Serialize(newValues),
                            OldData = JsonSerializer.Serialize(oldValues),
                            CreatedDate = utcNow,
                            CreatedBy = currentUserName,
                            UpdatedDate = utcNow,
                            UpdatedBy = currentUserName
                        });
                    }
                }
            }
        }

        if (auditLogs.Count > 0)
        {
            context.Set<AuditLog>().AddRange(auditLogs);
        }
    }
}

