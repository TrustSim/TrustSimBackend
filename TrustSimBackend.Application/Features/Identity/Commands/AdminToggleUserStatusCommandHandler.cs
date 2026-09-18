using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

public class AdminToggleUserStatusCommandHandler : IRequestHandler<AdminToggleUserStatusCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminToggleUserStatusCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(AdminToggleUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.Id, cancellationToken);
        if (user == null) throw new NotFoundException($"Không tìm thấy tài khoản người dùng với ID '{request.Id}'.");

        user.IsDeleted = !request.IsActive;
        user.UpdatedDate = DateTime.UtcNow;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
