using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Entities.System
{
    /// <summary>
    /// Đại diện cho một tài liệu hệ thống
    /// </summary>
    public class Document: FullAuditableEntity
    {

        /// <summary>
        /// Tên của tài liệu
        /// </summary>
        public string DocumentName {get;set;}=string.Empty;
        
        /// <summary>
        /// Đường dẫn đến tài liệu
        /// </summary>
        public string LinkUrl {get;set;}=string.Empty;
    }
}