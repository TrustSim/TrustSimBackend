namespace TrustSimBackend.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ bắn ra khi số CMND/CCCD/Hộ chiếu đã được xác thực ở một tài khoản khác.
/// </summary>
public class DuplicateIdentityNumberException : Exception
{
    public DuplicateIdentityNumberException(string identityNumber)
        : base($"Số CMND/CCCD/Hộ chiếu '{identityNumber}' đã được xác thực và liên kết với một tài khoản khác trên hệ thống.") { }
}
