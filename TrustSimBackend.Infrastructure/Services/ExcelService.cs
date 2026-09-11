using MiniExcelLibs;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ đọc/ghi file Excel (.xlsx) hiệu năng cao bằng MiniExcel.
/// </summary>
public class ExcelService : IExcelService
{
    public async IAsyncEnumerable<T> ReadExcelStreamAsync<T>(
        Stream stream,
        string? sheetName = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class, new()
    {
        var rows = await stream.QueryAsync<T>(sheetName: sheetName);
        foreach (var row in rows)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            if (row != null)
            {
                yield return row;
            }
        }
    }

    public async Task<List<T>> ReadExcelAsync<T>(
        Stream stream,
        string? sheetName = null,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        var result = new List<T>();
        await foreach (var item in ReadExcelStreamAsync<T>(stream, sheetName, cancellationToken))
        {
            result.Add(item);
        }
        return result;
    }

    public async Task<byte[]> WriteExcelAsync<T>(
        IEnumerable<T> records,
        string sheetName = "Sheet1",
        CancellationToken cancellationToken = default) where T : class
    {
        using var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(records, sheetName: sheetName, printHeader: true, cancellationToken: cancellationToken);
        return memoryStream.ToArray();
    }
}
