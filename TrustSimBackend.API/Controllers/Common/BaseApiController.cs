using MediatR;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Interfaces;

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
}
