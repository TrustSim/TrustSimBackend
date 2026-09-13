using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Ai;

/// <summary>
/// Đại diện cho một phiên tư vấn tìm kiếm SIM thông minh giữa khách hàng và AI Consultant.
/// </summary>
public class AiConsultationSession : FullAuditableEntity
{
    /// <summary>ID người dùng (nếu đã đăng nhập, null nếu là khách vãng lai).</summary>
    public Guid? UserAccountId { get; set; }

    /// <summary>Tiêu đề tóm tắt của phiên tư vấn (tự động sinh từ câu hỏi đầu tiên).</summary>
    public string SessionTitle { get; set; } = "Tư vấn SIM số đẹp";

    /// <summary>Ngân sách tối thiểu mà khách hàng mong muốn (VND).</summary>
    public decimal? TargetBudgetMin { get; set; }

    /// <summary>Ngân sách tối đa mà khách hàng có thể chi trả (VND).</summary>
    public decimal? TargetBudgetMax { get; set; }

    /// <summary>Năm sinh của khách hàng để tính cung mệnh ngũ hành.</summary>
    public int? UserBirthYear { get; set; }

    /// <summary>Ngành nghề / lĩnh vực kinh doanh của khách hàng (BĐS, Tài chính, Vận tải, Xây dựng...).</summary>
    public string? UserCareer { get; set; }

    /// <summary>Mệnh phong thủy được xác định (KIM, MOC, THUY, HOA, THO).</summary>
    public FengShuiElement? FengShuiElement { get; set; }

    // --- Navigation Properties ---
    /// <summary>Thông tin tài khoản người dùng (nếu có).</summary>
    public virtual UserAccount? UserAccount { get; set; }

    /// <summary>Danh sách các tin nhắn trong phiên tư vấn.</summary>
    public virtual ICollection<AiConsultationMessage> Messages { get; set; } = new List<AiConsultationMessage>();
}
