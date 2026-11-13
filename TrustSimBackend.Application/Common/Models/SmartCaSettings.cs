namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Cấu hình tích hợp dịch vụ ký số từ xa VNPT SmartCA theo Quyết định 769/QĐ-BTTTT (Phiên bản 4.0).
/// </summary>
public class SmartCaSettings
{
    public const string SectionName = "SmartCA";
    public const string AltSectionName = "SmartCaSettings";

    /// <summary>
    /// Tên nhà cung cấp dịch vụ chứng thực chữ ký số công cộng.
    /// </summary>
    public string ProviderName { get; set; } = "VNPT_SMARTCA";

    /// <summary>
    /// Môi trường kết nối: UAT hoặc PRODUCTION.
    /// </summary>
    public string Environment { get; set; } = "UAT";

    /// <summary>
    /// Địa chỉ Gateway API mặc định (UAT: https://rmgateway.vnptit.vn/sca/sp769).
    /// </summary>
    public string BaseUrl { get; set; } = "https://rmgateway.vnptit.vn/sca/sp769";

    /// <summary>
    /// Địa chỉ Gateway API môi trường UAT của VNPT SmartCA.
    /// </summary>
    public string UatBaseUrl { get; set; } = "https://rmgateway.vnptit.vn/sca/sp769";

    /// <summary>
    /// Địa chỉ Gateway API môi trường Production của VNPT SmartCA.
    /// </summary>
    public string ProductionBaseUrl { get; set; } = "https://gwsca.vnpt.vn/sca/sp769";

    /// <summary>
    /// Tên tài khoản đối tác tích hợp (Client_ID / sp_id) do VNPT cấp.
    /// </summary>
    public string SpId { get; set; } = "4184-637127995547330633.apps.signserviceapi.com";

    /// <summary>
    /// Mật khẩu xác thực đối tác tích hợp (Client_Secret / sp_password) do VNPT cấp.
    /// </summary>
    public string SpPassword { get; set; } = "NGNhMzdmOGE-OGM2Mi00MTg0";

    /// <summary>
    /// Khóa bí mật xác thực Webhook Callback từ máy chủ VNPT SmartCA.
    /// </summary>
    public string WebhookSecret { get; set; } = "TrustSim_SmartCA_Webhook_Secret_2026";

    /// <summary>
    /// Thời gian tối đa chờ người dùng xác nhận ký trên ứng dụng di động VNPT SmartCA (mặc định: 300 giây = 5 phút).
    /// </summary>
    public int AppConfirmTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Thời gian chờ HTTP Request tới cổng VNPT SmartCA (mặc định: 15 giây).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Alias tương thích cho HttpTimeoutSeconds.
    /// </summary>
    public int HttpTimeoutSeconds
    {
        get => TimeoutSeconds;
        set => TimeoutSeconds = value;
    }

    /// <summary>
    /// Bật chế độ mô phỏng Sandbox thông minh trong môi trường Dev/Test khi test bằng CCCD nội bộ.
    /// </summary>
    public bool EnableSandboxSimulation { get; set; } = true;

    /// <summary>
    /// Lấy URL Gateway thực tế dựa theo cấu hình Environment hoặc BaseUrl.
    /// </summary>
    public string ResolveGatewayBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            return BaseUrl.TrimEnd('/');
        }

        if (string.Equals(Environment, "PRODUCTION", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(ProductionBaseUrl))
        {
            return ProductionBaseUrl.TrimEnd('/');
        }

        return UatBaseUrl.TrimEnd('/');
    }
}
