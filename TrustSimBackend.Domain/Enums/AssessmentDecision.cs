namespace TrustSimBackend.Domain.Enums
{
    /// <summary>
    /// Quyết định xử lý rủi ro đối với người tham gia đấu giá.
    /// </summary>
    public enum AssessmentDecision
    {
        /// <summary>Cho phép tham gia bình thường.</summary>
        ALLOW,

        /// <summary>Gắn cờ cảnh báo / theo dõi thêm.</summary>
        FLAG_WARNING,

        /// <summary>Yêu cầu xác minh bổ sung (OTP, Captcha...).</summary>
        REQUIRE_VERIFICATION,

        /// <summary>Chặn / khóa quyền tham gia phiên đấu giá.</summary>
        BLOCK_PARTICIPANT
    }
}
