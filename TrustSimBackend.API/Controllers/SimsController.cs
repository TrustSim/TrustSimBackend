using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Quản lý toàn diện tài nguyên Số SIM, đăng ký sở hữu, đối soát viễn thông và kho SIM hệ thống.
/// </summary>
public class SimsController : BaseApiController
{
    /// <summary>
    /// [API 1: Proof of Possession] Gửi mã xác thực OTP qua SMS tới số SIM người dùng muốn xác thực quyền sở hữu.
    /// Yêu cầu: Tài khoản phải hoàn thành KYC (UserProfile.VerificationStatus == VERIFIED).
    /// </summary>
    [HttpPost("ownership-verification/send-otp")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SendOtpResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SendOwnershipOtp(
        [FromBody] SendSimVerificationOtpRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SendSimVerificationOtpCommand(request.PhoneNumber, CurrentUserId);
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<SendOtpResponseDto>.SuccessResult(result, result.Message));
    }

    /// <summary>
    /// [API 2: Proof of Legal Ownership] Xác thực mã OTP SMS và thực hiện đối soát quyền sở hữu chính chủ với Cổng viễn thông Nhà mạng (Giả lập delay).
    /// Cập nhật tự động sim_numbers (AVAILABLE, Price=0, FormatRhythmId=null), sim_ownerships (ACTIVE) và sim_verifications (VERIFIED).
    /// </summary>
    [HttpPost("ownership-verification/verify")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SimOwnershipVerificationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VerifySimOwnership(
        [FromBody] VerifySimOwnershipRequest request,
        CancellationToken cancellationToken)
    {
        var command = new VerifySimOwnershipCommand(request.PhoneNumber, request.OtpCode, CurrentUserId);
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<SimOwnershipVerificationResultDto>.SuccessResult(result, result.Message));
    }

    /// <summary>
    /// Lấy danh sách các số SIM đang thuộc quyền sở hữu của người dùng hiện tại (lọc theo trạng thái, simId, phân trang, sắp xếp, tìm kiếm).
    /// </summary>
    [HttpGet("my-ownerships")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SimOwnershipDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyOwnerships([FromQuery] SimOwnershipQueryFilter filter, CancellationToken cancellationToken)
    {
        var query = new GetMySimOwnershipsQuery(CurrentUserId, filter);
        var result = await Mediator.Send(query, cancellationToken);
        return HandlePagedResult(result, "Lấy danh sách SIM sở hữu thành công.");
    }

    /// <summary>
    /// Đăng ký số SIM mới vào hệ thống (Dành cho Người bán).
    /// Người dùng phải đăng nhập. Hệ thống sẽ tự động gán quyền sở hữu ban đầu và tạo yêu cầu xác minh.
    /// </summary>
    [HttpPost("register")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SimDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterSim(
        [FromBody] RegisterSimCommand command,
        CancellationToken cancellationToken)
    {
        command.UserAccountId = CurrentUserId;
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<SimDetailDto>.SuccessResult(result, "Đăng ký SIM vào hệ thống thành công. SIM đang ở trạng thái chờ xác minh."));
    }

    /// <summary>
    /// Lấy danh sách kho SIM thuộc sở hữu của Người dùng hiện tại (Có phân trang, khoảng giá, sắp xếp và tìm kiếm).
    /// </summary>
    [HttpGet("my-sims")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SimListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMySims(
        [FromQuery] MySimsQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMySimsQuery(filter)
        {
            UserAccountId = CurrentUserId
        };

        var result = await Mediator.Send(query, cancellationToken);
        return HandlePagedResult(result, "Lấy danh sách SIM của bạn thành công.");
    }

    /// <summary>
    /// [Admin] Lấy danh sách toàn bộ kho SIM hệ thống (Có phân trang và lọc đa tiêu chí theo Nhà mạng, Danh mục, Khoảng giá, Sắp xếp).
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SimListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminSims(
        [FromQuery] AdminSimsQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminSimsQuery(filter);
        var result = await Mediator.Send(query, cancellationToken);
        return HandlePagedResult(result, "Lấy danh sách kho SIM hệ thống thành công.");
    }

    /// <summary>
    /// Tìm kiếm và lọc danh sách SIM số đẹp trên sàn (phân trang, lọc theo nhà mạng, thể loại, đầu số, nhịp số, khoảng giá, sắp xếp động).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SimNumberDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSims(
        [FromQuery] SimPublicQueryFilter filter)
    {
        var query = new GetSimsQuery(filter);
        var result = await Mediator.Send(query);
        return HandlePagedResult(result, "Lấy danh sách SIM thành công.");
    }

    /// <summary>
    /// Xem thông tin chi tiết một số SIM theo ID (Nhà mạng, Đầu số, Danh mục, Nhịp định dạng, Người sở hữu).
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SimDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetSimDetailQuery(id), cancellationToken);
        return Ok(ApiResponse<SimDetailDto>.SuccessResult(result, "Lấy thông tin chi tiết số SIM thành công."));
    }

    /// <summary>
    /// [Admin/Consultant] Thêm mới số SIM vào kho
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN,CONSULTANT")]
    [ProducesResponseType(typeof(ApiResponse<SimNumberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSimCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimNumberDto>.SuccessResult(result, "Thêm mới số SIM thành công."));
    }

    /// <summary>
    /// [Admin/Consultant] Cập nhật thông tin và trạng thái số SIM
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN,CONSULTANT")]
    [ProducesResponseType(typeof(ApiResponse<SimNumberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateSimCommand command)
    {
        var result = await Mediator.Send(command with { Id = id });
        return Ok(ApiResponse<SimNumberDto>.SuccessResult(result, "Cập nhật số SIM thành công."));
    }

    /// <summary>
    /// [Admin] Xóa (xóa mềm) số SIM khỏi hệ thống
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new DeleteSimCommand(id));
        return Ok(ApiResponse<bool>.SuccessResult(result, "Xóa số SIM thành công."));
    }

    /// <summary>
    /// [Người bán/Seller] Bulk Import danh sách SIM số đẹp sở hữu từ file Excel (.xlsx) hoặc CSV (.csv) bằng EFCore.BulkExtensions
    /// </summary>
    [HttpPost("import")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ImportResult<SimNumberDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import([FromForm] ImportSimsRequest request)
    {
        var result = await Mediator.Send(new ImportSimsCommand(request.File, CurrentUserId, AutoVerify: false));
        return Ok(ApiResponse<ImportResult<SimNumberDto>>.SuccessResult(result, $"Import hoàn tất: {result.SuccessCount}/{result.TotalRows} SIM hợp lệ đã được nạp vào kho."));
    }

    /// <summary>
    /// Xuất danh sách SIM số đẹp theo bộ lọc ra file Excel (.xlsx)
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] AdminSimsQueryFilter filter)
    {
        var query = new ExportSimsQuery(filter);
        var bytes = await Mediator.Send(query);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"SimNumbers_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>
    /// Tải file Excel mẫu (.xlsx) để chuẩn bị dữ liệu import danh sách SIM
    /// </summary>
    [HttpGet("import-template")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImportTemplate()
    {
        var bytes = await Mediator.Send(new GetSimImportTemplateQuery());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Sim_Import_Template.xlsx");
    }
}
