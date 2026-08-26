namespace TrustSimBackend.Domain.Enums
{
    /// <summary>
    /// Trạng thái xử lý của bản ghi đánh giá rủi ro gian lận.
    /// </summary>
    public enum AssessmentStatus
    {
        /// <summary>Hệ thống tự động đánh giá và đưa ra quyết định.</summary>
        AUTOMATED,

        /// <summary>Đang chờ Admin / Chuyên viên rà soát thủ công do điểm rủi ro cao.</summary>
        PENDING_MANUAL_REVIEW,

        /// <summary>Admin đã rà soát và xác nhận quyết định.</summary>
        REVIEWED,

        /// <summary>Admin đã can thiệp và thay đổi quyết định so với hệ thống tự động.</summary>
        OVERRULED
    }
}
