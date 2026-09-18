using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, SimCategoryDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetCategoryByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SimCategoryDto> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _unitOfWork.SimCategories.GetByIdAsync(request.Id, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException($"Không tìm thấy thể loại SIM với ID '{request.Id}'.");
        }

        return new SimCategoryDto
        {
            Id = category.Id,
            CategoryCode = category.CategoryCode,
            CategoryName = category.CategoryName,
            Description = category.Description,
            ValuationMultiplier = category.ValuationMultiplier
        };
    }
}
