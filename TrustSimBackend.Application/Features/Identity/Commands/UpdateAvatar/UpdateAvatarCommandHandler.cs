using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands.UpdateAvatar;

/// <summary>
/// Xử lý cập nhật ảnh đại diện của người dùng.
/// </summary>
public class UpdateAvatarCommandHandler : IRequestHandler<UpdateAvatarCommand, ApiResponse<string>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAvatarCommandHandler(
        IUserAccountRepository userAccountRepository,
        IUnitOfWork unitOfWork)
    {
        _userAccountRepository = userAccountRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<string>> Handle(UpdateAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await _userAccountRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException(nameof(UserAccount), request.UserId);
        }

        if (user.UserProfile == null)
        {
            user.UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = user.Id,
                Email = user.UserEmail
            };
        }

        user.UserProfile.AvatarImg = request.AvatarUrl.Trim();
        _userAccountRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<string>.SuccessResult(user.UserProfile.AvatarImg, "Cập nhật ảnh đại diện thành công.");
    }
}
