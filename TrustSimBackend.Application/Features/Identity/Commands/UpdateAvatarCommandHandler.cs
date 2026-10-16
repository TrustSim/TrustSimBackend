using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý tải lên và cập nhật ảnh đại diện của người dùng lên Supabase Storage và dọn dẹp ảnh cũ.
/// </summary>
public class UpdateAvatarCommandHandler : IRequestHandler<UpdateAvatarCommand, string>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<UpdateAvatarCommandHandler> _logger;

    public UpdateAvatarCommandHandler(
        IUnitOfWork unitOfWork,
        ICloudStorageService cloudStorageService,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<UpdateAvatarCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cloudStorageService = cloudStorageService;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task<string> Handle(UpdateAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException(nameof(UserAccount), request.UserId);
        }

        if (user.UserProfile == null)
        {
            user.UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = user.Id,
                Email = user.UserEmail
            };
        }

        var oldAvatarUrl = user.UserProfile.AvatarImg;

        // 1. Tải ảnh mới lên Supabase Storage thư mục "avatars"
        var newAvatarUrl = await _cloudStorageService.UploadImageAsync(request.File, "avatars", cancellationToken);

        // 2. Cập nhật URL ảnh mới vào UserProfile
        user.UserProfile.AvatarImg = newAvatarUrl;
        user.UserProfile.UpdatedDate = DateTime.UtcNow;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 3. Xóa file ảnh cũ trên Supabase Storage chạy nền (Fire-and-forget), không chặn luồng phản hồi API
        if (!string.IsNullOrWhiteSpace(oldAvatarUrl) && !oldAvatarUrl.Contains("default", StringComparison.OrdinalIgnoreCase))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var storageService = scope.ServiceProvider.GetRequiredService<ICloudStorageService>();
                    await storageService.DeleteFileAsync(oldAvatarUrl, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Avatar] Cảnh báo không thể xóa ảnh đại diện cũ chạy nền trên Supabase Storage: {Url}", oldAvatarUrl);
                }
            });
        }

        _logger.LogInformation("[Avatar] Cập nhật ảnh đại diện thành công cho UserId: {UserId}, NewUrl: {Url}", user.Id, newAvatarUrl);
        return user.UserProfile.AvatarImg;
    }
}


