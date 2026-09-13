namespace TrustSimBackend.Application.Common.Exceptions;

public class SimOwnershipVerificationFailedException : Exception
{
    public SimOwnershipVerificationFailedException()
        : base("Xác thực quyền sở hữu SIM với nhà mạng thất bại do thông tin không trùng khớp.") { }

    public SimOwnershipVerificationFailedException(string reason)
        : base($"Xác thực nhà mạng thất bại: {reason}") { }
}
