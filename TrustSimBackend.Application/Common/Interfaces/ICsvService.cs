namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Interface cung cấp các dịch vụ đọc và ghi file CSV chuẩn RFC 4180.
/// </summary>
public interface ICsvService
{
    /// <summary>
    /// Đọc stream file CSV theo dạng bất đồng bộ IAsyncEnumerable để tiết kiệm bộ nhớ khi file có kích thước lớn.
    /// </summary>
    IAsyncEnumerable<T> ReadCsvStreamAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Đọc toàn bộ file CSV vào danh sách DTO.
    /// </summary>
    Task<List<T>> ReadCsvAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Ghi danh sách dữ liệu ra stream CSV dưới dạng mảng byte UTF-8 (kèm UTF-8 BOM để Excel hiển thị đúng tiếng Việt).
    /// </summary>
    Task<byte[]> WriteCsvAsync<T>(IEnumerable<T> records, CancellationToken cancellationToken = default) where T : class;
}
