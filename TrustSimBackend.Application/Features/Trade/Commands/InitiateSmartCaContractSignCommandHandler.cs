using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

/// <summary>
/// Handler khởi tạo giao dịch ký số hợp đồng chuyển nhượng SIM thông qua VNPT SmartCA (v1 App Confirm hoặc v2 SmartOTP).
/// </summary>
public class InitiateSmartCaContractSignCommandHandler : IRequestHandler<InitiateSmartCaContractSignCommand, InitiateSmartCaContractSignResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISmartCaService _smartCaService;
    private readonly IPdfContractService? _pdfContractService;
    private readonly IMediator? _mediator;
    private readonly ILogger<InitiateSmartCaContractSignCommandHandler> _logger;

    public InitiateSmartCaContractSignCommandHandler(
        IUnitOfWork unitOfWork,
        ISmartCaService smartCaService,
        ILogger<InitiateSmartCaContractSignCommandHandler> logger,
        IPdfContractService? pdfContractService = null,
        IMediator? mediator = null)
    {
        _unitOfWork = unitOfWork;
        _smartCaService = smartCaService;
        _logger = logger;
        _pdfContractService = pdfContractService;
        _mediator = mediator;
    }

    public async Task<InitiateSmartCaContractSignResultDto> Handle(
        InitiateSmartCaContractSignCommand request,
        CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{request.TradeId}'.");

        if (trade.TradeTransactionStatus != TradeTransactionStatus.PendingSignature)
        {
            throw new BadRequestException("Giao dịch không ở trạng thái chờ ký hợp đồng.");
        }

        var isBuyer = trade.BuyerId == request.UserId;
        var isSeller = trade.SellerId == request.UserId;

        if (!isBuyer && !isSeller)
        {
            throw new ForbiddenAccessException("Bạn không phải là Bên Mua hoặc Bên Bán của giao dịch này.");
        }

        var role = isBuyer ? SignatureRole.BUYER : SignatureRole.SELLER;
        var user = isBuyer ? trade.Buyer : trade.Seller;

        if (trade.TransferContract == null)
        {
            var newContract = new TransferContract
            {
                Id = Guid.NewGuid(),
                TradeTransactionId = trade.Id,
                ContractStatus = ContractStatus.DRAFT
            };
            await _unitOfWork.TradeTransactions.AddTransferContractAsync(newContract, cancellationToken);
            trade.TransferContract = newContract;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (trade.TransferContract.DigitalSignatures.Any(s => s.SignatureRole == role))
        {
            throw new BadRequestException("Bạn đã ký xác nhận hợp đồng này rồi.");
        }

        var citizenId = !string.IsNullOrWhiteSpace(request.CitizenId)
            ? request.CitizenId.Trim()
            : user?.UserProfile?.IdentityNumber?.Trim();

        if (string.IsNullOrWhiteSpace(citizenId))
        {
            throw new BadRequestException("Tài khoản chưa có số CCCD xác thực eKYC để thực hiện ký số VNPT SmartCA.");
        }

        // 1. Lấy chứng thư số của người dùng từ VNPT SmartCA
        var certs = await _smartCaService.GetUserCertificatesAsync(citizenId, request.SerialNumber, cancellationToken);
        var selectedCert = certs?.FirstOrDefault(c =>
            string.IsNullOrWhiteSpace(request.SerialNumber) ||
            string.Equals(c.SerialNumber, request.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? certs?.FirstOrDefault();

        if (selectedCert == null || string.IsNullOrWhiteSpace(selectedCert.SerialNumber))
        {
            throw new ForbiddenAccessException($"Không tìm thấy chứng thư số VNPT SmartCA hợp lệ cho CCCD '{citizenId}'.");
        }

        // 2. Kiểm tra an toàn đối soát tên (Name Cross-Check) giữa thông tin eKYC tài khoản và chứng thư số SmartCA
        if (!string.IsNullOrWhiteSpace(user?.UserProfile?.FullName) && !string.IsNullOrWhiteSpace(selectedCert.CertSubject))
        {
            if (!IsNameSimilar(user.UserProfile.FullName, selectedCert.CertSubject))
            {
                _logger.LogWarning(
                    "[SmartCA Sign] Cảnh báo sai lệch chủ quyền: Họ tên tài khoản '{FullName}' không khớp với chứng thư số '{CertSubject}' (CCCD: {CitizenId}, Serial: {Serial})",
                    user.UserProfile.FullName, selectedCert.CertSubject, citizenId, selectedCert.SerialNumber);

                throw new BadRequestException(
                    $"Họ tên tài khoản ({user.UserProfile.FullName}) không trùng khớp với chủ sở hữu chứng thư số SmartCA ({selectedCert.CertSubject}). Vui lòng kiểm tra lại thông tin định danh.");
            }
        }

        // 3. Tạo mảng byte PDF hợp đồng và băm SHA-256 (Hex) để gửi sang VNPT SmartCA (sign_type = "hash")
        byte[] pdfBytesToHash;
        if (_pdfContractService != null)
        {
            var rawBytes = await _pdfContractService.GenerateRawPdfBytesAsync(trade.Id, isAutoEnforced: false, cancellationToken);
            pdfBytesToHash = rawBytes != null && rawBytes.Length > 0
                ? rawBytes
                : Encoding.UTF8.GetBytes($"CONTRACT_PDF:{trade.Id}:{trade.TradeTransactionCode}:{role}:{selectedCert.SerialNumber}");
        }
        else
        {
            pdfBytesToHash = Encoding.UTF8.GetBytes($"CONTRACT_PDF:{trade.Id}:{trade.TradeTransactionCode}:{role}:{selectedCert.SerialNumber}");
        }

        var dataToBeSignedHex = Convert.ToHexString(SHA256.HashData(pdfBytesToHash)).ToLowerInvariant();
        var docId = $"HD-{trade.TradeTransactionCode}-{role}";
        var transactionId = $"TS_SCA_{Guid.NewGuid():N}";

        var signHashRequest = new SmartCaSignHashRequestDto(
            UserIdCitizenId: citizenId,
            SerialNumber: selectedCert.SerialNumber,
            TransactionId: transactionId,
            TransactionDesc: $"Ky hop dong chuyen nhuong SIM {trade.TradeTransactionCode} ({role})",
            DocId: docId,
            DataToBeSignedHex: dataToBeSignedHex,
            FileType: "pdf",
            SignType: "hash",
            UserPassword: request.UserPassword,
            Otp: request.Otp);

        var initResult = await _smartCaService.InitiateSignHashAsync(signHashRequest, cancellationToken);
        var tranCode = !string.IsNullOrWhiteSpace(initResult.TranCode)
            ? initResult.TranCode
            : initResult.TransactionId;

        var pendingSession = new SmartCaPendingContractSession(
            TradeId: trade.Id,
            UserId: request.UserId,
            Role: role,
            CitizenId: citizenId,
            SerialNumber: selectedCert.SerialNumber,
            TransactionId: initResult.TransactionId,
            TranCode: tranCode,
            DocId: docId,
            DataToBeSignedHex: dataToBeSignedHex,
            IpAddress: request.IpAddress ?? "unknown",
            UserAgent: request.UserAgent ?? "unknown",
            CreatedAt: DateTime.UtcNow);

        SmartCaPendingSessionStore.Save(pendingSession);

        _logger.LogInformation(
            "[SmartCA Sign] Đã khởi tạo giao dịch ký số hợp đồng {TradeCode} cho {Role} (CCCD: {CitizenId}, Serial: {Serial}, TranCode: {TranCode})",
            trade.TradeTransactionCode, role, citizenId, selectedCert.SerialNumber, tranCode);

        // Nếu là luồng SmartCA Tích hợp (v2) có trả về SAD và có đầy đủ Password + OTP -> Tự động gọi v2/signatures/confirm
        if (!string.IsNullOrWhiteSpace(initResult.Sad)
            && !string.IsNullOrWhiteSpace(request.UserPassword))
        {
            var confirmStatus = await _smartCaService.ConfirmIntegratedSignAsync(
                citizenId,
                request.UserPassword,
                initResult.TransactionId,
                initResult.Sad,
                cancellationToken);

            var sigValue = confirmStatus.Signatures.FirstOrDefault()?.SignatureValueBase64;
            if (confirmStatus.IsCompleted && !string.IsNullOrWhiteSpace(sigValue) && _mediator != null)
            {
                var completedDto = await _mediator.Send(new CompleteSmartCaContractSignCommand(
                    TranCodeOrTransactionId: tranCode,
                    TradeId: trade.Id,
                    UserId: request.UserId,
                    SerialNumber: selectedCert.SerialNumber,
                    WebhookSignatureValueBase64: sigValue,
                    WebhookDocId: docId,
                    WebhookStatusCode: 200,
                    IpAddress: request.IpAddress,
                    UserAgent: request.UserAgent), cancellationToken);

                return new InitiateSmartCaContractSignResultDto(
                    TradeId: trade.Id,
                    TradeTransactionCode: trade.TradeTransactionCode,
                    SignatureRole: role.ToString(),
                    TransactionId: initResult.TransactionId,
                    TranCode: tranCode,
                    DocId: docId,
                    DataToBeSignedHex: dataToBeSignedHex,
                    SerialNumber: selectedCert.SerialNumber,
                    StatusMessage: completedDto.Message,
                    ExpiredInSeconds: initResult.ExpiredIn ?? 300,
                    IsCompletedImmediately: true,
                    CompletedSignResult: completedDto);
            }
        }

        return new InitiateSmartCaContractSignResultDto(
            TradeId: trade.Id,
            TradeTransactionCode: trade.TradeTransactionCode,
            SignatureRole: role.ToString(),
            TransactionId: initResult.TransactionId,
            TranCode: tranCode,
            DocId: docId,
            DataToBeSignedHex: dataToBeSignedHex,
            SerialNumber: selectedCert.SerialNumber,
            StatusMessage: initResult.Message,
            ExpiredInSeconds: initResult.ExpiredIn ?? 300,
            IsCompletedImmediately: false,
            CompletedSignResult: null);
    }

    private static bool IsNameSimilar(string fullName, string certSubject)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(certSubject))
        {
            return true;
        }

        var normSubject = NormalizeVietnamese(certSubject);
        var normName = NormalizeVietnamese(fullName);

        // Chấp nhận nếu là chứng thư sandbox giả lập
        if (normSubject.Contains("thue bao smartca"))
        {
            return true;
        }

        var cnPart = ExtractCommonName(certSubject);
        var normCn = !string.IsNullOrWhiteSpace(cnPart) ? NormalizeVietnamese(cnPart) : normSubject;

        // Trùng khớp hoàn toàn chuỗi họ tên
        if (normCn.Contains(normName) || normName.Contains(normCn))
        {
            return true;
        }

        var nameTokens = normName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cnTokens = normCn.Split(new[] { ' ', ',', '.', '/', '-', '_', '=', ':' }, StringSplitOptions.RemoveEmptyEntries);

        if (nameTokens.Length == 0 || cnTokens.Length == 0)
        {
            return true;
        }

        var significantTokens = nameTokens.Where(t => t.Length >= 2).ToList();
        if (significantTokens.Count == 0)
        {
            return nameTokens.Any(t => cnTokens.Contains(t, StringComparer.OrdinalIgnoreCase));
        }

        return significantTokens.Any(t => cnTokens.Contains(t, StringComparer.OrdinalIgnoreCase));
    }

    private static string ExtractCommonName(string certSubject)
    {
        if (string.IsNullOrWhiteSpace(certSubject)) return string.Empty;

        var match = Regex.Match(certSubject, @"(?i)(?:^|,\s*)CN\s*=\s*([^,]+)");
        return match.Success && match.Groups.Count > 1
            ? match.Groups[1].Value.Trim()
            : certSubject.Trim();
    }

    private static string NormalizeVietnamese(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizedString.Length);
        foreach (var c in normalizedString)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .ToLowerInvariant()
            .Trim();
    }
}
