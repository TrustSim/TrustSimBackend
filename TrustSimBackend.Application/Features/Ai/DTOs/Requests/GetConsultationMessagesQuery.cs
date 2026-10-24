using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public record GetConsultationMessagesQuery(
    Guid SessionId,
    ConsultationMessageQueryFilter Filter
) : IRequest<PagedResult<ConsultationMessageDto>>;
