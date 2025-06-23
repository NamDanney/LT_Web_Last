// File: DA_Web/Controllers/ReviewController.cs

using DA_Web.DTOs.Common;
using DA_Web.DTOs.Review;
using DA_Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DA_Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly ILogger<ReviewController> _logger;

        public ReviewController(IReviewService reviewService, ILogger<ReviewController> logger)
        {
            _reviewService = reviewService;
            _logger = logger;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PostReview([FromForm] CreateReviewDto reviewDto)
        {
            try
            {
                _logger.LogInformation("=== BẮT ĐẦU XỬ LÝ REVIEW ===");
                _logger.LogInformation("TourId: {TourId}, Rating: {Rating}", reviewDto.TourId, reviewDto.Rating);

                // Kiểm tra ModelState
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState không hợp lệ");
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                                                  .Select(e => e.ErrorMessage)
                                                  .ToList();
                    var combinedErrorMessage = string.Join(" | ", errors);
                    return BadRequest(ApiResponse<object>.ErrorResult(combinedErrorMessage));
                }

                // Kiểm tra user
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _logger.LogInformation("UserIdString từ Claims: {UserIdString}", userIdString);

                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
                {
                    _logger.LogWarning("Không thể lấy UserId từ Claims");
                    return Unauthorized(ApiResponse<object>.ErrorResult("Không thể xác thực người dùng."));
                }

                _logger.LogInformation("UserId parsed: {UserId}", userId);

                // Gọi service - ĐÂY LÀ NƠI CÓ THỂ XẢY RA LỖI
                _logger.LogInformation("Bắt đầu gọi ReviewService.CreateReviewAsync");

                var result = await _reviewService.CreateReviewAsync(reviewDto, userId);

                _logger.LogInformation("ReviewService.CreateReviewAsync hoàn thành. Success: {Success}", result.Success);

                if (!result.Success)
                {
                    _logger.LogWarning("Service trả về lỗi: {Message}", result.Message);
                    return BadRequest(result);
                }

                _logger.LogInformation("=== HOÀN THÀNH XỬ LÝ REVIEW THÀNH CÔNG ===");
                return Ok(result);
            }
            catch (Exception ex)
            {
                // LOGGING CHI TIẾT LỖI
                _logger.LogError(ex, "=== LỖI TRONG CONTROLLER ===");
                _logger.LogError("Exception Type: {ExceptionType}", ex.GetType().Name);
                _logger.LogError("Exception Message: {ExceptionMessage}", ex.Message);
                _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);

                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner Exception: {InnerExceptionMessage}", ex.InnerException.Message);
                }

                return StatusCode(500, ApiResponse<object>.ErrorResult($"Lỗi máy chủ: {ex.Message}"));
            }
        }
    }
}