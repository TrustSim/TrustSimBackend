using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries.Categories;

public record GetCategoriesQuery() : IRequest<ApiResponse<IReadOnlyList<SimCategoryDto>>>;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, ApiResponse<IReadOnlyList<SimCategoryDto>>>
{
    private readonly ISimCategoryRepository _categoryRepository;

    public GetCategoriesQueryHandler(ISimCategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<ApiResponse<IReadOnlyList<SimCategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        var dtos = categories.Select(c => new SimCategoryDto
        {
            Id = c.Id,
            CategoryCode = c.CategoryCode,
            CategoryName = c.CategoryName,
            Description = c.Description,
            ValuationMultiplier = c.ValuationMultiplier
        }).ToList();

        return ApiResponse<IReadOnlyList<SimCategoryDto>>.SuccessResult(dtos, "Lấy danh sách thể loại SIM thành công.");
    }
}

public record GetCategoryByIdQuery(Guid Id) : IRequest<ApiResponse<SimCategoryDto>>;

public class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, ApiResponse<SimCategoryDto>>
{
    private readonly ISimCategoryRepository _categoryRepository;

    public GetCategoryByIdQueryHandler(ISimCategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<ApiResponse<SimCategoryDto>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException($"Không tìm thấy thể loại SIM với ID '{request.Id}'.");
        }

        var dto = new SimCategoryDto
        {
            Id = category.Id,
            CategoryCode = category.CategoryCode,
            CategoryName = category.CategoryName,
            Description = category.Description,
            ValuationMultiplier = category.ValuationMultiplier
        };

        return ApiResponse<SimCategoryDto>.SuccessResult(dto, "Lấy thông tin thể loại SIM thành công.");
    }
}
