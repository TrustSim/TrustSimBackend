using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Identity
{
    /// <summary>
    /// Thông tin tài khoản đăng nhập của người dùng
    /// </summary>
    public class UserAccount : BaseEntity
    {
        public Guid UserAccountId { get => Id; set => Id = value; }
        public string UserName { get; set; } = string.Empty;
        public string PassWordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.USER;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation Property (1-1 với UserProfile)
        /// <summary>
        /// Thông tin cá nhân liên kết với tài khoản này
        /// </summary>
        public virtual UserProfile? UserProfile { get; set; }

        public virtual UserSecurityProfile? UserSecurityProfile { get; set; }   
    }
}
