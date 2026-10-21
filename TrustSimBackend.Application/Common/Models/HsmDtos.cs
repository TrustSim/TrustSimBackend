using System.Text.Json.Serialization;

namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Kết quả phản hồi trạng thái sức khỏe của SoftHSMv2 microservice.
/// </summary>
public record HsmHealthResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("hsm_library")] string? HsmLibrary,
    [property: JsonPropertyName("token_label")] string? TokenLabel,
    [property: JsonPropertyName("slot_id")] long? SlotId,
    [property: JsonPropertyName("token_initialized")] bool? TokenInitialized,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("version")] string? Version);

/// <summary>
/// Yêu cầu sinh cặp khóa mới trên SoftHSM.
/// </summary>
public record HsmGenerateKeyRequest(
    [property: JsonPropertyName("key_label")] string KeyLabel,
    [property: JsonPropertyName("key_type")] string KeyType = "ECDSA",
    [property: JsonPropertyName("key_size")] int KeySize = 2048,
    [property: JsonPropertyName("curve")] string Curve = "secp256r1");

/// <summary>
/// Kết quả sinh cặp khóa mới.
/// </summary>
public record HsmGenerateKeyResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("key_label")] string KeyLabel,
    [property: JsonPropertyName("key_type")] string KeyType,
    [property: JsonPropertyName("public_key_pem")] string PublicKeyPem);

/// <summary>
/// Yêu cầu ký số văn bản hoặc mã băm.
/// </summary>
public record HsmSignRequest(
    [property: JsonPropertyName("key_label")] string KeyLabel,
    [property: JsonPropertyName("data")] string? Data = null,
    [property: JsonPropertyName("data_hex")] string? DataHex = null,
    [property: JsonPropertyName("is_prehashed")] bool IsPrehashed = false,
    [property: JsonPropertyName("mechanism")] string Mechanism = "AUTO");

/// <summary>
/// Kết quả ký số.
/// </summary>
public record HsmSignResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("key_label")] string KeyLabel,
    [property: JsonPropertyName("signature_base64")] string SignatureBase64,
    [property: JsonPropertyName("mechanism")] string? Mechanism = null);

/// <summary>
/// Yêu cầu xác thực chữ ký số.
/// </summary>
public record HsmVerifyRequest(
    [property: JsonPropertyName("key_label")] string KeyLabel,
    [property: JsonPropertyName("signature_base64")] string SignatureBase64,
    [property: JsonPropertyName("data")] string? Data = null,
    [property: JsonPropertyName("data_hex")] string? DataHex = null,
    [property: JsonPropertyName("is_prehashed")] bool IsPrehashed = false,
    [property: JsonPropertyName("mechanism")] string Mechanism = "AUTO");

/// <summary>
/// Kết quả xác thực chữ ký số.
/// </summary>
public record HsmVerifyResponse(
    [property: JsonPropertyName("valid")] bool Valid,
    [property: JsonPropertyName("key_label")] string? KeyLabel = null,
    [property: JsonPropertyName("mechanism")] string? Mechanism = null);

/// <summary>
/// Yêu cầu mã hóa dữ liệu bảo vệ (Envelope Encryption / Sealed-Bid).
/// </summary>
public record HsmEncryptRequest(
    [property: JsonPropertyName("key_label")] string KeyLabel,
    [property: JsonPropertyName("plaintext")] string Plaintext,
    [property: JsonPropertyName("mechanism")] string Mechanism = "RSA_PKCS1");

/// <summary>
/// Kết quả mã hóa dữ liệu.
/// </summary>
public record HsmEncryptResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("key_label")] string KeyLabel,
    [property: JsonPropertyName("ciphertext_base64")] string CiphertextBase64,
    [property: JsonPropertyName("mechanism")] string? Mechanism = null);

/// <summary>
/// Yêu cầu giải mã dữ liệu bằng Private Key bảo vệ trong HSM.
/// </summary>
public record HsmDecryptRequest(
    [property: JsonPropertyName("key_label")] string KeyLabel,
    [property: JsonPropertyName("ciphertext_base64")] string CiphertextBase64,
    [property: JsonPropertyName("mechanism")] string Mechanism = "RSA_PKCS1",
    [property: JsonPropertyName("return_as_string")] bool ReturnAsString = true);

/// <summary>
/// Kết quả giải mã dữ liệu.
/// </summary>
public record HsmDecryptResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("key_label")] string KeyLabel,
    [property: JsonPropertyName("plaintext")] string Plaintext,
    [property: JsonPropertyName("mechanism")] string? Mechanism = null);

/// <summary>
/// Kết quả lấy Public Key chuẩn PEM.
/// </summary>
public record HsmPublicKeyResponse(
    [property: JsonPropertyName("key_label")] string KeyLabel,
    [property: JsonPropertyName("key_type")] string KeyType,
    [property: JsonPropertyName("public_key_pem")] string PublicKeyPem);

/// <summary>
/// Kết quả sao lưu hoặc phục hồi Token Cloud Storage.
/// </summary>
public record HsmBackupResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("timestamp")] string Timestamp);
