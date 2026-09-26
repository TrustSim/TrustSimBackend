namespace TrustSimBackend.Domain.Enums
{
    public enum ParticipantStatus
    {
        REGISTERED,     // Đã đăng ký tham gia
        DEPOSIT_PAID,   // Đã nộp tiền cọc thành công
        WON,            // Người chiến thắng phiên đấu giá
        RUNNER_UP,      // Người về nhì
        LOST,           // Người thua cuộc (được hoàn cọc)
        REFUNDED,       // Đã hoàn trả tiền cọc
        DEFAULTED,      // Bùng kèo (bị tịch thu tiền cọc)
        BANNED          // Bị cấm do gian lận spam bot
    }
}