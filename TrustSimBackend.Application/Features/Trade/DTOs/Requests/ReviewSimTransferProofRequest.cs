namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Yêu cầu Admin duyệt hoặc từ chối một bộ bằng chứng chuyển nhượng SIM.
/// </summary>
public class ReviewSimTransferProofRequest
{
    /// <summary>True: Duyệt chấp thuận bộ bằng chứng, False: Từ chối toàn bộ ảnh trong bộ bằng chứng.</summary>
    public bool IsApproved { get; set; }

    /// <summary>Lý do từ chối (bắt buộc khi IsApproved = false).</summary>
    public string? RejectionReason { get; set; }
}
