using AutoMapper;
using TrustSimBackend.Application.Features.Identity.DTOs;
using TrustSimBackend.Domain.Entities.Identity;

namespace TrustSimBackend.Application.Mappings;

/// <summary>
/// Cấu hình AutoMapper cho phân hệ Identity (Người dùng & Xác thực).
/// </summary>
public class IdentityMappingProfile : Profile
{
    public IdentityMappingProfile()
    {
        CreateMap<UserAccount, AuthResponseDto>()
            .ForMember(d => d.UserId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.Email, opt => opt.MapFrom(s => s.UserEmail))
            .ForMember(d => d.FullName, opt => opt.MapFrom(s => s.UserProfile != null ? s.UserProfile.FullName : string.Empty))
            .ForMember(d => d.PhoneNumber, opt => opt.MapFrom(s => s.UserProfile != null ? s.UserProfile.PhoneNumber : string.Empty))
            .ForMember(d => d.Role, opt => opt.MapFrom(s => s.Role.ToString()));

        CreateMap<UserAccount, UserProfileDto>()
            .ForMember(d => d.UserId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.Email, opt => opt.MapFrom(s => s.UserEmail))
            .ForMember(d => d.FullName, opt => opt.MapFrom(s => s.UserProfile != null ? s.UserProfile.FullName : string.Empty))
            .ForMember(d => d.PhoneNumber, opt => opt.MapFrom(s => s.UserProfile != null ? s.UserProfile.PhoneNumber : string.Empty))
            .ForMember(d => d.Address, opt => opt.MapFrom(s => s.UserProfile != null ? s.UserProfile.Address : string.Empty))
            .ForMember(d => d.AvatarImg, opt => opt.MapFrom(s => s.UserProfile != null ? s.UserProfile.AvatarImg : string.Empty))
            .ForMember(d => d.IdentityNumber, opt => opt.MapFrom(s => s.UserProfile != null ? s.UserProfile.IdentityNumber : string.Empty))
            .ForMember(d => d.DateOfBirth, opt => opt.MapFrom(s => s.UserProfile != null ? s.UserProfile.DateOfBirth : default(DateTime?)))
            .ForMember(d => d.VerificationStatus, opt => opt.MapFrom(s => s.UserProfile != null ? s.UserProfile.VerificationStatus : Domain.Enums.VerificationStatus.PENDING_VERIFICATION))
            .ForMember(d => d.Role, opt => opt.MapFrom(s => s.Role.ToString()))
            .ForMember(d => d.WalletBalance, opt => opt.MapFrom(s => s.Wallet != null ? s.Wallet.AvailableBalance : 0));
    }
}
