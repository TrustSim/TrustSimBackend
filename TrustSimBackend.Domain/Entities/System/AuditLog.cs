using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.System
{
    /// <summary>
    /// Đại diện cho một log hệ thống
    /// </summary>
    public class AuditLog : AuditableEntity
    {
        /// <summary>
        /// Khóa chính của log hệ thống (alias cho <see cref="BaseEntity.Id"/>)
        /// </summary>
        public Guid AuditLogId {get=>Id; private set => Id = value;}

        /// <summary>
        /// Tên của log hệ thống
        /// </summary>
        public string AuditLogName {get;set;}=string.Empty;

        /// <summary>
        /// Loại hành động
        /// </summary>
        public ActionType ActionType {get;set;}

        /// <summary>
        /// Dữ liệu mới
        /// </summary>
        public string NewData {get;set;}=string.Empty;
        /// <summary>
        /// Dữ liệu cũ
        /// </summary>
        public string OldData {get;set;}=string.Empty;
        /// <summary>
        /// Địa chỉ IP
        /// </summary>
        public string IPAddress {get;set;}=string.Empty;
        
        /// <summary>
        /// ID người thực hiện hành động
        /// </summary>
        public Guid ActorId {get;set;}

        /// <summary>
        /// Người thực hiện hành động
        /// </summary>
        public virtual UserAccount UserAccount { get; set; }=new UserAccount();

        /// <summary>
        /// ID của thực thể bị tác động
        /// </summary>
        public Guid EntityId {get;set;}
        

        /// <summary>
        /// Tên của thực thể bị tác động
        /// </summary>
        public string EntityName {get;set;} =string.Empty;
    }
}