namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Yêu cầu Admin duyệt hồ sơ bằng chứng sang tên SIM từ Bên Bán hoặc Bên Mua.
/// </summary>
public class ReviewSimTransferProofRequest
{
    /// <summary>Đối tượng duyệt: SELLER hoặc BUYER.</summary>
    public string TargetParty { get; set; } = "SELLER";

    /// <summary>True: Duyệt hồ sơ, False: Từ chối yêu cầu nộp lại.</summary>
    public bool IsApproved { get; set; }

    /// <summary>Lý do từ chối (nếu có).</summary>
    public string? RejectionReason { get; set; }
}
