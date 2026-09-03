using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands.Categories;

public record CreateCategoryCommand(
    string CategoryCode,
    string CategoryName,
    string? Description,
    float ValuationMultiplier = 1.0f
) : IRequest<ApiResponse<SimCategoryDto>>;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, ApiResponse<SimCategoryDto>>
{
    private readonly ISimCategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCategoryCommandHandler(ISimCategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<SimCategoryDto>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.CategoryCode.Trim().ToUpperInvariant();
        if (await _categoryRepository.ExistsByCodeAsync(normalizedCode, cancellationToken: cancellationToken))
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

        await _categoryRepository.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new SimCategoryDto
        {
            Id = category.Id,
            CategoryCode = category.CategoryCode,
            CategoryName = category.CategoryName,
            Description = category.Description,
            ValuationMultiplier = category.ValuationMultiplier
        };

        return ApiResponse<SimCategoryDto>.SuccessResult(dto, "Thêm mới thể loại SIM thành công.");
    }
}

public record UpdateCategoryCommand(
    Guid Id,
    string CategoryName,
    string? Description,
    float ValuationMultiplier
) : IRequest<ApiResponse<SimCategoryDto>>;

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, ApiResponse<SimCategoryDto>>
{
    private readonly ISimCategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCategoryCommandHandler(ISimCategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<SimCategoryDto>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
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

        var dto = new SimCategoryDto
        {
            Id = category.Id,
            CategoryCode = category.CategoryCode,
            CategoryName = category.CategoryName,
            Description = category.Description,
            ValuationMultiplier = category.ValuationMultiplier
        };

        return ApiResponse<SimCategoryDto>.SuccessResult(dto, "Cập nhật thể loại SIM thành công.");
    }
}

public record DeleteCategoryCommand(Guid Id) : IRequest<ApiResponse<bool>>;

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, ApiResponse<bool>>
{
    private readonly ISimCategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCategoryCommandHandler(ISimCategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException($"Không tìm thấy thể loại SIM với ID '{request.Id}'.");
        }

        category.IsDeleted = true;
        category.DeletedDate = DateTime.UtcNow;

        _categoryRepository.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.SuccessResult(true, "Xóa thể loại SIM thành công.");
    }
}
