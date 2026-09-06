using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ đọc/ghi CSV bằng CsvHelper với mã hóa UTF-8.
/// </summary>
public class CsvService : ICsvService
{
    private readonly CsvConfiguration _config;

    public CsvService()
    {
        _config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
            BadDataFound = null,
            IgnoreBlankLines = true,
            Delimiter = ","
        };
    }

    public async IAsyncEnumerable<T> ReadCsvStreamAsync<T>(Stream stream, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csv = new CsvReader(reader, _config);

        var records = csv.GetRecordsAsync<T>(cancellationToken);
        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            yield return record;
        }
    }

    public async Task<List<T>> ReadCsvAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : class
    {
        var list = new List<T>();
        await foreach (var record in ReadCsvStreamAsync<T>(stream, cancellationToken))
        {
            list.Add(record);
        }
        return list;
    }

    public async Task<byte[]> WriteCsvAsync<T>(IEnumerable<T> records, CancellationToken cancellationToken = default) where T : class
    {
        using var memoryStream = new MemoryStream();
        // Ghi UTF-8 BOM để các ứng dụng như Microsoft Excel mở trực tiếp file tiếng Việt không bị lỗi font
        var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        
        await using (var writer = new StreamWriter(memoryStream, utf8WithBom, leaveOpen: true))
        await using (var csv = new CsvWriter(writer, _config))
        {
            await csv.WriteRecordsAsync(records, cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }

        return memoryStream.ToArray();
    }
}
