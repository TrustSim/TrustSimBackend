using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Chat;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai IChatConversationRepository sử dụng Entity Framework Core.
/// Đảm bảo quy tắc quyền riêng tư tuyệt đối cho cả hội thoại Direct (1-1) và hội thoại Support (với Nhân viên Sàn).
/// </summary>
public class ChatConversationRepository : IChatConversationRepository
{
    private readonly TrustSimDbContext _context;

    public ChatConversationRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<ChatConversation?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ChatConversations
            .Include(c => c.InitiatorUser)
                .ThenInclude(u => u!.UserProfile)
            .Include(c => c.ParticipantUser)
                .ThenInclude(u => u!.UserProfile)
            .Include(c => c.AssignedStaff)
                .ThenInclude(u => u!.UserProfile)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
    }

    public async Task<ChatConversation?> GetDirectConversationBetweenUsersAsync(
        Guid userAId,
        Guid userBId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ChatConversations
            .Include(c => c.InitiatorUser)
                .ThenInclude(u => u!.UserProfile)
            .Include(c => c.ParticipantUser)
                .ThenInclude(u => u!.UserProfile)
            .FirstOrDefaultAsync(
                c => !c.IsDeleted
                  && c.Type == ConversationType.DIRECT
                  && ((c.InitiatorUserId == userAId && c.ParticipantUserId == userBId)
                   || (c.InitiatorUserId == userBId && c.ParticipantUserId == userAId)),
                cancellationToken);
    }

    public async Task<ChatConversation?> GetActiveSupportConversationByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ChatConversations
            .Include(c => c.InitiatorUser)
                .ThenInclude(u => u!.UserProfile)
            .Include(c => c.AssignedStaff)
                .ThenInclude(u => u!.UserProfile)
            .OrderByDescending(c => c.CreatedDate)
            .FirstOrDefaultAsync(
                c => !c.IsDeleted
                  && c.Type == ConversationType.SUPPORT
                  && c.InitiatorUserId == userId
                  && c.SupportStatus != SupportConversationStatus.RESOLVED,
                cancellationToken);
    }

    public async Task<ChatConversation?> GetPendingSupportConversationByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ChatConversations
            .Include(c => c.InitiatorUser)
                .ThenInclude(u => u!.UserProfile)
            .Include(c => c.AssignedStaff)
                .ThenInclude(u => u!.UserProfile)
            .OrderByDescending(c => c.CreatedDate)
            .FirstOrDefaultAsync(
                c => !c.IsDeleted
                  && c.Type == ConversationType.SUPPORT
                  && c.InitiatorUserId == userId
                  && c.SupportStatus == SupportConversationStatus.PENDING
                  && c.AssignedStaffId == null,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ChatConversation>> GetClaimedSupportConversationsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ChatConversations
            .Include(c => c.InitiatorUser)
                .ThenInclude(u => u!.UserProfile)
            .Include(c => c.AssignedStaff)
                .ThenInclude(u => u!.UserProfile)
            .Where(c => !c.IsDeleted
                     && c.Type == ConversationType.SUPPORT
                     && c.InitiatorUserId == userId
                     && c.SupportStatus == SupportConversationStatus.CLAIMED)
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ChatConversation> Items, int TotalCount)> GetUserConversationsPagedAsync(
        Guid currentUserId,
        UserRole currentUserRole,
        ConversationType? typeFilter,
        SupportConversationStatus? supportStatusFilter,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var isStaffOrAdmin = currentUserRole == UserRole.ADMIN || currentUserRole == UserRole.CONSULTANT;

        var query = _context.ChatConversations
            .Include(c => c.InitiatorUser)
                .ThenInclude(u => u!.UserProfile)
            .Include(c => c.ParticipantUser)
                .ThenInclude(u => u!.UserProfile)
            .Include(c => c.AssignedStaff)
                .ThenInclude(u => u!.UserProfile)
            .AsNoTracking()
            .Where(c => !c.IsDeleted);

        if (isStaffOrAdmin)
        {
            // Quy tắc Riêng tư Tuyệt đối:
            // Nhân viên Sàn (CONSULTANT) hoặc ADMIN chỉ nhìn thấy:
            // 1. Hội thoại DIRECT mà chính họ là người tham gia
            // 2. Hội thoại SUPPORT đang ở trạng thái PENDING (chưa có ai nhận: AssignedStaffId == null)
            // 3. Hội thoại SUPPORT đã được CHÍNH HỌ duyệt nhận (AssignedStaffId == currentUserId)
            // -> Tuyệt đối KHÔNG nhìn thấy các hội thoại SUPPORT đã được Nhân viên/Admin khác duyệt nhận!
            query = query.Where(c =>
                (c.Type == ConversationType.DIRECT && (c.InitiatorUserId == currentUserId || c.ParticipantUserId == currentUserId))
                || (c.Type == ConversationType.SUPPORT && (
                    (c.SupportStatus == SupportConversationStatus.PENDING && c.AssignedStaffId == null && c.InitiatorUserId != currentUserId)
                    || (c.AssignedStaffId == currentUserId)
                )));
        }
        else
        {
            // Người dùng thông thường chỉ nhìn thấy:
            // 1. Hội thoại DIRECT của chính mình
            // 2. Hội thoại SUPPORT do chính mình khởi tạo với Sàn
            query = query.Where(c =>
                (c.Type == ConversationType.DIRECT && (c.InitiatorUserId == currentUserId || c.ParticipantUserId == currentUserId))
                || (c.Type == ConversationType.SUPPORT && c.InitiatorUserId == currentUserId));
        }

        if (typeFilter.HasValue)
        {
            query = query.Where(c => c.Type == typeFilter.Value);
        }

        if (supportStatusFilter.HasValue)
        {
            query = query.Where(c => c.SupportStatus == supportStatusFilter.Value);
        }

        query = query.OrderByDescending(c => c.LastMessageAt ?? c.CreatedDate);
        return await query.ToPagedTupleAsync(pageIndex, pageSize, cancellationToken);
    }

    public async Task<bool> TryClaimSupportConversationAtomicallyAsync(
        Guid conversationId,
        Guid staffUserId,
        DateTime claimedAt,
        CancellationToken cancellationToken = default)
    {
        if (_context.Database.IsRelational())
        {
            var affectedRows = await _context.ChatConversations
                .Where(c => c.Id == conversationId
                         && !c.IsDeleted
                         && c.Type == ConversationType.SUPPORT
                         && c.SupportStatus == SupportConversationStatus.PENDING
                         && c.AssignedStaffId == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(c => c.AssignedStaffId, staffUserId)
                    .SetProperty(c => c.SupportStatus, SupportConversationStatus.CLAIMED)
                    .SetProperty(c => c.ClaimedAt, claimedAt)
                    .SetProperty(c => c.LockVersion, c => c.LockVersion + 1)
                    .SetProperty(c => c.UpdatedDate, claimedAt),
                    cancellationToken);

            return affectedRows > 0;
        }

        // Fallback dành cho InMemory Database khi chạy Unit Test
        var conv = await _context.ChatConversations
            .FirstOrDefaultAsync(
                c => c.Id == conversationId
                  && !c.IsDeleted
                  && c.Type == ConversationType.SUPPORT
                  && c.SupportStatus == SupportConversationStatus.PENDING
                  && c.AssignedStaffId == null,
                cancellationToken);

        if (conv == null)
        {
            return false;
        }

        conv.AssignedStaffId = staffUserId;
        conv.SupportStatus = SupportConversationStatus.CLAIMED;
        conv.ClaimedAt = claimedAt;
        conv.LockVersion += 1;
        conv.UpdatedDate = claimedAt;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<UserAccount>> SearchVerifiedUsersByEmailOrPhoneAsync(
        string keyword,
        Guid excludeUserId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var rawTerm = keyword.Trim();
        if (string.IsNullOrWhiteSpace(rawTerm))
        {
            return Array.Empty<UserAccount>();
        }

        var lowerTerm = rawTerm.ToLowerInvariant();
        var digitsOnly = Regex.Replace(rawTerm, @"\D", "");
        var altPhone0 = digitsOnly.StartsWith("84") && digitsOnly.Length >= 10
            ? "0" + digitsOnly[2..]
            : digitsOnly;
        var altPhone84 = digitsOnly.StartsWith("0") && digitsOnly.Length >= 9
            ? "+84" + digitsOnly[1..]
            : digitsOnly;

        var query = _context.UserAccounts
            .Include(u => u.UserProfile)
            .AsNoTracking()
            .Where(u => u.Id != excludeUserId
                     && u.IsActive
                     && !u.IsDeleted
                     && u.UserProfile != null
                     && !u.UserProfile.IsDeleted
                     && u.UserProfile.VerificationStatus == VerificationStatus.VERIFIED);

        if (!string.IsNullOrEmpty(digitsOnly) && digitsOnly.Length >= 3 && !rawTerm.Contains('@'))
        {
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(lowerTerm))
                || u.UserProfile!.Email.ToLower().Contains(lowerTerm)
                || u.UserProfile.PhoneNumber.Contains(rawTerm)
                || u.UserProfile.PhoneNumber.Contains(altPhone0)
                || u.UserProfile.PhoneNumber.Contains(altPhone84));
        }
        else
        {
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(lowerTerm))
                || u.UserProfile!.Email.ToLower().Contains(lowerTerm)
                || u.UserProfile.PhoneNumber.Contains(rawTerm));
        }

        return await query
            .OrderBy(u => u.UserProfile!.FullName)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ChatConversation conversation, CancellationToken cancellationToken = default)
    {
        await _context.ChatConversations.AddAsync(conversation, cancellationToken);
    }

    public void Update(ChatConversation conversation)
    {
        var entry = _context.Entry(conversation);
        if (entry.State == EntityState.Detached)
        {
            _context.ChatConversations.Update(conversation);
        }
    }
}
