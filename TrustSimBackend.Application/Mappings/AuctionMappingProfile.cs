using AutoMapper;
using TrustSimBackend.Domain.Entities.Auctions;

namespace TrustSimBackend.Application.Mappings;

/// <summary>
/// Cấu hình AutoMapper cho phân hệ Đấu giá SIM (Auction).
/// </summary>
public class AuctionMappingProfile : Profile
{
    public AuctionMappingProfile()
    {
        CreateMap<Auction, object>();
        CreateMap<Bid, object>();
        CreateMap<AuctionParticipant, object>();
        CreateMap<AuctionResult, object>();
    }
}
