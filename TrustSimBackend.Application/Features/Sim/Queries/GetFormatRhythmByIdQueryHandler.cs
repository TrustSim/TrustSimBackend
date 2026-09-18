using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetFormatRhythmByIdQueryHandler : IRequestHandler<GetFormatRhythmByIdQuery, FormatRhythmDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetFormatRhythmByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<FormatRhythmDto> Handle(GetFormatRhythmByIdQuery request, CancellationToken cancellationToken)
    {
        var rhythm = await _unitOfWork.FormatRhythms.GetByIdAsync(request.Id, cancellationToken);
        if (rhythm == null)
        {
            throw new NotFoundException($"Không tìm thấy định dạng nhịp số với ID '{request.Id}'.");
        }

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
