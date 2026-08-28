namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ băm và xác thực mật khẩu người dùng (BCrypt/Argon2).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Băm mật khẩu thuần thành chuỗi hash an toàn.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Kiểm tra mật khẩu thuần có khớp với chuỗi hash trong DB hay không.
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);
}
