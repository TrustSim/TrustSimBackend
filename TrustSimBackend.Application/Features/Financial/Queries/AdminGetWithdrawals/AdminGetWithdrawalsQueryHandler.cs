using AutoMapper;
using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries.AdminGetWithdrawals;

/// <summary>
/// Xử lý truy vấn danh sách yêu cầu rút tiền phục vụ quản trị viên kiểm duyệt.
/// </summary>
public class AdminGetWithdrawalsQueryHandler : IRequestHandler<AdminGetWithdrawalsQuery, ApiResponse<PagedResult<WalletTransactionDto>>>
{
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly IMapper _mapper;

    public AdminGetWithdrawalsQueryHandler(
        IWalletTransactionRepository walletTransactionRepository,
        IMapper mapper)
    {
        _walletTransactionRepository = walletTransactionRepository;
        _mapper = mapper;
    }

    public async Task<ApiResponse<PagedResult<WalletTransactionDto>>> Handle(AdminGetWithdrawalsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _walletTransactionRepository.GetPagedAllAsync(
            WalletTransactionType.WITHDRAW,
            request.Status,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<WalletTransactionDto>>(items);
        var pagedResult = PagedResult<WalletTransactionDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);

        return ApiResponse<PagedResult<WalletTransactionDto>>.SuccessResult(pagedResult, "Lấy danh sách yêu cầu rút tiền thành công.");
    }
}
