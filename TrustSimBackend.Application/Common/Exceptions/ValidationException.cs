using FluentValidation.Results;

namespace TrustSimBackend.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ bắn ra khi dữ liệu đầu vào không vượt qua kiểm tra của FluentValidation (tương ứng HTTP 400).
/// Chứa danh sách lỗi chi tiết theo từng thuộc tính (Property).
/// </summary>
public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException()
        : base("Dữ liệu đầu vào không hợp lệ. Đã xảy ra một hoặc nhiều lỗi xác thực.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : this()
    {
        Errors = failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());
    }
}
