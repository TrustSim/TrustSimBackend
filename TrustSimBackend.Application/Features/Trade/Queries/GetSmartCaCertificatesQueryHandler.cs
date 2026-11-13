using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Queries;

/// <summary>
/// Handler truy vấn danh sách chứng thư số VNPT SmartCA của Bên Mua hoặc Bên Bán trong giao dịch.
/// </summary>
public class GetSmartCaCertificatesQueryHandler : IRequestHandler<GetSmartCaCertificatesQuery, IReadOnlyList<SmartCaCertificateDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISmartCaService _smartCaService;

    public GetSmartCaCertificatesQueryHandler(
        IUnitOfWork unitOfWork,
        ISmartCaService smartCaService)
    {
        _unitOfWork = unitOfWork;
        _smartCaService = smartCaService;
    }

    public async Task<IReadOnlyList<SmartCaCertificateDto>> Handle(
        GetSmartCaCertificatesQuery request,
        CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{request.TradeId}'.");

        var isBuyer = trade.BuyerId == request.UserId;
        var isSeller = trade.SellerId == request.UserId;

        if (!isBuyer && !isSeller)
        {
            throw new ForbiddenAccessException("Bạn không phải là Bên Mua hoặc Bên Bán của giao dịch này.");
        }

        var user = isBuyer ? trade.Buyer : trade.Seller;
        var citizenId = !string.IsNullOrWhiteSpace(request.CitizenId)
            ? request.CitizenId.Trim()
            : user?.UserProfile?.IdentityNumber?.Trim();

        if (string.IsNullOrWhiteSpace(citizenId))
        {
            throw new BadRequestException("Không tìm thấy số CCCD/CMND hợp lệ trong hồ sơ eKYC để tra cứu chứng thư số VNPT SmartCA.");
        }

        var certs = await _smartCaService.GetUserCertificatesAsync(citizenId, request.SerialNumber, cancellationToken);
        if (certs == null || certs.Count == 0)
        {
            throw new BadRequestException($"Không tìm thấy chứng thư số VNPT SmartCA nào đang hoạt động cho số CCCD '{citizenId}'.");
        }

        return certs;
    }
}
