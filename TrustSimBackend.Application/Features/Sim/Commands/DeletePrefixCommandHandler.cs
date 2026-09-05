using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class DeletePrefixCommandHandler : IRequestHandler<DeletePrefixCommand, bool>
{
    private readonly IPhonePrefixRepository _prefixRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePrefixCommandHandler(IPhonePrefixRepository prefixRepository, IUnitOfWork unitOfWork)
    {
        _prefixRepository = prefixRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeletePrefixCommand request, CancellationToken cancellationToken)
    {
        var prefix = await _prefixRepository.GetByIdAsync(request.Id, cancellationToken);
        if (prefix == null)
        {
            throw new NotFoundException($"Không tìm thấy đầu số với ID '{request.Id}'.");
        }

        prefix.IsDeleted = true;
        prefix.DeletedDate = DateTime.UtcNow;

        _prefixRepository.Update(prefix);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
