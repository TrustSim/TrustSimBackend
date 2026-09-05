using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, IReadOnlyList<SimCategoryDto>>
{
    private readonly ISimCategoryRepository _categoryRepository;

    public GetCategoriesQueryHandler(ISimCategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<IReadOnlyList<SimCategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        return categories.Select(c => new SimCategoryDto
        {
            Id = c.Id,
            CategoryCode = c.CategoryCode,
            CategoryName = c.CategoryName,
            Description = c.Description,
            ValuationMultiplier = c.ValuationMultiplier
        }).ToList();
    }
}
