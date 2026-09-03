using AutoMapper;
using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries.GetMyWallet;

/// <summary>
/// Xử lý truy vấn thông tin ví tiền của người dùng.
/// </summary>
public class GetMyWalletQueryHandler : IRequestHandler<GetMyWalletQuery, ApiResponse<WalletDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IMapper _mapper;

    public GetMyWalletQueryHandler(IWalletRepository walletRepository, IMapper mapper)
    {
        _walletRepository = walletRepository;
        _mapper = mapper;
    }

    public async Task<ApiResponse<WalletDto>> Handle(GetMyWalletQuery request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng với ID '{request.UserId}'.");
        }

        var dto = _mapper.Map<WalletDto>(wallet);
        return ApiResponse<WalletDto>.SuccessResult(dto, "Lấy thông tin ví thành công.");
    }
}
