using MediatR;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

public record AdminExportTransactionsQuery(
    AdminWalletTransactionsFilter Filter
) : IRequest<byte[]>;
