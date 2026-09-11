using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetFormatRhythmsQueryHandler : IRequestHandler<GetFormatRhythmsQuery, IReadOnlyList<FormatRhythmDto>>
{
    private readonly IFormatRhythmRepository _repository;

    public GetFormatRhythmsQueryHandler(IFormatRhythmRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<FormatRhythmDto>> Handle(GetFormatRhythmsQuery request, CancellationToken cancellationToken)
    {
        var list = await _repository.GetAllAsync(cancellationToken);
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
