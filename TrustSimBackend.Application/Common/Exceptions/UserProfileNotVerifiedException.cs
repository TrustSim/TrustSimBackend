namespace TrustSimBackend.Application.Common.Exceptions;

public class UserProfileNotVerifiedException : Exception
{
    public UserProfileNotVerifiedException()
        : base("Hồ sơ cá nhân chưa được xác thực danh tính (KYC). Vui lòng hoàn tất xác thực KYC trước khi thực hiện xác thực quyền sở hữu SIM.") { }

    public UserProfileNotVerifiedException(string message) : base(message) { }
}
