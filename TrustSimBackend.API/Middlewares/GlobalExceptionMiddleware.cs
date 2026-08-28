using System.Net;
using System.Text.Json;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.API.Middlewares;

/// <summary>
/// Middleware bắt tập trung mọi Exception trong Pipeline và chuyển đổi thành ApiResponse chuẩn.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

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
            _logger.LogError(ex, "Đã xảy ra lỗi chưa được xử lý: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ApiResponse<object>
        {
            Success = false,
            Timestamp = DateTime.UtcNow
        };

        switch (exception)
        {
            case ValidationException validationEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Message = validationEx.Message;
                response.Errors = validationEx.Errors
                    .SelectMany(x => x.Value.Select(err => $"{x.Key}: {err}"))
                    .ToList();
                break;

            case BadRequestException badRequestEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Message = badRequestEx.Message;
                break;

            case NotFoundException notFoundEx:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Message = notFoundEx.Message;
                break;

            case ForbiddenAccessException forbiddenEx:
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                response.Message = forbiddenEx.Message;
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Message = "Đã xảy ra lỗi máy chủ nội bộ. Vui lòng thử lại sau.";
#if DEBUG
                response.Errors = new List<string> { exception.Message, exception.StackTrace ?? string.Empty };
#endif
                break;
        }

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
