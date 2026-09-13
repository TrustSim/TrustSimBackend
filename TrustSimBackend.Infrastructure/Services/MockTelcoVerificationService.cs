using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Dịch vụ giả lập cổng viễn thông nhà mạng phục vụ Đồ án Tốt nghiệp (Simulation Service).
/// Tái hiện hành vi gọi API viễn thông thực tế kèm độ trễ mạng (Network Latency) và các kịch bản kiểm thử (Test Cases).
/// </summary>
public class MockTelcoVerificationService : ITelcoVerificationService
{
    private readonly ILogger<MockTelcoVerificationService> _logger;

    public MockTelcoVerificationService(ILogger<MockTelcoVerificationService> logger)
    {
        _logger = logger;
    }

    public async Task<TelcoVerificationResult> VerifyOwnershipAsync(
        string carrierCode,
        string phoneNumber,
        string identityNumber,
        string fullName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[MockTelco] Bắt đầu gọi API nhà mạng {Carrier} để đối soát SIM {Phone} với CCCD {Identity} ({FullName})",
            carrierCode, phoneNumber, identityNumber, fullName);

        // 1. Giả lập độ trễ mạng (Network Latency) 1000ms như khi gọi External Gateway thực tế
        await Task.Delay(1000, cancellationToken);

        // 2. Kịch bản Test 1: Giả lập lỗi nhà mạng từ chối nếu CCCD chứa chuỗi '000000' hoặc số điện thoại có đuôi '9999'
        if (identityNumber.Contains("000000") || phoneNumber.EndsWith("9999"))
        {
            _logger.LogWarning("[MockTelco] Kịch bản Test Thất bại: Số CCCD hoặc Số SIM nằm trong danh sách giả lập từ chối.");
            return TelcoVerificationResult.Failure(
                carrierCode,
                phoneNumber,
                $"Nhà mạng {carrierCode} phản hồi: Thông tin CCCD '{identityNumber}' không trùng khớp với chủ thuê bao đã đăng ký.");
        }

        // 3. Kịch bản Test 2: Giả lập lỗi số thuê bao chưa kích hoạt / khóa 2 chiều nếu số điện thoại có đuôi '8888'
        if (phoneNumber.EndsWith("8888"))
        {
            _logger.LogWarning("[MockTelco] Kịch bản Test Thất bại: Số SIM đang bị khóa 2 chiều tại nhà mạng.");
            return TelcoVerificationResult.Failure(
                carrierCode,
                phoneNumber,
                $"Nhà mạng {carrierCode} phản hồi: Thuê bao đang trong trạng thái khóa hoặc chưa hoàn thiện hồ sơ TTTB Nghị định 49.");
        }

        // 4. Kịch bản Chuẩn (Mặc định): Xác thực thành công thông tin trùng khớp với Nhà mạng
        var transactionId = $"TELCO-{carrierCode}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        _logger.LogInformation("[MockTelco] Xác thực thành công qua nhà mạng {Carrier}. Mã giao dịch: {TxId}", carrierCode, transactionId);

        return TelcoVerificationResult.Success(carrierCode, phoneNumber, identityNumber, fullName, transactionId);
    }
}
