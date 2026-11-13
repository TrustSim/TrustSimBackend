using Microsoft.AspNetCore.Http;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Form request nộp nhiều hình ảnh bằng chứng sang tên SIM từ multipart/form-data.
/// </summary>
public class SubmitMultipleTransferProofsFormRequest
{
    /// <summary>Danh sách tệp hình ảnh tải lên.</summary>
    public List<IFormFile> Files { get; set; } = new();

    /// <summary>Loại bằng chứng: SELLER_HANDOVER (Bên Bán) hoặc BUYER_RECEIPT (Bên Mua).</summary>
    public TransferProofType ProofType { get; set; } = TransferProofType.SELLER_HANDOVER;

    /// <summary>Ghi chú mô tả thêm về hình ảnh/thủ tục.</summary>
    public string? Description { get; set; }
}
