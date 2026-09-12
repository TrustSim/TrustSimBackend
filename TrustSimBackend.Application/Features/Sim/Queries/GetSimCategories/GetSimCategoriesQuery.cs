using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries.GetSimCategories;

/// <summary>
/// Query lấy danh sách toàn bộ danh mục SIM (Tứ quý, Lộc phát, Thần tài...) kèm hệ số định giá.
/// </summary>
public record GetSimCategoriesQuery : IRequest<ApiResponse<List<SimCategoryDto>>>;

public class GetSimCategoriesQueryHandler : IRequestHandler<GetSimCategoriesQuery, ApiResponse<List<SimCategoryDto>>>
{
    private readonly ISimMasterDataRepository _masterDataRepository;

    public GetSimCategoriesQueryHandler(ISimMasterDataRepository masterDataRepository)
    {
        _masterDataRepository = masterDataRepository;
    }

    public async Task<ApiResponse<List<SimCategoryDto>>> Handle(GetSimCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _masterDataRepository.GetCategoriesAsync(cancellationToken);

        var result = categories.Select(c => new SimCategoryDto
        {
            Id = c.Id,
            CategoryCode = c.CategoryCode,
            CategoryName = c.CategoryName,
            Description = c.Description,
            ValuationMultiplier = c.ValuationMultiplier,
            IsActive = c.IsActive
        }).ToList();

        return ApiResponse<List<SimCategoryDto>>.SuccessResult(result, "Lấy danh sách danh mục SIM thành công.");
    }
}
