namespace TrustSimBackend.Application.Common.Exceptions;

public class SimCarrierNotFoundException : NotFoundException
{
    public SimCarrierNotFoundException(string identifier)
        : base($"Không tìm thấy nhà mạng viễn thông với định danh '{identifier}'.") { }
}
