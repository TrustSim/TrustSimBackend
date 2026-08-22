namespace TrustSimBackend.Domain.Base
{
    /// <summary>
    /// Audit log interface dùng để định nghĩa các thuộc tính audit log cho các entity
    /// </summary>
    public interface IAuditlog
    {
        /// <summary>
        /// Ngày tạo
        /// </summary>
        DateTime CreatedDate { get; set; }

        /// <summary>
        /// Người tạo
        /// </summary>
        string? CreatedBy { get; set; }

        /// <summary>
        /// Ngày cập nhật
        /// </summary>
        DateTime UpdatedDate { get; set; }

        /// <summary>
        /// Người cập nhật
        /// </summary>
        string? UpdatedBy { get; set; }
    }
}