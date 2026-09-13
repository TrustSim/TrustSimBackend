namespace TrustSimBackend.Application.Common.Exceptions;

public class OtpVerificationFailedException : Exception
{
    public OtpVerificationFailedException()
        : base("Mã xác thực OTP không chính xác hoặc đã hết hạn.") { }

    public OtpVerificationFailedException(string message) : base(message) { }
}
