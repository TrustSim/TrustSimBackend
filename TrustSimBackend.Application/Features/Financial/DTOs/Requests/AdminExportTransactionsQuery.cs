using MediatR;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

public record AdminExportTransactionsQuery(
    WalletTransactionType? Type = null,
    TransactionStatus? Status = null
) : IRequest<byte[]>;
