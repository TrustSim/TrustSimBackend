using AutoMapper;
using TrustSimBackend.Application.Features.Financial.DTOs;
using TrustSimBackend.Domain.Entities.Financial;

namespace TrustSimBackend.Application.Mappings;

/// <summary>
/// Cấu hình AutoMapper cho phân hệ Tài chính / Ví tiền (Financial & Wallet).
/// </summary>
public class FinancialMappingProfile : Profile
{
    public FinancialMappingProfile()
    {
        CreateMap<Wallet, WalletDto>()
            .ForMember(d => d.WalletId, opt => opt.MapFrom(s => s.Id));

        CreateMap<WalletTransaction, WalletTransactionDto>()
            .ForMember(d => d.WalletTransactionId, opt => opt.MapFrom(s => s.Id));
    }
}
