using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class DeleteFormatRhythmCommandHandler : IRequestHandler<DeleteFormatRhythmCommand, bool>
{
    private readonly IFormatRhythmRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFormatRhythmCommandHandler(IFormatRhythmRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteFormatRhythmCommand request, CancellationToken cancellationToken)
    {
        var rhythm = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (rhythm == null)
        {
            throw new NotFoundException($"Không tìm thấy định dạng nhịp số với ID '{request.Id}'.");
        }

        await _repository.DeleteAsync(rhythm, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
