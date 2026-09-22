using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Ai.Plugins;

/// <summary>
/// Plugin tra cứu số dư tài khoản cá nhân và hướng dẫn nạp tiền tự động qua SePay VietQR.
/// </summary>
public class UserWalletPlugin
{
    private readonly IWalletRepository _walletRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly SePaySettings _sePaySettings;

    public UserWalletPlugin(
        IWalletRepository walletRepo,
        ICurrentUserService currentUser,
        IOptions<SePaySettings> sePayOptions)
    {
        _walletRepo = walletRepo;
        _currentUser = currentUser;
        _sePaySettings = sePayOptions.Value;
    }

    [KernelFunction, Description("Tra cứu số dư ví khả dụng hiện tại của người dùng đã đăng nhập. Gọi khi người dùng hỏi 'tôi còn bao nhiêu tiền' hoặc 'số dư của tôi có đủ mua sim này không'.")]
    public async Task<string> GetMyWalletBalanceAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return "{\"isLoggedIn\":false,\"message\":\"Người dùng chưa đăng nhập. Vui lòng đăng nhập tài khoản TrustSim để tra cứu số dư ví.\"}";
        }

        var wallet = await _walletRepo.GetByUserIdAsync(userId.Value, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            isLoggedIn = true,
            balance = wallet?.AvailableBalance ?? 0,
            lockedBalance = wallet?.LockedBalance ?? 0,
            currency = "VND"
        });
    }

    [KernelFunction, Description("Cung cấp thông tin và hướng dẫn nạp tiền vào ví sàn TrustSim qua cổng thanh toán tự động SePay VietQR.")]
    public string GetSePayDepositGuideline(
        [Description("Số tiền muốn nạp vào ví (VND)")] decimal amount)
    {
        var userCode = _currentUser.UserId.HasValue
            ? _currentUser.UserId.Value.ToString("N").Substring(0, 8).ToUpper()
            : "GUEST";

        var prefix = !string.IsNullOrWhiteSpace(_sePaySettings.PaymentPrefix) ? _sePaySettings.PaymentPrefix : "TS";
        var bankName = !string.IsNullOrWhiteSpace(_sePaySettings.BankName) ? _sePaySettings.BankName : "MBBank";
        var accountNumber = !string.IsNullOrWhiteSpace(_sePaySettings.AccountNumber) ? _sePaySettings.AccountNumber : "070131881841";
        var accountHolder = !string.IsNullOrWhiteSpace(_sePaySettings.AccountHolderName) ? _sePaySettings.AccountHolderName : "CONG TY TNHH TRUSTSIM";

        return JsonSerializer.Serialize(new
        {
            method = "Chuyển khoản SePay VietQR Tự Động",
            bankName = bankName,
            accountNumber = accountNumber,
            accountHolder = accountHolder,
            suggestedAmount = amount,
            transferSyntax = $"{prefix} NAPTIEN {userCode}",
            processingTime = "Cộng tiền tự động 24/7 tức thì trong 5 - 15 giây sau khi hoàn tất chuyển khoản."
        });
    }
}

