namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Interface cung cấp dịch vụ đọc và xuất file Excel (.xlsx) với hiệu năng cao và tiết kiệm RAM (MiniExcel).
/// </summary>
public interface IExcelService
{
    /// <summary>
    /// Đọc stream file Excel theo dạng bất đồng bộ IAsyncEnumerable streaming từng dòng.
    /// </summary>
    IAsyncEnumerable<T> ReadExcelStreamAsync<T>(Stream stream, string? sheetName = null, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Đọc toàn bộ file Excel vào danh sách DTO.
    /// </summary>
    Task<List<T>> ReadExcelAsync<T>(Stream stream, string? sheetName = null, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Xuất danh sách dữ liệu ra mảng byte định dạng Excel (.xlsx).
    /// </summary>
    Task<byte[]> WriteExcelAsync<T>(IEnumerable<T> records, string sheetName = "Sheet1", CancellationToken cancellationToken = default) where T : class;
}
