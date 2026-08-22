namespace TrustSimBackend.Domain.Base
{
    /// <summary>
    /// Soft delete interface dùng để định nghĩa các thuộc tính soft delete cho các entity
    /// </summary>
    public interface ISoftDelete
    {
        /// <summary>
        /// Ngày xóa
        /// </summary>
        DateTime? DeletedDate { get; set; }

        /// <summary>
        /// Người xóa
        /// </summary>
        string? DeletedBy { get; set; }

        /// <summary>
        /// Có bị xóa không
        /// </summary>
        bool IsDeleted { get; set; }
    }
}