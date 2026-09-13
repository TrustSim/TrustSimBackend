using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Domain.Entities.Ai;

/// <summary>
/// Đại diện cho một bản ghi lịch sử định giá SIM bằng Trí tuệ Nhân tạo (Hybrid AI Valuation).
/// Lưu lại điểm toán học cơ sở, khoảng giá khuyến nghị, lời giải trình từ NVIDIA LLM và các tài liệu tham chiếu RAG.
/// </summary>
public class SimValuation : FullAuditableEntity
{
    /// <summary>ID của số SIM được định giá.</summary>
    public Guid SimNumberId { get; set; }

    /// <summary>ID của người dùng hoặc Admin yêu cầu định giá.</summary>
    public Guid? UserAccountId { get; set; }

    /// <summary>Mức giá đề xuất tối ưu do AI tính toán (VND).</summary>
    public decimal SuggestedPrice { get; set; }

    /// <summary>Mức giá sàn ước tính tối thiểu (VND).</summary>
    public decimal MinPrice { get; set; }

    /// <summary>Mức giá trần ước tính tối đa (VND).</summary>
    public decimal MaxPrice { get; set; }

    /// <summary>Độ tin cậy của mô hình AI (thang điểm 0.0 - 1.0).</summary>
    public float ConfidenceScore { get; set; }

    /// <summary>Điểm số định giá tính từ mô hình toán học cơ sở (Tầng 2).</summary>
    public decimal BaselinePrice { get; set; }

    /// <summary>Hệ số điều chỉnh xu hướng thị trường từ RAG (Tầng 3).</summary>
    public float MarketMultiplier { get; set; } = 1.0f;

    /// <summary>Bài phân tích & giải trình định giá chuyên sâu từ NVIDIA LLM (Tầng 4).</summary>
    public string ValuationReasoning { get; set; } = string.Empty;

    /// <summary>Tóm tắt ý nghĩa phong thủy và bản mệnh phù hợp của SIM.</summary>
    public string? FengShuiAnalysis { get; set; }

    /// <summary>Danh sách ID các DocumentChunks được RAG trích xuất làm căn cứ định giá (JSON Array).</summary>
    public string? ReferencedChunkIds { get; set; }

    /// <summary>Tên mô hình AI LLM đã sử dụng (VD: meta/llama-3.3-70b-instruct).</summary>
    public string AiModelName { get; set; } = "meta/llama-3.3-70b-instruct";

    /// <summary>Cờ đánh dấu mức giá này đã được chủ sở hữu áp dụng trực tiếp vào SimNumber.Price hay chưa.</summary>
    public bool AppliedToSim { get; set; } = false;

    // --- Navigation Properties ---
    /// <summary>Số SIM tương ứng.</summary>
    public virtual SimNumber? SimNumber { get; set; }

    /// <summary>Người dùng yêu cầu định giá.</summary>
    public virtual UserAccount? UserAccount { get; set; }
}
