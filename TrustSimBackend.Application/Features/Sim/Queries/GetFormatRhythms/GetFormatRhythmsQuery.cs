using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries.GetFormatRhythms;

/// <summary>
/// Query lấy danh sách toàn bộ các kiểu nhịp định dạng số SIM (Sảnh tiến, Lặp đôi, Gánh...).
/// </summary>
public record GetFormatRhythmsQuery : IRequest<ApiResponse<List<FormatRhythmDto>>>;

public class GetFormatRhythmsQueryHandler : IRequestHandler<GetFormatRhythmsQuery, ApiResponse<List<FormatRhythmDto>>>
{
    private readonly ISimMasterDataRepository _masterDataRepository;

    public GetFormatRhythmsQueryHandler(ISimMasterDataRepository masterDataRepository)
    {
        _masterDataRepository = masterDataRepository;
    }

    public async Task<ApiResponse<List<FormatRhythmDto>>> Handle(GetFormatRhythmsQuery request, CancellationToken cancellationToken)
    {
        var rhythms = await _masterDataRepository.GetFormatRhythmsAsync(cancellationToken);

        var result = rhythms.Select(r => new FormatRhythmDto
        {
            Id = r.Id,
            Value = r.Value,
            FormatRhythmName = r.FormatRhythmName,
            Description = r.Description,
            IsActive = r.IsActive
        }).ToList();

        return ApiResponse<List<FormatRhythmDto>>.SuccessResult(result, "Lấy danh sách kiểu nhịp định dạng SIM thành công.");
    }
}
