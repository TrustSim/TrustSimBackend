namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

public class DepositResponseDto
{
    public Guid TransactionId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransferContent { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public DateTime ExpiredAt { get; set; }
}
