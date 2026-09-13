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

    /// <summary>Trạng thái hiện tại của SIM: UNVERIFIED, AVAILABLE, LISTED, IN_AUCTION, TRANSFERRING, TRANSFERRED, SUSPENDED.</summary>
    public SimStatus SimStatus { get; set; } = SimStatus.AVAILABLE;

    /// <summary>
    /// Giá trị định giá của SIM (VND). Khởi tạo mặc định bằng 0 tại thời điểm xác minh sở hữu.
    /// Giá trị này sẽ được cập nhật khi thực hiện nghiệp vụ Định giá SIM (Valuation).
    /// </summary>
    public decimal Price { get; set; } = 0m;

    // --- Foreign Keys ---
    /// <summary>ID của đầu số (<see cref="PhonePrefix"/>) mà số SIM này thuộc về (VD: đầu số "090" của Mobifone).</summary>
    public Guid PhonePrefixId { get; set; }

    /// <summary>
    /// ID của kiểu định dạng số (<see cref="FormatRhythm"/>) — VD: Sảnh, Lặp, Gánh, Ngũ quý...
    /// Mặc định là null khi mới khởi tạo/xác minh, được gán vào thời điểm định giá SIM.
    /// </summary>
    public Guid? FormatRhythmId { get; set; }

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