namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Interface dịch vụ đẩy yêu cầu ký số niêm phong hợp đồng vào hàng đợi phân tán (Redis Stream).
/// </summary>
public interface IContractSigningQueueService
{
    /// <summary>
    /// Đẩy tác vụ ký số niêm phong PAdES vào Redis Stream "stream:contracts:sign".
    /// </summary>
    /// <param name="tradeId">ID giao dịch chuyển nhượng.</param>
    /// <param name="isAutoEnforced">Cờ đánh dấu niêm phong theo cơ chế cưỡng chế ủy quyền.</param>
    /// <param name="cancellationToken">CancellationToken.</param>
    Task EnqueueContractSigningTaskAsync(Guid tradeId, bool isAutoEnforced = false, CancellationToken cancellationToken = default);
}
