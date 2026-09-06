namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Kết quả tổng hợp sau khi thực hiện import dữ liệu hàng loạt từ file.
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của bản ghi hoặc Entity.</typeparam>
public class ImportResult<T>
{
    /// <summary>Tổng số dòng dữ liệu đọc được từ file (không tính dòng tiêu đề).</summary>
    public int TotalRows { get; set; }

    /// <summary>Số lượng dòng hợp lệ và import thành công vào hệ thống.</summary>
    public int SuccessCount { get; set; }

    /// <summary>Số lượng dòng bị lỗi (lỗi định dạng, trùng lặp, vi phạm ràng buộc dữ liệu).</summary>
    public int FailedCount { get; set; }

    /// <summary>Danh sách các bản ghi đã import thành công.</summary>
    public List<T> SuccessItems { get; set; } = new();

    /// <summary>Danh sách chi tiết các lỗi theo từng dòng.</summary>
    public List<ImportRowError> Errors { get; set; } = new();

    /// <summary>Cho biết toàn bộ quá trình import có thành công 100% (không có dòng nào bị lỗi) hay không.</summary>
    public bool IsSuccess => FailedCount == 0;
}

/// <summary>
/// Thông tin chi tiết một dòng bị lỗi trong file import.
/// </summary>
public class ImportRowError
{
    /// <summary>Số thứ tự dòng trong file (1-indexed, tính theo file gốc).</summary>
    public int RowIndex { get; set; }

    /// <summary>Tên cột hoặc trường dữ liệu bị lỗi.</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>Nội dung chi tiết thông báo lỗi.</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>Dữ liệu thô của dòng đó để người dùng dễ đối chiếu và sửa lại.</summary>
    public string? RawData { get; set; }
}

/// <summary>
/// Định dạng file hỗ trợ khi Import/Export.
/// </summary>
public enum ImportExportFormat
{
    Csv = 1,
    Excel = 2
}
