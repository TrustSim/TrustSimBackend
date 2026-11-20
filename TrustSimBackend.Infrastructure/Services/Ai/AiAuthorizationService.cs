using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Entities.Ai;

namespace TrustSimBackend.Infrastructure.Services.Ai;

/// <summary>
/// Enforces AI consultation session ownership and MCP tool RBAC gates.
/// Guests may consult on public tools, while authenticated sessions are strictly isolated to their owners.
/// </summary>
public class AiAuthorizationService : IAiAuthorizationService
{
    public void EnsureCanAccessSession(AiConsultationSession session, Guid? currentUserId, string? currentUserRole = null)
    {
        if (!session.UserAccountId.HasValue)
        {
            return;
        }

        if (!currentUserId.HasValue || currentUserId.Value == Guid.Empty)
        {
            throw new ForbiddenAccessException("Phiên tư vấn này thuộc về tài khoản thành viên. Vui lòng đăng nhập đúng tài khoản để tiếp tục.");
        }

        if (session.UserAccountId.Value != currentUserId.Value)
        {
            throw new ForbiddenAccessException("Bạn không có quyền truy cập vào phiên tư vấn AI của người dùng khác.");
        }
    }

    public (bool Allowed, string? DenialMessage) AuthorizeToolAccess(McpToolDescriptor tool, AiUserContext userContext)
    {
        return tool.RequiredPermission switch
        {
            McpToolPermissionLevel.Anonymous => (true, null),
            McpToolPermissionLevel.Authenticated when userContext.IsAuthenticated => (true, null),
            McpToolPermissionLevel.Authenticated => (
                false,
                $"Công cụ '{tool.Name}' yêu cầu đăng nhập tài khoản TrustSim. Vui lòng đăng nhập để sử dụng tính năng cá nhân này."
            ),
            McpToolPermissionLevel.AdminOnly when userContext.IsAuthenticated &&
                string.Equals(userContext.Role, "ADMIN", StringComparison.OrdinalIgnoreCase) => (true, null),
            McpToolPermissionLevel.AdminOnly => (
                false,
                $"Công cụ '{tool.Name}' chỉ dành cho Quản trị viên hệ thống."
            ),
            _ => (false, $"Không đủ quyền thực thi công cụ '{tool.Name}'.")
        };
    }
}
