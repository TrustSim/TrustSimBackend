using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ tích hợp chữ ký số từ xa VNPT SmartCA (Phiên bản 4.0 - Quyết định 769/QĐ-BTTTT).
/// Hỗ trợ đầy đủ:
/// - v1/credentials/get_certificate
/// - v1/signatures/sign (Xác nhận qua App VNPT SmartCA)
/// - v1/signatures/sign/{tranId}/status (Polling trạng thái ký)
/// - v2/signatures/sign &amp; v2/signatures/confirm (Ký tích hợp SmartCA TH qua Smart OTP / TOTP)
/// </summary>
public class SmartCaService : ISmartCaService
{
    private static readonly ConcurrentDictionary<string, SandboxSignSession> _sandboxSessions = new(StringComparer.OrdinalIgnoreCase);

    private readonly HttpClient _httpClient;
    private readonly SmartCaSettings _settings;
    private readonly ILogger<SmartCaService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SmartCaService(
        HttpClient httpClient,
        IOptions<SmartCaSettings> settingsOptions,
        ILogger<SmartCaService> logger)
    {
        _httpClient = httpClient;
        _settings = settingsOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SmartCaCertificateDto>> GetUserCertificatesAsync(
        string citizenIdOrTaxCode,
        string? serialNumber = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(citizenIdOrTaxCode))
        {
            throw new BadRequestException("Số CCCD/CMND hoặc Mã số thuế của thuê bao SmartCA không được để trống.");
        }

        var normalizedUserId = citizenIdOrTaxCode.Trim();
        var transactionId = $"TS_SCA_{Guid.NewGuid():N}";
        var baseUrl = _settings.ResolveGatewayBaseUrl();
        var endpoint = $"{baseUrl}/v1/credentials/get_certificate";

        var payload = new
        {
            sp_id = _settings.SpId,
            sp_password = _settings.SpPassword,
            user_id = normalizedUserId,
            serial_number = serialNumber ?? string.Empty,
            transaction_id = transactionId
        };

        try
        {
            using var response = await PostJsonAsync(endpoint, payload, cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

            var parsed = JsonSerializer.Deserialize<SmartCaEnvelope<SmartCaGetCertData>>(rawJson, JsonOptions);
            var statusCode = parsed?.StatusCode ?? (int)response.StatusCode;
            var message = parsed?.Message ?? response.ReasonPhrase ?? "Unknown";

            EnsureSmartCaSuccessStatus(statusCode, message);

            var rawCerts = parsed?.Data?.UserCertificates ?? new List<SmartCaRawCertificate>();
            var results = rawCerts
                .Select(c => new SmartCaCertificateDto(
                    CertId: c.CertId ?? Guid.NewGuid().ToString(),
                    SerialNumber: c.SerialNumber ?? string.Empty,
                    ServiceType: c.ServiceType ?? "SMARTCA",
                    ServiceName: c.ServiceName ?? "SMARTCA PERSONAL",
                    CertStatus: c.CertStatus ?? "Đang hoạt động",
                    CertSubject: c.CertSubject ?? $"CN={normalizedUserId}",
                    ValidFrom: ParseNullableDateTime(c.CertValidFrom),
                    ValidTo: ParseNullableDateTime(c.CertValidTo),
                    CertDataBase64: c.CertData ?? string.Empty,
                    CaCertBase64: c.ChainData?.CaCert,
                    RootCertBase64: c.ChainData?.RootCert,
                    TransactionId: c.TransactionId ?? transactionId))
                .ToList();

            return results;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && _settings.EnableSandboxSimulation)
        {
            _logger.LogWarning(
                ex,
                "[VNPT SmartCA] Không thể kết nối mạng tới cổng UAT ({Endpoint}). Chuyển sang chế độ Sandbox Simulation cho CCCD '{UserId}'.",
                endpoint, normalizedUserId);

            return new List<SmartCaCertificateDto>
            {
                BuildSandboxCertificate(normalizedUserId, serialNumber, transactionId)
            };
        }
    }

    public async Task<SmartCaSignInitiatedResultDto> InitiateSignHashAsync(
        SmartCaSignHashRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserIdCitizenId))
        {
            throw new BadRequestException("Thiếu số CCCD/CMND của người ký SmartCA (user_id).");
        }

