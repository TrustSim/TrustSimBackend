namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Các tiêu chí sắp xếp cho nhật ký kiểm toán hệ thống (AuditLog).
/// </summary>
public enum AuditLogSortBy
{
    CreatedDate = 1,
    EntityName = 2,
    ActionType = 3
}
