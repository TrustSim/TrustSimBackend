using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ băm mật khẩu sử dụng thuật toán BCrypt.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }
}
