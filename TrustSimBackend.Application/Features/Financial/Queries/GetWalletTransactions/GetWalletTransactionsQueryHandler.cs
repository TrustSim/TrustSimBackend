using AutoMapper;
using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries.GetWalletTransactions;

/// <summary>
/// Xử lý truy vấn phân trang danh sách lịch sử nạp / rút tiền ví cá nhân.
/// </summary>
public class GetWalletTransactionsQueryHandler : IRequestHandler<GetWalletTransactionsQuery, ApiResponse<PagedResult<WalletTransactionDto>>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly IMapper _mapper;

    public GetWalletTransactionsQueryHandler(
        IWalletRepository walletRepository,
        IWalletTransactionRepository walletTransactionRepository,
        IMapper mapper)
    {
        _walletRepository = walletRepository;
        _walletTransactionRepository = walletTransactionRepository;
        _mapper = mapper;
    }

    public async Task<ApiResponse<PagedResult<WalletTransactionDto>>> Handle(GetWalletTransactionsQuery request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng với ID '{request.UserId}'.");
        }

        var (items, totalCount) = await _walletTransactionRepository.GetPagedByWalletIdAsync(
            wallet.Id,
            request.Type,
            request.Status,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<WalletTransactionDto>>(items);
        var pagedResult = PagedResult<WalletTransactionDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);

        return ApiResponse<PagedResult<WalletTransactionDto>>.SuccessResult(pagedResult, "Lấy lịch sử giao dịch thành công.");
    }
}
