using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Chat.DTOs.Requests;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Chat.Queries;

/// <summary>
/// Xử lý tìm kiếm người dùng khác thông qua Email hoặc Số điện thoại liên lạc trong UserProfile.
/// Yêu cầu bắt buộc: Cả người tìm kiếm và người được tìm thấy đều phải đã tạo tài khoản và xác thực danh tính (eKYC VERIFIED).
/// </summary>
public class SearchVerifiedUsersForChatQueryHandler : IRequestHandler<SearchVerifiedUsersForChatQuery, IReadOnlyList<ChatUserSearchDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public SearchVerifiedUsersForChatQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ChatUserSearchDto>> Handle(SearchVerifiedUsersForChatQuery request, CancellationToken cancellationToken)
    {
        var currentUser = await _unitOfWork.Users.GetByIdAsync(request.CurrentUserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy tài khoản người dùng với ID '{request.CurrentUserId}'.");

        if (!currentUser.IsActive || currentUser.IsDeleted)
        {
            throw new ForbiddenAccessException("Tài khoản của bạn đã bị khóa hoặc ngừng hoạt động.");
        }

        // Bắt buộc người dùng phải đã xác thực danh tính (eKYC) mới được tìm kiếm và nhắn tin với người dùng khác
        if (request.CurrentUserRole == UserRole.USER &&
            (currentUser.UserProfile == null || currentUser.UserProfile.VerificationStatus != VerificationStatus.VERIFIED))
        {
            throw new UserProfileNotVerifiedException(
                "Chỉ những người dùng đã tạo tài khoản và hoàn tất xác thực danh tính (eKYC) mới được phép tìm kiếm và nhắn tin với người dùng khác.");
        }

        if (string.IsNullOrWhiteSpace(request.Keyword))
        {
            return Array.Empty<ChatUserSearchDto>();
        }

        var safeLimit = request.Limit <= 0 ? 10 : Math.Min(request.Limit, 50);
        var matchedUsers = await _unitOfWork.ChatConversations.SearchVerifiedUsersByEmailOrPhoneAsync(
            request.Keyword,
            request.CurrentUserId,
            safeLimit,
            cancellationToken);

        return matchedUsers
            .Select(u => new ChatUserSearchDto(
                UserId: u.Id,
                UserName: u.UserName ?? string.Empty,
                FullName: !string.IsNullOrWhiteSpace(u.UserProfile?.FullName) ? u.UserProfile.FullName : (u.UserName ?? string.Empty),
                Email: !string.IsNullOrWhiteSpace(u.UserProfile?.Email) ? u.UserProfile.Email : (u.Email ?? string.Empty),
                PhoneNumber: u.UserProfile?.PhoneNumber ?? string.Empty,
                AvatarUrl: u.UserProfile?.AvatarImg ?? string.Empty,
                VerificationStatus: u.UserProfile?.VerificationStatus ?? VerificationStatus.PENDING_VERIFICATION))
            .ToList();
    }
}
