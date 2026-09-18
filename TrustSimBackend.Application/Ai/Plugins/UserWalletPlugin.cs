using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Ai.Plugins;

/// <summary>
/// Plugin tra cứu số dư tài khoản cá nhân và hướng dẫn nạp tiền tự động qua SePay VietQR.
/// </summary>
public class UserWalletPlugin
{
    private readonly IWalletRepository _walletRepo;
    private readonly ICurrentUserService _currentUser;

    public UserWalletPlugin(IWalletRepository walletRepo, ICurrentUserService currentUser)
    {
        _walletRepo = walletRepo;
        _currentUser = currentUser;
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

        return JsonSerializer.Serialize(new
        {
            method = "Chuyển khoản SePay VietQR Tự Động",
            bankName = "Sacombank",
            accountNumber = "070131881841",
            accountHolder = "CONG TY TNHH TRUSTSIM",
            suggestedAmount = amount,
            transferSyntax = $"TS NAPTIEN {userCode}",
            processingTime = "Cộng tiền tự động 24/7 tức thì trong 5 - 15 giây sau khi hoàn tất chuyển khoản."
        });
    }
}
