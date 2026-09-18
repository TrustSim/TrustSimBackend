using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class DeleteFormatRhythmCommandHandler : IRequestHandler<DeleteFormatRhythmCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFormatRhythmCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteFormatRhythmCommand request, CancellationToken cancellationToken)
    {
        var rhythm = await _unitOfWork.FormatRhythms.GetByIdAsync(request.Id, cancellationToken);
        if (rhythm == null)
        {
            throw new NotFoundException($"Không tìm thấy định dạng nhịp số với ID '{request.Id}'.");
        }

        await _unitOfWork.FormatRhythms.DeleteAsync(rhythm, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
