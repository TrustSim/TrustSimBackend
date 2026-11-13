using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Controller xử lý thị giác máy tính và nhận diện ký tự quang học (OCR) qua Google Cloud Vision.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VisionController : BaseApiController
{
    private readonly IGoogleVisionService _visionService;

    public VisionController(IGoogleVisionService visionService)
    {
        _visionService = visionService;
    }

    /// <summary>
    /// Nhận diện toàn bộ chữ (OCR) từ hình ảnh tải lên qua multipart/form-data.
    /// </summary>
    /// <param name="file">Tệp tin hình ảnh cần nhận diện.</param>
    /// <param name="cancellationToken">CancellationToken.</param>
    [HttpPost("ocr/detect-text")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<GoogleVisionOcrResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DetectText(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.FailureResult("Vui lòng tải lên một tệp tin hình ảnh hợp lệ."));
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(ApiResponse<object>.FailureResult("Định dạng tệp không được hỗ trợ. Vui lòng tải lên ảnh JPG, PNG, WEBP hoặc BMP."));
        }

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        var result = await _visionService.DetectAllTextAsync(bytes, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(ApiResponse<GoogleVisionOcrResult>.FailureResult(result.ErrorMessage ?? "Nhận diện văn bản thất bại."));
        }

        return Ok(ApiResponse<GoogleVisionOcrResult>.SuccessResult(result, "Nhận diện văn bản OCR thành công."));
    }
}
