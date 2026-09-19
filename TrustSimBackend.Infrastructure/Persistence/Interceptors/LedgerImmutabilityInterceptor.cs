using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TrustSimBackend.Domain.Entities.Financial;

namespace TrustSimBackend.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core Interceptor bảo vệ tính bất biến (Immutability Defense-in-Depth).
/// Chặn và từ chối mọi thao tác UPDATE hoặc DELETE đối với các thực thể Sổ cái (LedgerEntry, LedgerJournal).
/// </summary>
public class LedgerImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ValidateImmutability(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ValidateImmutability(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ValidateImmutability(DbContext? context)
    {
        if (context == null) return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is LedgerEntry or LedgerJournal)
            {
                if (entry.State == EntityState.Deleted)
                {
                    throw new InvalidOperationException(
                        $"[VI PHẠM BẢO TOÀN TÀI CHÍNH] Thực thể '{entry.Entity.GetType().Name}' (ID: {entry.Property("Id").CurrentValue}) là dữ liệu sổ cái bất biến (Append-Only), tuyệt đối KHÔNG ĐƯỢC PHÉP XÓA.");
                }

                if (entry.State == EntityState.Modified)
                {
                    // Cho phép sửa trong môi trường test/demo nếu có cờ đặc biệt, ngược lại chặn hoàn toàn
                    var isAllowTamperTest = context.Database.CurrentTransaction != null &&
                                            context.ChangeTracker.Context.GetType().Name == "TrustSimDbContextTest";

                    if (!isAllowTamperTest)
                    {
                        // Kiểm tra xem có thuộc tính nào thực sự bị thay đổi không
                        var modifiedProps = entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToList();
                        if (modifiedProps.Count > 0)
                        {
                            throw new InvalidOperationException(
                                $"[VI PHẠM BẢO TOÀN TÀI CHÍNH] Thực thể '{entry.Entity.GetType().Name}' (ID: {entry.Property("Id").CurrentValue}) là dữ liệu sổ cái bất biến, tuyệt đối KHÔNG ĐƯỢC PHÉP CHỈNH SỬA các cột: [{string.Join(", ", modifiedProps)}].");
                        }
                    }
                }
            }
        }
    }
}
