using MediatR;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

/// <summary>
/// Query tính toán tiền cọc Người Bán dựa trên Giá khởi điểm và cấu hình SellerDepositSettings trong appsettings.json
/// </summary>
public record CalculateSellerDepositQuery(decimal StartingPrice) : IRequest<CalculateSellerDepositResultDto>;
