using MediatR;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.API.Controllers.Common;

/// <summary>
/// Controller cơ sở cung cấp Mediator và ICurrentUserService được UserContextMiddleware xác thực tự động từ Token trong Cookie/Header.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    private IMediator? _mediator;
    private ICurrentUserService? _currentUserService;

    /// <summary>
    /// Mediator gửi Commands/Queries xuống Application Layer.
    /// </summary>
    protected IMediator Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<IMediator>();

    /// <summary>
    /// Dịch vụ danh tính người dùng hiện tại do UserContextMiddleware nạp sẵn.
    /// </summary>
    protected ICurrentUserService CurrentUser => _currentUserService ??= HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();

    /// <summary>
    /// UserId của người dùng hiện tại (do Middleware xác thực).
    /// </summary>
    protected Guid CurrentUserId => CurrentUser.UserId ?? Guid.Empty;

    /// <summary>
    /// Kiểm tra người dùng có quyền Quản trị viên (ADMIN) không.
    /// </summary>
    protected bool IsAdmin => CurrentUser.Role == "ADMIN" || User.IsInRole("ADMIN");

    /// <summary>
    /// Kiểm tra người dùng có quyền Tư vấn viên (CONSULTANT) không.
    /// </summary>
    protected bool IsConsultant => CurrentUser.Role == "CONSULTANT" || User.IsInRole("CONSULTANT");

    /// <summary>
    /// Bọc dữ liệu trả về theo format ApiResponse chuẩn HTTP 200 OK.
    /// </summary>
    protected IActionResult HandleResult<T>(T result, string message = "Thao tác thành công.")
    {
        return Ok(ApiResponse<T>.SuccessResult(result, message));
    }

    /// <summary>
    /// Bọc kết quả phân trang theo format ApiResponse&lt;PagedResult&lt;T&gt;&gt; chuẩn HTTP 200 OK.
    /// </summary>
    protected IActionResult HandlePagedResult<T>(PagedResult<T> result, string message = "Lấy dữ liệu danh sách thành công.")
    {
        return Ok(ApiResponse<PagedResult<T>>.SuccessResult(result, message));
    }
}

