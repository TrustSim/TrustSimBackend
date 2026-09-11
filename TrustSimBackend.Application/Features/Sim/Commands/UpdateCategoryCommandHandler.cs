using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, SimCategoryDto>
{
    private readonly ISimCategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCategoryCommandHandler(ISimCategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SimCategoryDto> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException($"Không tìm thấy thể loại SIM với ID '{request.Id}'.");
        }

        category.CategoryName = request.CategoryName.Trim();
        if (request.Description != null)
        {
            category.Description = request.Description.Trim();
        }
        category.ValuationMultiplier = request.ValuationMultiplier > 0 ? request.ValuationMultiplier : 1.0f;

        _categoryRepository.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
