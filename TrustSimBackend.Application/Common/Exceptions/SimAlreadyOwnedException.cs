namespace TrustSimBackend.Application.Common.Exceptions;

public class SimAlreadyOwnedException : Exception
{
    public SimAlreadyOwnedException()
        : base("Số SIM này hiện đang thuộc quyền sở hữu của một tài khoản khác trên hệ thống.") { }

    public SimAlreadyOwnedException(string phoneNumber)
        : base($"Số SIM '{phoneNumber}' đã được xác thực sở hữu bởi một tài khoản khác trên hệ thống.") { }
}
