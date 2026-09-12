namespace TrustSimBackend.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ bắn ra khi Họ và tên trích xuất từ thẻ CCCD không trùng khớp với Họ và tên trong hồ sơ người dùng.
/// </summary>
public class IdentityNameMismatchException : Exception
{
    public string ExtractedName { get; }
    public string ProfileName { get; }

    public IdentityNameMismatchException(string extractedName, string profileName)
        : base($"Họ và tên trên thẻ CCCD ({extractedName}) không trùng khớp với Họ và tên trong hồ sơ cá nhân ({profileName}).")
    {
        ExtractedName = extractedName;
        ProfileName = profileName;
    }
}
