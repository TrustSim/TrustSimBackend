using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Auctions
{
    /// <summary>
    /// Đánh giá rủi ro gian lận của người tham gia đấu giá (shill bidding, spam bid, tài khoản nghi vấn...).
    /// Chỉ Audit, KHÔNG xóa — lưu vết lịch sử phục vụ kiểm toán rủi ro.
    /// </summary>
    public class FraudAssessment : AuditableEntity
    {
        /// <summary>Khóa chính của bản ghi đánh giá (alias cho <see cref="BaseEntity.Id"/>).</summary>
        public Guid FraudAssessmentId { get => Id; set => Id = value; }

        // --- Foreign Key ---
        /// <summary>ID của người tham gia đấu giá (<see cref="AuctionParticipant"/>) bị đánh giá.</summary>
        public Guid ParticipantId { get; set; }

        // --- Điểm và mức độ rủi ro ---
        /// <summary>Điểm rủi ro tính toán được từ hệ thống (thang điểm 0.0 - 100.0 hoặc 0.0 - 1.0).</summary>
        public float RiskScore { get; set; }

        /// <summary>Mức độ rủi ro: LOW, MEDIUM, HIGH, CRITICAL.</summary>
        public RiskLevel RiskLevel { get; set; } = RiskLevel.LOW;

        /// <summary>Chuỗi mô tả dấu hiệu / tín hiệu phát hiện gian lận (VD: "Spam 15 bids trong 10s").</summary>
        public string FraudSignal { get; set; } = string.Empty;

        // --- Quyết định và trạng thái xử lý ---
        /// <summary>Quyết định xử lý rủi ro: ALLOW, FLAG_WARNING, REQUIRE_VERIFICATION, BLOCK_PARTICIPANT.</summary>
        public AssessmentDecision AssessmentDecision { get; set; } = AssessmentDecision.ALLOW;

        /// <summary>Trạng thái xử lý của bản ghi: AUTOMATED, PENDING_MANUAL_REVIEW, REVIEWED, OVERRULED.</summary>
        public AssessmentStatus AssessmentStatus { get; set; } = AssessmentStatus.AUTOMATED;

        // --- Thời gian ---
        /// <summary>Thời điểm hệ thống thực hiện đánh giá.</summary>
        public DateTime AssessedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Thời điểm Admin rà soát thủ công (nếu có). Null nếu tự động hoàn toàn.</summary>
        public DateTime? ReviewedAt { get; set; }

        // --- Navigation Property ---
        /// <summary>Người tham gia đấu giá liên kết với bản đánh giá này.</summary>
        public virtual AuctionParticipant? AuctionParticipant { get; set; }
    }
}