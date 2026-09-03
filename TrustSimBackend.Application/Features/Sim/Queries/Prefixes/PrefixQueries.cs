using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries.Prefixes;

public record GetPrefixesQuery(Guid? CarrierId = null) : IRequest<ApiResponse<IReadOnlyList<PhonePrefixDto>>>;

public class GetPrefixesQueryHandler : IRequestHandler<GetPrefixesQuery, ApiResponse<IReadOnlyList<PhonePrefixDto>>>
{
    private readonly IPhonePrefixRepository _prefixRepository;

    public GetPrefixesQueryHandler(IPhonePrefixRepository prefixRepository)
    {
        _prefixRepository = prefixRepository;
    }

    public async Task<ApiResponse<IReadOnlyList<PhonePrefixDto>>> Handle(GetPrefixesQuery request, CancellationToken cancellationToken)
    {
        var prefixes = await _prefixRepository.GetAllAsync(request.CarrierId, cancellationToken);
        var dtos = prefixes.Select(p => new PhonePrefixDto
        {
            Id = p.Id,
            PrefixValue = p.PrefixValue,
            SimCarrierId = p.SimCarrierId,
            CarrierName = p.SimCarrier?.CarrierName ?? string.Empty
        }).ToList();

        return ApiResponse<IReadOnlyList<PhonePrefixDto>>.SuccessResult(dtos, "Lấy danh sách đầu số thành công.");
    }
}