        if (string.IsNullOrWhiteSpace(request.DataToBeSignedHex) || !IsValidHex(request.DataToBeSignedHex.Trim()))
        {
            throw new BadRequestException("Chuỗi băm tài liệu cần ký (data_to_be_signed) phải là chuỗi Hex SHA-256 hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(request.DocId))
        {
            throw new BadRequestException("Mã tài liệu yêu cầu ký số (doc_id) không được để trống.");
        }

        var transactionId = string.IsNullOrWhiteSpace(request.TransactionId)
            ? $"TS_SCA_{Guid.NewGuid():N}"
            : request.TransactionId.Trim();

        var isV2Integrated = !string.IsNullOrWhiteSpace(request.UserPassword) && !string.IsNullOrWhiteSpace(request.Otp);
        var baseUrl = _settings.ResolveGatewayBaseUrl();
        var endpoint = isV2Integrated
            ? $"{baseUrl}/v2/signatures/sign"
            : $"{baseUrl}/v1/signatures/sign";

        var timeStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "Z";

        object payload = isV2Integrated
            ? new
            {
                sp_id = _settings.SpId,
                sp_password = _settings.SpPassword,
                user_id = request.UserIdCitizenId.Trim(),
                password = request.UserPassword,
                otp = request.Otp,
                transaction_id = transactionId,
                transaction_desc = request.TransactionDesc ?? "Ky hop dong chuyen nhuong SIM TrustSim",
                serial_number = request.SerialNumber,
                time_stamp = timeStamp,
                sign_files = new[]
                {
                    new
                    {
                        file_type = request.FileType ?? "pdf",
                        data_to_be_signed = request.DataToBeSignedHex.Trim().ToLowerInvariant(),
                        doc_id = request.DocId.Trim(),
                        sign_type = request.SignType ?? "hash"
                    }
                }
            }
            : new
            {
                sp_id = _settings.SpId,
                sp_password = _settings.SpPassword,
                user_id = request.UserIdCitizenId.Trim(),
                transaction_id = transactionId,
                transaction_desc = request.TransactionDesc ?? "Ky hop dong chuyen nhuong SIM TrustSim",
                serial_number = request.SerialNumber,
                time_stamp = timeStamp,
                sign_files = new[]
                {
                    new
                    {
                        file_type = request.FileType ?? "pdf",
                        data_to_be_signed = request.DataToBeSignedHex.Trim().ToLowerInvariant(),
                        doc_id = request.DocId.Trim(),
                        sign_type = request.SignType ?? "hash"
                    }
                }
            };

        try
        {
            using var response = await PostJsonAsync(endpoint, payload, cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

            var parsed = JsonSerializer.Deserialize<SmartCaEnvelope<SmartCaSignInitData>>(rawJson, JsonOptions);
            var statusCode = parsed?.StatusCode ?? (int)response.StatusCode;
            var message = parsed?.Message ?? response.ReasonPhrase ?? "sig_wait_for_user_confirm";

            EnsureSmartCaSuccessStatus(statusCode, message);

            var tranCode = parsed?.Data?.TranCode ?? parsed?.Data?.TransactionId ?? transactionId;
            return new SmartCaSignInitiatedResultDto(
                IsSuccess: true,
                StatusCode: statusCode == 0 ? 200 : statusCode,
                Message: message,
                TransactionId: parsed?.Data?.TransactionId ?? transactionId,
                TranCode: tranCode,
                Sad: parsed?.Data?.Sad,
                ExpiredIn: parsed?.Data?.ExpiredIn);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && _settings.EnableSandboxSimulation)
        {
            var sandboxTranCode = $"SANDBOX_{Guid.NewGuid():N}";
            var sandboxSad = isV2Integrated
                ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"SANDBOX_SAD:{sandboxTranCode}:{request.DocId}"))
                : null;

            _sandboxSessions[sandboxTranCode] = new SandboxSignSession(
                TransactionId: transactionId,
                TranCode: sandboxTranCode,
                UserId: request.UserIdCitizenId.Trim(),
                SerialNumber: string.IsNullOrWhiteSpace(request.SerialNumber) ? "54010101493c47d39f8a84b30ed55191" : request.SerialNumber,
                DocId: request.DocId.Trim(),
                DataToBeSignedHex: request.DataToBeSignedHex.Trim().ToLowerInvariant(),
                Sad: sandboxSad,
                CreatedAt: DateTime.UtcNow);

            _logger.LogInformation(
                "[VNPT SmartCA Sandbox] Đã tạo giao dịch ký mô phỏng TranCode={TranCode}, DocId={DocId}",
                sandboxTranCode, request.DocId);

            return new SmartCaSignInitiatedResultDto(
                IsSuccess: true,
                StatusCode: 200,
                Message: "sig_wait_for_user_confirm",
                TransactionId: transactionId,
                TranCode: sandboxTranCode,
                Sad: sandboxSad,
                ExpiredIn: _settings.AppConfirmTimeoutSeconds);
        }
    }

