using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý cập nhật ảnh đại diện của người dùng.
/// </summary>
public class UpdateAvatarCommandHandler : IRequestHandler<UpdateAvatarCommand, string>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAvatarCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<string> Handle(UpdateAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
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
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user.UserProfile.AvatarImg;
    }
}
