namespace TrustSimBackend.Application.Features.Auction.DTOs.Responses;

/// <summary>
/// DTO kết quả tính toán tiền cọc Người Bán (Seller Deposit) trả về từ Server
/// </summary>
public record CalculateSellerDepositResultDto(
    decimal StartingPrice,
    decimal DepositRate,
    decimal RawDeposit,
    decimal SellerDepositAmount,
    decimal MinDepositAmount,
    decimal MaxDepositAmount,
    bool IsMinClamped,
    bool IsMaxClamped,
    string FormulaText,
    string BreakdownText
);
