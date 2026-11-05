using MediatR;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Requests;

/// <summary>
/// Command để Nhân viên Sàn (CONSULTANT) hoặc ADMIN bấm Duyệt / Chấp nhận hỗ trợ một cuộc hội thoại đang PENDING.
/// Sau khi duyệt, cuộc hội thoại lập tức trở thành riêng tư giữa người duyệt và khách hàng.
/// </summary>
public record ClaimSupportConversationCommand(
    Guid ConversationId,
    Guid StaffUserId,
    UserRole StaffRole) : IRequest<ChatConversationSummaryDto>;
