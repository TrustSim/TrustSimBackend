using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, SimCategoryDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateCategoryCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SimCategoryDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.CategoryCode.Trim().ToUpperInvariant();
        if (await _unitOfWork.SimCategories.ExistsByCodeAsync(normalizedCode, cancellationToken: cancellationToken))
        {
            throw new BadRequestException($"Mã thể loại SIM '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        var category = new SimCategory
        {
            Id = Guid.NewGuid(),
            CategoryCode = normalizedCode,
            CategoryName = request.CategoryName.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            ValuationMultiplier = request.ValuationMultiplier > 0 ? request.ValuationMultiplier : 1.0f
        };

        await _unitOfWork.SimCategories.AddAsync(category, cancellationToken);
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
