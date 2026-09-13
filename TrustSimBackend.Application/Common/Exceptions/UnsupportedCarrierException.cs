namespace TrustSimBackend.Application.Common.Exceptions;

public class UnsupportedCarrierException : Exception
{
    public UnsupportedCarrierException()
        : base("Đầu số của số SIM này chưa được hệ thống hỗ trợ hoặc không tìm thấy nhà mạng tương ứng trong cơ sở dữ liệu.") { }

    public UnsupportedCarrierException(string prefixValue)
        : base($"Đầu số '{prefixValue}' chưa được hệ thống hỗ trợ hoặc không thuộc nhà mạng nào đã kích hoạt.") { }
}
