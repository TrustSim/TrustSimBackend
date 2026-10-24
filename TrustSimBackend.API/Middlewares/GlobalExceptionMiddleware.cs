using System.Text.Json;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.API.Middlewares;

/// <summary>
/// Middleware bắt tập trung mọi Exception trong Pipeline và chuyển đổi thành ApiResponse chuẩn.
/// Sử dụng Dictionary Mapping Pattern (O(1) Lookup) thay vì switch-case để tuân thủ Open/Closed Principle (OCP).
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    // Bảng tra cứu xử lý lỗi tập trung O(1) theo Type
    private static readonly Dictionary<Type, Func<Exception, (int StatusCode, string Message, List<string>? Errors)>> _exceptionHandlers = new()
    {
        [typeof(ValidationException)] = ex =>
        {
            var vEx = (ValidationException)ex;
            return (
                StatusCodes.Status400BadRequest,
                vEx.Message,
                vEx.Errors.SelectMany(x => x.Value.Select(err => $"{x.Key}: {err}")).ToList()
            );
        },
        [typeof(BadRequestException)] = ex => (StatusCodes.Status400BadRequest, ex.Message, null),
        [typeof(NotFoundException)] = ex => (StatusCodes.Status404NotFound, ex.Message, null),
        [typeof(ForbiddenAccessException)] = ex => (StatusCodes.Status403Forbidden, ex.Message, null),
        [typeof(UnauthorizedException)] = ex => (StatusCodes.Status401Unauthorized, ex.Message, null),
        [typeof(UnauthorizedAccessException)] = ex => (StatusCodes.Status401Unauthorized, ex.Message, null),
        [typeof(KeyNotFoundException)] = ex => (StatusCodes.Status404NotFound, ex.Message, null),
        [typeof(ArgumentNullException)] = ex => (StatusCodes.Status400BadRequest, ex.Message, null),
        [typeof(ArgumentException)] = ex => (StatusCodes.Status400BadRequest, ex.Message, null),
        [typeof(InvalidOperationException)] = ex => (StatusCodes.Status400BadRequest, ex.Message, null),
        [typeof(Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)] = ex => (StatusCodes.Status409Conflict, "Giao dịch tài chính đang được xử lý đồng thời bởi một luồng khác. Vui lòng thử lại sau giây lát.", null),

        // ─── Các Exception Nghiệp vụ eKYC / Identity Verification ──────────────
        [typeof(IdentityAlreadyVerifiedException)] = ex => (StatusCodes.Status409Conflict, ex.Message, null),
        [typeof(DuplicateIdentityNumberException)] = ex => (StatusCodes.Status409Conflict, ex.Message, null),
        [typeof(IdentityNumberMismatchException)] = ex => (StatusCodes.Status400BadRequest, ex.Message, null),
        [typeof(IdentityNameMismatchException)] = ex => (StatusCodes.Status400BadRequest, ex.Message, null),
        [typeof(KycFaceMismatchException)] = ex => (StatusCodes.Status400BadRequest, ex.Message, null),
        [typeof(InvalidIdentityDocumentException)] = ex => (StatusCodes.Status400BadRequest, ex.Message, null),
        [typeof(KycImageQualityException)] = ex => (StatusCodes.Status400BadRequest, ex.Message, null),

        // ─── Các Exception Nghiệp vụ Xác thực Quyền Sở hữu SIM ──────────────────
        [typeof(UserProfileNotVerifiedException)] = ex => (StatusCodes.Status403Forbidden, ex.Message, null),
        [typeof(UnsupportedCarrierException)] = ex => (StatusCodes.Status400BadRequest, ex.Message, null),
        [typeof(SimCarrierNotFoundException)] = ex => (StatusCodes.Status404NotFound, ex.Message, null),
        [typeof(SimAlreadyOwnedException)] = ex => (StatusCodes.Status409Conflict, ex.Message, null),
        [typeof(SimOwnershipVerificationFailedException)] = ex => (StatusCodes.Status400BadRequest, ex.Message, null),
        [typeof(OtpVerificationFailedException)] = ex => (StatusCodes.Status400BadRequest, ex.Message, null)
    };

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (ex is ValidationException vEx)
            {
                var details = string.Join(" | ", vEx.Errors.SelectMany(x => x.Value.Select(err => $"{x.Key}: {err}")));
                _logger.LogWarning("[GlobalExceptionMiddleware] Lỗi ValidationException chi tiết: {Errors}", details);
            }
            else if (_exceptionHandlers.ContainsKey(ex.GetType()) || _exceptionHandlers.Keys.Any(k => k.IsAssignableFrom(ex.GetType())))
            {
                _logger.LogWarning("[GlobalExceptionMiddleware] Lỗi quy tắc nghiệp vụ ({Type}): {Message}", ex.GetType().Name, ex.Message);
            }
            else
            {
                _logger.LogError(ex, "[GlobalExceptionMiddleware] Lỗi hệ thống chưa được xử lý: {Message}", ex.Message);
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var exceptionType = exception.GetType();
        int statusCode;
        string message;
        List<string>? errors = null;

        // 1. Tìm chính xác Type trong Dictionary (O(1))
        if (_exceptionHandlers.TryGetValue(exceptionType, out var handler))
        {
            (statusCode, message, errors) = handler(exception);
        }
        else
        {
            // 2. Tìm kiếm theo kiểu kế thừa (Assignable Type fallback)
            var assignableEntry = _exceptionHandlers.FirstOrDefault(entry => entry.Key.IsAssignableFrom(exceptionType));
            if (assignableEntry.Value != null)
            {
                (statusCode, message, errors) = assignableEntry.Value(exception);
            }
            else
            {
                // 3. Fallback mặc định 500 Internal Server Error
                statusCode = StatusCodes.Status500InternalServerError;
                message = "Đã xảy ra lỗi máy chủ nội bộ. Vui lòng thử lại sau.";
#if DEBUG
                errors = new List<string> { exception.Message, exception.StackTrace ?? string.Empty };
#endif
            }
        }

        context.Response.StatusCode = statusCode;
        var response = ApiResponse<object>.FailureResult(message, errors);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
