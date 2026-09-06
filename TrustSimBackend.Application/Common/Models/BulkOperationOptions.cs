namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Các tùy chọn cấu hình nâng cao khi thực hiện thao tác cơ sở dữ liệu hàng loạt (Bulk Operations).
/// </summary>
public class BulkOperationOptions
{
    /// <summary>Số lượng bản ghi xử lý trong một Batch (Mặc định 5000).</summary>
    public int BatchSize { get; set; } = 5000;

    /// <summary>Có tự động nạp lại giá trị Identity/Key sau khi Insert hay không.</summary>
    public bool SetOutputIdentity { get; set; } = true;

    /// <summary>Bảo toàn thứ tự danh sách khi Insert.</summary>
    public bool PreserveInsertOrder { get; set; } = false;

    /// <summary>Bật chế độ Tracking Entity của EF Core sau khi bulk thao tác.</summary>
    public bool TrackingEntities { get; set; } = false;

    /// <summary>Danh sách tên thuộc tính cần include (nếu chỉ muốn update/insert một số cột cụ thể).</summary>
    public List<string>? PropertiesToInclude { get; set; }

    /// <summary>Danh sách tên thuộc tính cần loại trừ.</summary>
    public List<string>? PropertiesToExclude { get; set; }

    /// <summary>Danh sách tên thuộc tính dùng làm khóa so khớp khi Upsert (BulkInsertOrUpdate).</summary>
    public List<string>? UpdateByProperties { get; set; }
}
