using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iText.Bouncycastle.Cert;
using iText.Bouncycastle.Crypto;
using iText.Bouncycastle.X509;
using iText.Commons.Bouncycastle.Cert;
using iText.Kernel.Pdf;
using iText.Signatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Pkcs;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Dịch vụ ký số PAdES niêm phong hợp đồng số sử dụng iText7, nạp chứng thư X.509
/// và tích hợp dấu thời gian RFC 3161 TSA (với cơ chế Circuit Breaker 3s & Fallback).
/// </summary>
public class PdfSigningService : IPdfSigningService
{
    private readonly ILogger<PdfSigningService> _logger;
    private readonly IConfiguration _configuration;

    public PdfSigningService(ILogger<PdfSigningService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<PadesSignResult> SignPdfPadesAsync(
        byte[] sourcePdfBytes,
        string signatureReason,
        string signatureLocation,
        IReadOnlyList<SignatureStampBoxDto> stampBoxes,
        CancellationToken cancellationToken = default)
    {
        // 1. Lấy đường dẫn file chứng thư .pfx
        var pfxPath = _configuration["DigitalSignature:CertPath"] ?? "keys/trustsim_platform.pfx";
        var pfxPassword = _configuration["DigitalSignature:CertPassword"] ?? "TrustSim2026@Pass";

        // Tự động sinh chứng thư nếu chưa tồn tại (Hỗ trợ chạy ngay trong môi trường Dev/Test)
        EnsureCertificateExists(pfxPath, pfxPassword);

        // 2. Nạp chứng thư và Private Key qua BouncyCastle PKCS12 Store
        var pkcs12Store = new Pkcs12StoreBuilder().Build();
        await using (var certStream = File.OpenRead(pfxPath))
        {
            pkcs12Store.Load(certStream, pfxPassword.ToCharArray());
        }

        string alias = null!;
        foreach (string al in pkcs12Store.Aliases)
        {
            if (pkcs12Store.IsKeyEntry(al))
            {
                alias = al;
                break;
            }
        }

        var privateKey = pkcs12Store.GetKey(alias).Key;
        var certChain = pkcs12Store.GetCertificateChain(alias);
        var bcChain = new IX509Certificate[certChain.Length];
        for (int i = 0; i < certChain.Length; i++)
        {
            bcChain[i] = new X509CertificateBC(certChain[i].Certificate);
        }
        var ipk = new PrivateKeyBC(privateKey);

        // 3. Gọi dịch vụ Timestamp Authority (TSA) với Circuit Breaker 3 giây
        ITSAClient? tsaClient = null;
        var tsaUrl = _configuration["DigitalSignature:TsaUrl"] ?? "https://freetsa.org/tsr";
        string? usedTsaAuthority = null;
        DateTime? tsaTime = null;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var pingUri = new Uri(tsaUrl);
            var response = await httpClient.GetAsync(pingUri.GetLeftPart(UriPartial.Authority), cts.Token);

            tsaClient = new TSAClientBouncyCastle(tsaUrl);
            usedTsaAuthority = pingUri.Host;
            tsaTime = DateTime.UtcNow;
            _logger.LogInformation("[PdfSigner] ✅ Kết nối máy chủ TSA '{Host}' thành công.", usedTsaAuthority);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[PdfSigner] ⚠️ Không thể kết nối máy chủ TSA ({Msg}). Fallback sang Local Server Time.", ex.Message);
            tsaClient = null;
            usedTsaAuthority = "LOCAL_SERVER_TIME_FALLBACK";
            tsaTime = DateTime.UtcNow;
        }

        // 4. Ký số PAdES Detached Signature (CADES)
        using var readerStream = new MemoryStream(sourcePdfBytes);
        using var outputStream = new MemoryStream();
        var reader = new PdfReader(readerStream);
        var signer = new PdfSigner(reader, outputStream, new StampingProperties().UseAppendMode());

        signer.SetReason(signatureReason);
        signer.SetLocation(signatureLocation);

        IExternalSignature pks = new PrivateKeySignature(ipk, DigestAlgorithms.SHA256);
        signer.SignDetached(pks, bcChain, null, null, tsaClient, 0, PdfSigner.CryptoStandard.CADES);

        var signedBytes = outputStream.ToArray();
        var sha256Hex = Convert.ToHexString(SHA256.HashData(signedBytes)).ToLower();
        var certSerial = certChain[0].Certificate.SerialNumber.ToString(16);

        return new PadesSignResult(
            signedBytes,
            sha256Hex,
            certSerial,
            DateTime.UtcNow,
            tsaTime,
            usedTsaAuthority);
    }

    /// <summary>
    /// Kiểm tra và tự động sinh chứng thư X.509 RSA 2048-bit nếu file .pfx chưa tồn tại trên ổ đĩa.
    /// </summary>
    private void EnsureCertificateExists(string pfxPath, string pfxPassword)
    {
        if (File.Exists(pfxPath)) return;

        try
        {
            var dir = Path.GetDirectoryName(pfxPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _logger.LogInformation("[PdfSigner] 🔨 Chưa tìm thấy chứng thư tại '{Path}'. Đang tự động sinh chứng thư X.509 mới...", pfxPath);

            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest(
                "CN=TrustSim Platform Authority, O=TrustSim Inc, C=VN",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            req.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, 
                critical: true));

            var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
            var certWithKey = cert.CopyWithPrivateKey(rsa);
            var pfxBytes = certWithKey.Export(X509ContentType.Pfx, pfxPassword);

            File.WriteAllBytes(pfxPath, pfxBytes);
            _logger.LogInformation("[PdfSigner] ✅ Đã tự động tạo và lưu trữ chứng thư số tại '{Path}'.", pfxPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PdfSigner] ❌ Lỗi khi tự động sinh chứng thư số: {Msg}", ex.Message);
        }
    }
}
