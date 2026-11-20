using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Entities.Ai;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Enforces session ownership isolation (anti-IDOR) and MCP tool RBAC permissions.
/// </summary>
public interface IAiAuthorizationService
{
    void EnsureCanAccessSession(AiConsultationSession session, Guid? currentUserId, string? currentUserRole = null);
    (bool Allowed, string? DenialMessage) AuthorizeToolAccess(McpToolDescriptor tool, AiUserContext userContext);
}
