namespace TrustSimBackend.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ bắn ra khi tài khoản đã hoàn tất xác minh KYC trước đó.
/// </summary>
public class IdentityAlreadyVerifiedException : Exception
{
    public IdentityAlreadyVerifiedException()
        : base("Hồ sơ cá nhân của tài khoản này đã được xác thực danh tính (KYC) thành công trước đó.") { }

    public IdentityAlreadyVerifiedException(string message) : base(message) { }
}
