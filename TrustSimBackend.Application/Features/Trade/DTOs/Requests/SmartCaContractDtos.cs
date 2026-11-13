using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Truy vấn danh sách chứng thư số VNPT SmartCA của người dùng cho giao dịch hợp đồng.
/// </summary>
public record GetSmartCaCertificatesQuery(
    Guid TradeId,
    Guid UserId,
    string? CitizenId = null,
    string? SerialNumber = null) : IRequest<IReadOnlyList<SmartCaCertificateDto>>;

/// <summary>
/// Request body từ Client khi yêu cầu khởi tạo ký số hợp đồng bằng VNPT SmartCA.
/// </summary>
public record InitiateSmartCaSignRequest(
    string? CitizenId = null,
    string? SerialNumber = null,
    string? Password = null,
    string? Otp = null);

/// <summary>
/// Command khởi tạo giao dịch ký số hợp đồng chuyển nhượng SIM qua cổng VNPT SmartCA (v1 App Confirm hoặc v2 SmartOTP).
/// </summary>
public record InitiateSmartCaContractSignCommand(
    Guid TradeId,
    Guid UserId,
    string? CitizenId = null,
    string? SerialNumber = null,
    string? UserPassword = null,
    string? Otp = null,
    string? IpAddress = null,
    string? UserAgent = null) : IRequest<InitiateSmartCaContractSignResultDto>;

/// <summary>
/// Kết quả khởi tạo phiên ký số hợp đồng qua VNPT SmartCA.
/// </summary>
public record InitiateSmartCaContractSignResultDto(
    Guid TradeId,
    string TradeTransactionCode,
    string SignatureRole,
    string TransactionId,
    string TranCode,
    string DocId,
    string DataToBeSignedHex,
    string SerialNumber,
    string StatusMessage,
    int ExpiredInSeconds,
    bool IsCompletedImmediately = false,
    SignContractResultDto? CompletedSignResult = null)
{
    /// <summary>
    /// Deep Link dùng để kích hoạt trực tiếp ứng dụng VNPT SmartCA trên thiết bị di động.
    /// </summary>
    public string DeepLinkUrl => $"vnptsmartca://sign?tranCode={TranCode}";
}

/// <summary>
/// Command kiểm tra trạng thái hoặc xử lý Webhook Callback hoàn tất ký số hợp đồng từ VNPT SmartCA.
/// </summary>
public record CompleteSmartCaContractSignCommand(
    string TranCodeOrTransactionId,
    Guid? TradeId = null,
    Guid? UserId = null,
    string? SerialNumber = null,
    string? WebhookSignatureValueBase64 = null,
    string? WebhookDocId = null,
    int? WebhookStatusCode = null,
    string? IpAddress = null,
    string? UserAgent = null) : IRequest<SignContractResultDto>;

/// <summary>
/// Payload Webhook do VNPT SmartCA gọi về SP sau khi người dùng xác nhận ký trên App (Mục 3.5 Tài liệu SmartCA v4.0).
/// </summary>
public class SmartCaWebhookRequest
{
    [JsonPropertyName("sp_id")]
    public string? SpId { get; set; }

    [JsonPropertyName("status_code")]
    public int StatusCode { get; set; } = 200;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("tran_code")]
    public string? TranCode { get; set; }

    [JsonPropertyName("signed_files")]
    public List<SmartCaWebhookSignedFileItem>? SignedFiles { get; set; }
}

public class SmartCaWebhookSignedFileItem
{
    [JsonPropertyName("doc_id")]
    public string? DocId { get; set; }

    [JsonPropertyName("signature_value")]
    public string? SignatureValue { get; set; }

    [JsonPropertyName("timestamp_signature")]
    public string? TimestampSignature { get; set; }
}

/// <summary>
/// Bộ nhớ tạm đồng bộ phiên giao dịch ký hợp đồng SmartCA đang chờ người dùng xác nhận trên App.
/// </summary>
public sealed record SmartCaPendingContractSession(
    Guid TradeId,
    Guid UserId,
    SignatureRole Role,
    string CitizenId,
    string SerialNumber,
    string TransactionId,
    string TranCode,
    string DocId,
    string DataToBeSignedHex,
    string IpAddress,
    string UserAgent,
    DateTime CreatedAt);

public static class SmartCaPendingSessionStore
{
    private static readonly ConcurrentDictionary<string, SmartCaPendingContractSession> ByKey = new(StringComparer.OrdinalIgnoreCase);

    public static void Save(SmartCaPendingContractSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.TranCode))
        {
            ByKey[session.TranCode] = session;
        }

        if (!string.IsNullOrWhiteSpace(session.TransactionId))
        {
            ByKey[session.TransactionId] = session;
        }
    }

    public static SmartCaPendingContractSession? TryGet(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return ByKey.TryGetValue(key.Trim(), out var session) ? session : null;
    }
}
