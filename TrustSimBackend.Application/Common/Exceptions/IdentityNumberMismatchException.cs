namespace TrustSimBackend.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ bắn ra khi số CMND/CCCD trích xuất từ hình ảnh không trùng khớp với số CMND/CCCD đã đăng ký trong hồ sơ người dùng.
/// </summary>
public class IdentityNumberMismatchException : Exception
{
    public string ExtractedNumber { get; }
    public string ProfileNumber { get; }

    public IdentityNumberMismatchException(string extractedNumber, string profileNumber)
        : base($"Số CMND/CCCD trên hình ảnh ({extractedNumber}) không trùng khớp với số CMND/CCCD trong hồ sơ cá nhân ({profileNumber}).")
    {
        ExtractedNumber = extractedNumber;
        ProfileNumber = profileNumber;
    }
}
