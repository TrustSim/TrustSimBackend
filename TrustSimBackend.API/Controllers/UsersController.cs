using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

public class UsersController : BaseApiController
{
    /// <summary>
    /// Người dùng gửi hồ sơ eKYC tự động tích hợp AI AWS Rekognition (Tải lên ảnh CCCD mặt trước, mặt sau và ảnh chân dung Selfie).
    /// </summary>
    [HttpPost("kyc/submit-ekyc")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<EkycVerificationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitEkyc([FromForm] SubmitEkycRequest request)
    {
        var command = new SubmitEkycVerificationCommand(
            request.FrontCardImage,
            request.BackCardImage,
            request.SelfieImage,
            CurrentUserId
        );

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<EkycVerificationResultDto>.SuccessResult(result, result.Message));
    }

    /// <summary>
    /// Người dùng gửi hồ sơ xác minh danh tính KYC thủ công (CCCD và ảnh đại diện)
    /// </summary>
    [HttpPost("kyc")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IdentityVerificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitKyc([FromBody] SubmitKycCommand command)
    {
        var result = await Mediator.Send(command with { UserId = CurrentUserId });
        return Ok(ApiResponse<IdentityVerificationDto>.SuccessResult(result, "Gửi hồ sơ xác minh KYC thành công. Vui lòng chờ phê duyệt."));
    }

    /// <summary>
    /// Lấy trạng thái hồ sơ KYC gần nhất của người dùng hiện tại
    /// </summary>
    [HttpGet("kyc/status")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IdentityVerificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyKycStatus()
    {
        var result = await Mediator.Send(new GetMyKycStatusQuery(CurrentUserId));
        return Ok(ApiResponse<IdentityVerificationDto>.SuccessResult(result, "Lấy thông tin trạng thái KYC thành công."));
    }

    /// <summary>
    /// Lấy danh sách lịch sử tất cả các lần nộp hồ sơ KYC của người dùng hiện tại
    /// </summary>
    [HttpGet("kyc/history")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<IdentityVerificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyKycHistory()
    {
        var result = await Mediator.Send(new GetMyKycHistoryQuery(CurrentUserId));
        return Ok(ApiResponse<IReadOnlyList<IdentityVerificationDto>>.SuccessResult(result, "Lấy lịch sử xác minh KYC thành công."));
    }

    /// <summary>
    /// [Admin] Lấy danh sách các hồ sơ KYC chờ duyệt (có phân trang và lọc theo trạng thái)
    /// </summary>
    [HttpGet("admin/kyc")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<IdentityVerificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingKyc(
        [FromQuery] VerificationStatus? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetPendingKycQuery(status, pageIndex, pageSize);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResult<IdentityVerificationDto>>.SuccessResult(result, "Lấy danh sách hồ sơ KYC thành công."));
    }

    /// <summary>
    /// [Admin] Xem chi tiết hồ sơ KYC kèm bằng chứng ảnh (CCCD, Selfie) và dữ liệu AI để thẩm định
    /// </summary>
    [HttpGet("admin/kyc/{verificationId:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<IdentityVerificationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetKycDetail([FromRoute] Guid verificationId)
    {
        var result = await Mediator.Send(new GetKycVerificationDetailQuery(verificationId));
        return Ok(ApiResponse<IdentityVerificationDetailDto>.SuccessResult(result, "Lấy chi tiết hồ sơ xác minh KYC thành công."));
    }

    /// <summary>
    /// [Admin] Phê duyệt hoặc từ chối hồ sơ xác minh KYC thủ công
    /// </summary>
    [HttpPost("admin/kyc/{verificationId:guid}/review")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewKyc([FromRoute] Guid verificationId, [FromBody] ReviewKycCommand command)
    {
        var result = await Mediator.Send(command with { VerificationId = verificationId, AdminUserId = CurrentUserId });
        return Ok(ApiResponse<bool>.SuccessResult(result, "Duyệt hồ sơ xác minh KYC thành công."));
    }

    /// <summary>
    /// [Admin] Lấy danh sách tài khoản thành viên trong hệ thống (phân trang, lọc theo vai trò, trạng thái, tìm kiếm tên/email/sđt)
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminUserDetailDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminGetUsers(
        [FromQuery] string? keyword,
        [FromQuery] UserRole? role,
        [FromQuery] bool? isActive,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new AdminGetUsersQuery(keyword, role, isActive, pageIndex, pageSize);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResult<AdminUserDetailDto>>.SuccessResult(result, "Lấy danh sách người dùng thành công."));
    }

    /// <summary>
    /// [Admin] Xem thông tin chi tiết một tài khoản (kèm hồ sơ, số dư ví, cặp khóa PKI)
    /// </summary>
    [HttpGet("admin/{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminGetUserById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new AdminGetUserByIdQuery(id));
        return Ok(ApiResponse<AdminUserDetailDto>.SuccessResult(result, "Lấy chi tiết người dùng thành công."));
    }

    /// <summary>
    /// [Admin] Tạo mới tài khoản nội bộ (Admin, Consultant, Đối tác...)
    /// </summary>
    [HttpPost("admin")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminCreateUser([FromBody] AdminCreateUserCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<AdminUserDetailDto>.SuccessResult(result, "Tạo tài khoản thành công."));
    }

    /// <summary>
    /// [Admin] Cập nhật thông tin tài khoản, hồ sơ và vai trò người dùng
    /// </summary>
    [HttpPut("admin/{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminUpdateUser([FromRoute] Guid id, [FromBody] AdminUpdateUserCommand command)
    {
        var result = await Mediator.Send(command with { Id = id });
        return Ok(ApiResponse<AdminUserDetailDto>.SuccessResult(result, "Cập nhật tài khoản người dùng thành công."));
    }

    /// <summary>
    /// [Admin] Khóa hoặc Kích hoạt lại tài khoản người dùng
    /// </summary>
    [HttpPatch("admin/{id:guid}/status")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminToggleUserStatus([FromRoute] Guid id, [FromQuery] bool isActive)
    {
        var result = await Mediator.Send(new AdminToggleUserStatusCommand(id, isActive));
        var msg = isActive ? "Kích hoạt tài khoản thành công." : "Khóa tài khoản thành công.";
        return Ok(ApiResponse<bool>.SuccessResult(result, msg));
    }

    /// <summary>
    /// [Admin] Xuất danh sách người dùng ra file Excel (.xlsx)
    /// </summary>
    [HttpGet("admin/export")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminExportUsers(
        [FromQuery] string? keyword,
        [FromQuery] UserRole? role,
        [FromQuery] bool? isActive)
    {
        var bytes = await Mediator.Send(new AdminExportUsersQuery(keyword, role, isActive));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Users_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>
    /// [Admin] Xuất danh sách báo cáo thẩm định KYC ra file Excel (.xlsx)
    /// </summary>
    [HttpGet("admin/kyc/export")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminExportKyc([FromQuery] VerificationStatus? status)
    {
        var bytes = await Mediator.Send(new AdminExportKycQuery(status));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"KYC_Verifications_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }
}


