using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class CreateFormatRhythmCommandHandler : IRequestHandler<CreateFormatRhythmCommand, FormatRhythmDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateFormatRhythmCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<FormatRhythmDto> Handle(CreateFormatRhythmCommand request, CancellationToken cancellationToken)
    {
        if (await _unitOfWork.FormatRhythms.ExistsByNameAsync(request.FormatRhythmName, cancellationToken: cancellationToken))
        {
            throw new BadRequestException($"Định dạng nhịp số '{request.FormatRhythmName}' đã tồn tại.");
        }

        var rhythm = new FormatRhythm
        {
            Id = Guid.NewGuid(),
            FormatRhythmName = request.FormatRhythmName.Trim(),
            Value = request.Value.Trim(),
            Description = request.Description.Trim(),
            CreatedDate = DateTime.UtcNow
        };

        await _unitOfWork.FormatRhythms.AddAsync(rhythm, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new FormatRhythmDto
        {
            Id = rhythm.Id,
            FormatRhythmName = rhythm.FormatRhythmName,
            Value = rhythm.Value,
            Description = rhythm.Description,
            SimCount = 0,
            CreatedDate = rhythm.CreatedDate
        };
    }
}
