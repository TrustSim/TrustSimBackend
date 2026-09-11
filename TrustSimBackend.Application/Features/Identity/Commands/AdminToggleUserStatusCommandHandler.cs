using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

public class AdminToggleUserStatusCommandHandler : IRequestHandler<AdminToggleUserStatusCommand, bool>
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AdminToggleUserStatusCommandHandler(IUserAccountRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(AdminToggleUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user == null) throw new NotFoundException($"Không tìm thấy tài khoản người dùng với ID '{request.Id}'.");

        user.IsDeleted = !request.IsActive;
        user.UpdatedDate = DateTime.UtcNow;

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
