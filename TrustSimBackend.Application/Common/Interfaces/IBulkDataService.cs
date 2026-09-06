using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Interface cung cấp các thao tác cơ sở dữ liệu hàng loạt với hiệu năng cao (PostgreSQL COPY Binary).
/// </summary>
public interface IBulkDataService
{
    /// <summary>
    /// Thêm mới hàng loạt danh sách thực thể với tốc độ cao.
    /// </summary>
    Task BulkInsertAsync<T>(IList<T> entities, Action<BulkOperationOptions>? configureOptions = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Cập nhật hàng loạt danh sách thực thể.
    /// </summary>
    Task BulkUpdateAsync<T>(IList<T> entities, Action<BulkOperationOptions>? configureOptions = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Xóa hàng loạt danh sách thực thể theo danh sách truyền vào.
    /// </summary>
    Task BulkDeleteAsync<T>(IList<T> entities, Action<BulkOperationOptions>? configureOptions = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Thực hiện Upsert (Insert nếu chưa có, Update nếu đã tồn tại theo khóa chính hoặc thuộc tính chỉ định).
    /// </summary>
    Task BulkInsertOrUpdateAsync<T>(IList<T> entities, Action<BulkOperationOptions>? configureOptions = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Đọc dữ liệu hàng loạt theo danh sách thực thể chứa khóa.
    /// </summary>
    Task BulkReadAsync<T>(IList<T> entities, Action<BulkOperationOptions>? configureOptions = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Xóa trắng bảng dữ liệu (TRUNCATE TABLE) siêu tốc.
    /// </summary>
    Task TruncateAsync<T>(CancellationToken cancellationToken = default) where T : class;
}
