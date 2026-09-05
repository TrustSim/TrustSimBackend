namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

public class SepayWebhookDto
{
    public long Id { get; set; }
    public string Gateway { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? SubAccount { get; set; }
    public decimal TransferAmount { get; set; }
    public string TransferType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Code { get; set; }
    public decimal Accumulated { get; set; }
    public string? ReferenceCode { get; set; }
    public string? Description { get; set; }
}
