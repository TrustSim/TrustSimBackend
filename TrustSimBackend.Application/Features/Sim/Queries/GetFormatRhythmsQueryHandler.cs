using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetFormatRhythmsQueryHandler : IRequestHandler<GetFormatRhythmsQuery, IReadOnlyList<FormatRhythmDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetFormatRhythmsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<FormatRhythmDto>> Handle(GetFormatRhythmsQuery request, CancellationToken cancellationToken)
    {
        var list = await _unitOfWork.FormatRhythms.GetAllAsync(cancellationToken);
        return list.Select(r => new FormatRhythmDto
        {
            Id = r.Id,
            FormatRhythmName = r.FormatRhythmName,
            Value = r.Value,
            Description = r.Description,
            SimCount = r.SimNumbers?.Count ?? 0,
            CreatedDate = r.CreatedDate
        }).ToList();
    }
}