    public async Task<SmartCaSignStatusResultDto> GetSignTransactionStatusAsync(
        string tranCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tranCode))
        {
            throw new BadRequestException("Mã giao dịch kiểm tra trạng thái ký (tranCode) không được để trống.");
        }

        var cleanTranCode = tranCode.Trim();
        if (_sandboxSessions.TryGetValue(cleanTranCode, out var sandboxSession))
        {
            var sigBase64 = GenerateSandboxSignatureBase64(sandboxSession.DataToBeSignedHex, sandboxSession.SerialNumber);
            return new SmartCaSignStatusResultDto(
                IsCompleted: true,
                StatusCode: 200,
                Message: "SUCCESS",
                TransactionId: sandboxSession.TransactionId,
                Signatures: new List<SmartCaSignatureItemDto>
                {
                    new(sandboxSession.DocId, sigBase64, null)
                });
        }

        var baseUrl = _settings.ResolveGatewayBaseUrl();
        var endpoint = $"{baseUrl}/v1/signatures/sign/{Uri.EscapeDataString(cleanTranCode)}/status";

        var payload = new
        {
            sp_id = _settings.SpId,
            sp_password = _settings.SpPassword,
            transaction_id = cleanTranCode
        };

        using var response = await PostJsonAsync(endpoint, payload, cancellationToken);
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

        var parsed = JsonSerializer.Deserialize<SmartCaEnvelope<SmartCaSignStatusData>>(rawJson, JsonOptions);
        var statusCode = parsed?.StatusCode ?? (int)response.StatusCode;
        var message = parsed?.Message ?? response.ReasonPhrase ?? string.Empty;

        EnsureSmartCaSuccessStatus(statusCode, message);

        var signatures = (parsed?.Data?.Signatures ?? new List<SmartCaRawSignatureItem>())
            .Select(s => new SmartCaSignatureItemDto(
                DocId: s.DocId ?? string.Empty,
                SignatureValueBase64: s.SignatureValue,
                TimestampSignatureBase64: s.TimestampSignature))
            .ToList();

        var isCompleted = signatures.Count > 0 && signatures.All(s => !string.IsNullOrWhiteSpace(s.SignatureValueBase64));

        return new SmartCaSignStatusResultDto(
            IsCompleted: isCompleted,
            StatusCode: statusCode == 0 ? 200 : statusCode,
            Message: message,
            TransactionId: parsed?.Data?.TransactionId ?? cleanTranCode,
            Signatures: signatures);
    }

    public async Task<SmartCaSignStatusResultDto> ConfirmIntegratedSignAsync(
        string citizenIdOrTaxCode,
        string userPassword,
        string transactionId,
        string sad,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(citizenIdOrTaxCode)
            || string.IsNullOrWhiteSpace(userPassword)
            || string.IsNullOrWhiteSpace(transactionId)
            || string.IsNullOrWhiteSpace(sad))
        {
            throw new BadRequestException("Thiếu thông tin xác nhận giao dịch ký tích hợp SmartCA TH (user_id, password, transaction_id, sad).");
        }

        var matchingSandbox = _sandboxSessions.Values.FirstOrDefault(s =>
            string.Equals(s.Sad, sad, StringComparison.Ordinal) ||
            string.Equals(s.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase));

        if (matchingSandbox != null)
        {
            var sigBase64 = GenerateSandboxSignatureBase64(matchingSandbox.DataToBeSignedHex, matchingSandbox.SerialNumber);
            return new SmartCaSignStatusResultDto(
                IsCompleted: true,
                StatusCode: 200,
                Message: "Success",
                TransactionId: matchingSandbox.TransactionId,
                Signatures: new List<SmartCaSignatureItemDto>
                {
                    new(matchingSandbox.DocId, sigBase64, null)
                });
        }

        var baseUrl = _settings.ResolveGatewayBaseUrl();
        var endpoint = $"{baseUrl}/v2/signatures/confirm";

        var payload = new
        {
            sp_id = _settings.SpId,
            sp_password = _settings.SpPassword,
            user_id = citizenIdOrTaxCode.Trim(),
            password = userPassword,
            transaction_id = transactionId.Trim(),
            sad = sad.Trim()
        };

        using var response = await PostJsonAsync(endpoint, payload, cancellationToken);
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

        var parsed = JsonSerializer.Deserialize<SmartCaEnvelope<SmartCaSignStatusData>>(rawJson, JsonOptions);
        var statusCode = parsed?.StatusCode ?? (int)response.StatusCode;
        var message = parsed?.Message ?? response.ReasonPhrase ?? string.Empty;

        EnsureSmartCaSuccessStatus(statusCode, message);

        var signatures = (parsed?.Data?.Signatures ?? new List<SmartCaRawSignatureItem>())
            .Select(s => new SmartCaSignatureItemDto(
                DocId: s.DocId ?? string.Empty,
                SignatureValueBase64: s.SignatureValue,
                TimestampSignatureBase64: s.TimestampSignature))
            .ToList();

        var isCompleted = signatures.Count > 0 && signatures.All(s => !string.IsNullOrWhiteSpace(s.SignatureValueBase64));

        return new SmartCaSignStatusResultDto(
            IsCompleted: isCompleted,
            StatusCode: statusCode == 0 ? 200 : statusCode,
            Message: message,
            TransactionId: parsed?.Data?.TransactionId ?? transactionId,
            Signatures: signatures);
    }

    private async Task<HttpResponseMessage> PostJsonAsync(string endpoint, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _httpClient.PostAsync(endpoint, content, cancellationToken);
    }

    private static void EnsureSmartCaSuccessStatus(int statusCode, string message)
    {
        if (statusCode == 200 || statusCode == 0)
        {
            return;
        }

        if (statusCode == (int)HttpStatusCode.BadRequest)
        {
            throw new BadRequestException($"[VNPT SmartCA 400 BAD_REQUEST] Dữ liệu yêu cầu ký số không hợp lệ: {message}");
        }

        if (statusCode == (int)HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException($"[VNPT SmartCA 401 SP_CREDENTIAL_INVALID] Thông tin xác thực tài khoản SP hoặc thuê bao không hợp lệ: {message}");
        }

        if (statusCode == (int)HttpStatusCode.Forbidden)
        {
            throw new ForbiddenAccessException($"[VNPT SmartCA 403 CREDENTIAL_STATUS_INVALID] Chứng thư số thuê bao không hợp lệ, chưa kích hoạt hoặc đã hết hạn: {message}");
        }

        throw new BadRequestException($"[VNPT SmartCA {statusCode}] Lỗi xử lý từ cổng ký số từ xa VNPT SmartCA: {message}");
    }

    private static bool IsValidHex(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length % 2 != 0)
        {
            return false;
        }

        foreach (var c in input)
        {
            bool isHexChar = (c >= '0' && c <= '9')
                          || (c >= 'a' && c <= 'f')
                          || (c >= 'A' && c <= 'F');
            if (!isHexChar)
            {
                return false;
            }
        }

        return true;
    }

    private static DateTime? ParseNullableDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, out var dt) ? dt.ToUniversalTime() : null;
    }

    private static SmartCaCertificateDto BuildSandboxCertificate(string citizenId, string? requestedSerial, string transactionId)
    {
        using var rsa = RSA.Create(2048);
        var subject = $"C=VN, ST=HÀ NỘI, CN=Thuê bao SmartCA ({citizenId}), UID=CCCD:{citizenId}";
        var req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var now = DateTimeOffset.UtcNow;
        using var cert = req.CreateSelfSigned(now.AddDays(-1), now.AddYears(2));

        var certBase64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert));
        var serial = !string.IsNullOrWhiteSpace(requestedSerial)
            ? requestedSerial
            : "54010101493c47d39f8a84b30ed55191";

        return new SmartCaCertificateDto(
            CertId: Guid.NewGuid().ToString(),
            SerialNumber: serial,
            ServiceType: "SMARTCA",
            ServiceName: "SMARTCA PERSONAL PRO",
            CertStatus: "Đang hoạt động",
            CertSubject: subject,
            ValidFrom: now.AddDays(-1).UtcDateTime,
            ValidTo: now.AddYears(2).UtcDateTime,
            CertDataBase64: certBase64,
            CaCertBase64: certBase64,
            RootCertBase64: certBase64,
            TransactionId: transactionId);
    }

    private static string GenerateSandboxSignatureBase64(string hashHex, string serialNumber)
    {
        var rawBytes = Encoding.UTF8.GetBytes($"VNPT_SMARTCA_PADES_SIG:{serialNumber}:{hashHex}:{DateTime.UtcNow:O}");
        var sigHash = SHA512.HashData(rawBytes);
        return Convert.ToBase64String(sigHash);
    }

    private sealed record SandboxSignSession(
        string TransactionId,
        string TranCode,
        string UserId,
        string SerialNumber,
        string DocId,
        string DataToBeSignedHex,
        string? Sad,
        DateTime CreatedAt);

    private sealed class SmartCaEnvelope<T>
    {
        [JsonPropertyName("status_code")]
        public int StatusCode { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    private sealed class SmartCaGetCertData
    {
        [JsonPropertyName("user_certificates")]
        public List<SmartCaRawCertificate>? UserCertificates { get; set; }
    }

    private sealed class SmartCaRawCertificate
    {
        [JsonPropertyName("service_type")]
        public string? ServiceType { get; set; }

        [JsonPropertyName("service_name")]
        public string? ServiceName { get; set; }

        [JsonPropertyName("cert_id")]
        public string? CertId { get; set; }

        [JsonPropertyName("cert_status")]
        public string? CertStatus { get; set; }

        [JsonPropertyName("serial_number")]
        public string? SerialNumber { get; set; }

        [JsonPropertyName("cert_subject")]
        public string? CertSubject { get; set; }

        [JsonPropertyName("cert_valid_from")]
        public string? CertValidFrom { get; set; }

        [JsonPropertyName("cert_valid_to")]
        public string? CertValidTo { get; set; }

        [JsonPropertyName("cert_data")]
        public string? CertData { get; set; }

        [JsonPropertyName("chain_data")]
        public SmartCaRawChainData? ChainData { get; set; }

        [JsonPropertyName("transaction_id")]
        public string? TransactionId { get; set; }
    }

    private sealed class SmartCaRawChainData
    {
        [JsonPropertyName("ca_cert")]
        public string? CaCert { get; set; }

        [JsonPropertyName("root_cert")]
        public string? RootCert { get; set; }
    }

    private sealed class SmartCaSignInitData
    {
        [JsonPropertyName("transaction_id")]
        public string? TransactionId { get; set; }

        [JsonPropertyName("tran_code")]
        public string? TranCode { get; set; }

        [JsonPropertyName("sad")]
        public string? Sad { get; set; }

        [JsonPropertyName("expired_in")]
        public int? ExpiredIn { get; set; }
    }

    private sealed class SmartCaSignStatusData
    {
        [JsonPropertyName("transaction_id")]
        public string? TransactionId { get; set; }

        [JsonPropertyName("expired_in")]
        public int? ExpiredIn { get; set; }

        [JsonPropertyName("signatures")]
        public List<SmartCaRawSignatureItem>? Signatures { get; set; }
    }

    private sealed class SmartCaRawSignatureItem
    {
        [JsonPropertyName("doc_id")]
        public string? DocId { get; set; }

        [JsonPropertyName("signature_value")]
        public string? SignatureValue { get; set; }

        [JsonPropertyName("timestamp_signature")]
        public string? TimestampSignature { get; set; }
    }
}
