using Microsoft.AspNetCore.Identity;
using TrustSimBackend.Domain.Entities.Identity;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai PasswordHasher tùy biến cho ASP.NET Core Identity hỗ trợ:
/// 1. Chuẩn băm mật khẩu Identity PBKDF2 HMAC-SHA256 (Base64).
/// 2. Tương thích ngược liền mạch (Backward Compatibility) với các mật khẩu cũ được băm bằng BCrypt ($2a$, $2b$, $2y$).
/// 3. Tự động băm lại mật khẩu cũ sang chuẩn Identity mới khi người dùng đăng nhập thành công (SuccessRehashNeeded).
/// </summary>
public class CustomIdentityPasswordHasher : PasswordHasher<UserAccount>
{
    public override PasswordVerificationResult VerifyHashedPassword(UserAccount user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        // Kiểm tra nếu là mật khẩu định dạng BCrypt cũ ($2a$, $2b$, $2y$, $2x$)
        if (hashedPassword.StartsWith("$2", StringComparison.Ordinal))
        {
            try
            {
                var isBcryptValid = BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
                if (isBcryptValid)
                {
                    // Trả về SuccessRehashNeeded để ASP.NET Core Identity tự động băm lại mật khẩu sang chuẩn mới
                    return PasswordVerificationResult.SuccessRehashNeeded;
                }

                return PasswordVerificationResult.Failed;
            }
            catch
            {
                return PasswordVerificationResult.Failed;
            }
        }

        // Mật khẩu chuẩn ASP.NET Core Identity (PBKDF2 Base64)
        try
        {
            return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }
    }
}
