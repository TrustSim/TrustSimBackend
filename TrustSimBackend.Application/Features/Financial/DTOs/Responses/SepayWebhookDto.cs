using System.Text.Json.Serialization;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

public class SepayWebhookDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("gateway")]
    public string? Gateway { get; set; }

    [JsonPropertyName("transactionDate")]
    public string? TransactionDate { get; set; }

    [JsonPropertyName("transaction_date")]
    public string? SnakeTransactionDate { set => TransactionDate ??= value; }

    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }

    [JsonPropertyName("account_number")]
    public string? SnakeAccountNumber { set => AccountNumber ??= value; }

    [JsonPropertyName("subAccount")]
    public string? SubAccount { get; set; }

    [JsonPropertyName("sub_account")]
    public string? SnakeSubAccount { set => SubAccount ??= value; }

    [JsonPropertyName("transferAmount")]
    public decimal TransferAmount { get; set; }

    [JsonPropertyName("transfer_amount")]
    public decimal SnakeTransferAmount { set => TransferAmount = value != 0 ? value : TransferAmount; }

    [JsonPropertyName("transferType")]
    public string? TransferType { get; set; }

    [JsonPropertyName("transfer_type")]
    public string? SnakeTransferType { set => TransferType ??= value; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("accumulated")]
    public decimal Accumulated { get; set; }

    [JsonPropertyName("referenceCode")]
    public string? ReferenceCode { get; set; }

    [JsonPropertyName("reference_code")]
    public string? SnakeReferenceCode { set => ReferenceCode ??= value; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

