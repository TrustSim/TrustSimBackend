using AutoMapper;
using TrustSimBackend.Domain.Entities.Trade;

namespace TrustSimBackend.Application.Mappings;

/// <summary>
/// Cấu hình AutoMapper cho phân hệ Mua bán & Sang tên chính chủ SIM (Trade).
/// </summary>
public class TradeMappingProfile : Profile
{
    public TradeMappingProfile()
    {
        CreateMap<TradeTransaction, object>();
        CreateMap<TransferContract, object>();
        CreateMap<SimTransfer, object>();
    }
}
