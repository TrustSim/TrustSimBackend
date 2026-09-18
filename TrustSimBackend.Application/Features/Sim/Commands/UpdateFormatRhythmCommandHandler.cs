using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class UpdateFormatRhythmCommandHandler : IRequestHandler<UpdateFormatRhythmCommand, FormatRhythmDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFormatRhythmCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<FormatRhythmDto> Handle(UpdateFormatRhythmCommand request, CancellationToken cancellationToken)
    {
        var rhythm = await _unitOfWork.FormatRhythms.GetByIdAsync(request.Id, cancellationToken);
        if (rhythm == null)
        {
            throw new NotFoundException($"Không tìm thấy định dạng nhịp số với ID '{request.Id}'.");
        }

        if (await _unitOfWork.FormatRhythms.ExistsByNameAsync(request.FormatRhythmName, request.Id, cancellationToken))
        {
            throw new BadRequestException($"Tên định dạng nhịp số '{request.FormatRhythmName}' đã tồn tại.");
        }

        rhythm.FormatRhythmName = request.FormatRhythmName.Trim();
        rhythm.Value = request.Value.Trim();
        rhythm.Description = request.Description.Trim();
        rhythm.UpdatedDate = DateTime.UtcNow;

        await _unitOfWork.FormatRhythms.UpdateAsync(rhythm, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new FormatRhythmDto
        {
            Id = rhythm.Id,
            FormatRhythmName = rhythm.FormatRhythmName,
            Value = rhythm.Value,
            Description = rhythm.Description,
            SimCount = rhythm.SimNumbers?.Count ?? 0,
            CreatedDate = rhythm.CreatedDate
        };
    }
}
