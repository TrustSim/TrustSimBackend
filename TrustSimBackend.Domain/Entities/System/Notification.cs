using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;

namespace TrustSimBackend.Domain.Entities.System
{
    public class Notification: FullAuditableEntity
    {

        public string Title {get;set;}= string.Empty;

        public string Content {get;set;}= string.Empty;
        
        /// <summary>
        /// Đường dẫn đến trang chi tiết hành động (nếu có).
        /// </summary>
        public string? ActionUrl {get;set;}
        /// <summary>
        /// Trạng thái đã đọc hay chưa đọc.
        /// </summary>
        public bool IsRead {get;set;} = false;

        public Guid UserId {get;set;}
        public virtual UserAccount? User { get; set; }

    }
}