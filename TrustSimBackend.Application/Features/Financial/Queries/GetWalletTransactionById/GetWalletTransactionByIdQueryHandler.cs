using AutoMapper;
using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries.GetWalletTransactionById;

/// <summary>
/// Xử lý truy vấn chi tiết một giao dịch ví (kiểm tra quyền sở hữu).
/// </summary>
public class GetWalletTransactionByIdQueryHandler : IRequestHandler<GetWalletTransactionByIdQuery, ApiResponse<WalletTransactionDto>>
{
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IMapper _mapper;

    public GetWalletTransactionByIdQueryHandler(
        IWalletTransactionRepository walletTransactionRepository,
        IWalletRepository walletRepository,
        IMapper mapper)
    {
        _walletTransactionRepository = walletTransactionRepository;
        _walletRepository = walletRepository;
        _mapper = mapper;
    }

    public async Task<ApiResponse<WalletTransactionDto>> Handle(GetWalletTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var transaction = await _walletTransactionRepository.GetByIdAsync(request.TransactionId, cancellationToken);
        if (transaction == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch với ID '{request.TransactionId}'.");
        }

        var wallet = await _walletRepository.GetByIdAsync(transaction.WalletId, cancellationToken);
        if (wallet == null || wallet.UserAccountId != request.UserId)
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem chi tiết giao dịch này.");
        }

        var dto = _mapper.Map<WalletTransactionDto>(transaction);
        return ApiResponse<WalletTransactionDto>.SuccessResult(dto, "Lấy thông tin chi tiết giao dịch thành công.");
    }
}
