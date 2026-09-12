using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Sim;

/// <summary>
/// Đại diện cho một số SIM/điện thoại trên sàn giao dịch TrustSim.
/// Mỗi số SIM có trạng thái riêng và gắn với một đầu số (<see cref="PhonePrefix"/>) và định dạng số (<see cref="FormatRhythm"/>).
/// Quyền sở hữu được quản lý qua <see cref="SimOwnership"/>.
/// </summary>
public class SimNumber : FullAuditableEntity
{

    /// <summary>Chuỗi số điện thoại đầy đủ (VD: "0909123456"). Duy nhất trong toàn hệ thống.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Trạng thái hiện tại của SIM: AVAILABLE, LISTED, IN_AUCTION, TRANSFERRED, SUSPENDED.</summary>
    public SimStatus SimStatus { get; set; } = SimStatus.AVAILABLE;

    // --- Foreign Keys ---
    /// <summary>ID của đầu số (<see cref="PhonePrefix"/>) mà số SIM này thuộc về (VD: đầu số "090" của Mobifone).</summary>
    public Guid PhonePrefixId { get; set; }

    /// <summary>ID của kiểu định dạng số (<see cref="FormatRhythm"/>) — VD: Sảnh, Lặp, Gánh, Ngũ quý...</summary>
    public Guid FormatRhythmId { get; set; }

    // --- Navigation Properties ---
    /// <summary>Đầu số điện thoại của nhà mạng mà SIM này thuộc về.</summary>
    public virtual PhonePrefix? PhonePrefix { get; set; }

    /// <summary>Kiểu định dạng (nhịp số) của số SIM này, dùng để phân loại và định giá.</summary>
    public virtual FormatRhythm? FormatRhythm { get; set; }

    /// <summary>Lịch sử tất cả các lần sở hữu SIM này (mỗi lần chuyển nhượng là một bản ghi mới).</summary>
    public virtual ICollection<SimOwnership> SimOwnerships { get; set; } = new List<SimOwnership>();

    /// <summary>Danh sách ánh xạ phân loại danh mục của SIM này (N-N với <see cref="SimCategory"/>).</summary>
    public virtual ICollection<SimCategoryMapping> SimCategoryMappings { get; set; } = new List<SimCategoryMapping>();

    public virtual ICollection<TradeTransaction> TradeTransactions {get;set;}= new List<TradeTransaction>();
}