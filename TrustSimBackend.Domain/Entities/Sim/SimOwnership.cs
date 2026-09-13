using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Sim;

/// <summary>
/// Thực thể kết hợp (Junction Entity) ghi lại lịch sử quyền sở hữu giữa một <see cref="UserAccount"/> và một <see cref="SimNumber"/>.
/// Mỗi lần SIM đổi chủ (mua lại, trúng đấu giá, Admin gán) sẽ tạo ra một bản ghi mới.
/// Không bao giờ bị xóa — là bằng chứng pháp lý về chuỗi quyền sở hữu SIM.
/// </summary>
public class SimOwnership : AuditableEntity
{

    /// <summary>Trạng thái của quyền sở hữu: UNVERIFIED (chưa xác minh), ACTIVE (đang sở hữu), ENDED (đã chuyển nhượng), DISPUTED, REVOKED.</summary>
    public OwnershipStatus OwnershipStatus { get; set; } = OwnershipStatus.UNVERIFIED;

    /// <summary>Hình thức nhận quyền sở hữu: INITIAL_VERIFICATION (xác minh chính chủ lần đầu), AUCTION_TRANSFER (trúng đấu giá), ADMIN_ASSIGNMENT.</summary>
    public AcquistionType AcquistionType { get; set; } = AcquistionType.INITIAL_VERIFICATION;

    // --- Foreign Keys ---
    /// <summary>ID của người dùng đang hoặc đã từng sở hữu số SIM này.</summary>
    public Guid UserAccountId { get; set; }

    /// <summary>ID của số SIM được sở hữu trong bản ghi này.</summary>
    public Guid SimNumberId { get; set; }

    // --- Navigation Properties ---
    /// <summary>Tài khoản người dùng sở hữu số SIM này.</summary>
    public virtual UserAccount? UserAccount { get; set; }

    /// <summary>Số SIM được sở hữu.</summary>
    public virtual SimNumber? SimNumber { get; set; }

    /// <summary>Danh sách lịch sử các lần xác minh quyền sở hữu (1-N) — mỗi lần nộp bằng chứng là một bản ghi.</summary>
    public virtual ICollection<SimVerification> SimVerifications { get; set; } = new List<SimVerification>();
}