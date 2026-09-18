using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class ExportCategoriesQueryHandler : IRequestHandler<ExportCategoriesQuery, byte[]>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelService _excelService;

    public ExportCategoriesQueryHandler(IUnitOfWork unitOfWork, IExcelService excelService)
    {
        _unitOfWork = unitOfWork;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(ExportCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _unitOfWork.SimCategories.GetAllAsync(cancellationToken);
        var rows = categories.Select(c => new CategoryExcelRow
        {
            CategoryName = c.CategoryName,
            Description = c.Description
        }).ToList();
        return await _excelService.WriteExcelAsync(rows, sheetName: "Categories", cancellationToken: cancellationToken);
    }
}
