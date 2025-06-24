// File: DA_Web/Services/Implementations/ReviewService.cs

using DA_Web.Data;
using DA_Web.DTOs.Common;
using DA_Web.DTOs.Review;
using DA_Web.Models;
using DA_Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DA_Web.Services.Implementations
{
    public class ReviewService : IReviewService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;
        private readonly ILogger<ReviewService> _logger;

        public ReviewService(ApplicationDbContext context, IFileService fileService, ILogger<ReviewService> logger)
        {
            _context = context;
            _fileService = fileService;
            _logger = logger;
        }

        public async Task<ApiResponse<ReviewItemDto>> CreateReviewAsync(CreateReviewDto reviewDto, int userId)
        {
            // Sử dụng transaction để đảm bảo toàn vẹn dữ liệu
            // Nếu có lỗi ở bất kỳ bước nào, tất cả thay đổi sẽ được hoàn tác.
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var hasAlreadyReviewed = await _context.Reviews
                    .AnyAsync(r => r.TourId == reviewDto.TourId && r.UserId == userId);
                if (hasAlreadyReviewed)
                {
                    return ApiResponse<ReviewItemDto>.ErrorResult("Bạn đã gửi đánh giá cho tour này rồi.");
                }

                // 1. Lưu các tệp hình ảnh trước
                var imagePaths = new List<string>();
                if (reviewDto.Images != null && reviewDto.Images.Any())
                {
                    var uploadPath = "review_images";
                    imagePaths = await _fileService.SaveFilesAsync(reviewDto.Images, uploadPath);
                }

                // 2. Tạo đối tượng Review chính, KHÔNG bao gồm các ảnh
                var newReview = new Review
                {
                    UserId = userId,
                    TourId = reviewDto.TourId,
                    Rating = reviewDto.Rating,
                    Comment = reviewDto.Comment,
                    CreatedAt = DateTime.UtcNow
                };

                // 3. Thêm Review vào context và LƯU LẠI để lấy Id
                _context.Reviews.Add(newReview);
                await _context.SaveChangesAsync();

                // 4. Bây giờ `newReview.Id` đã có giá trị. Tạo các đối tượng ReviewImage
                if (imagePaths.Any())
                {
                    var reviewImages = imagePaths.Select(path => new ReviewImage
                    {
                        ReviewId = newReview.Id, // Sử dụng Id vừa được tạo
                        ImageUrl = path
                    }).ToList();

                    // Thêm danh sách các ảnh vào context và lưu lại lần nữa
                    await _context.Review_Images.AddRangeAsync(reviewImages);
                    await _context.SaveChangesAsync();
                }

                // 5. Hoàn tất transaction
                await transaction.CommitAsync();

                // 6. Lấy thông tin người dùng để trả về DTO
                var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

                // 7. Tạo DTO kết quả hoàn chỉnh để gửi về client
                var resultDto = new ReviewItemDto
                {
                    Id = newReview.Id,
                    Rating = newReview.Rating,
                    Comment = newReview.Comment,
                    CreatedAt = newReview.CreatedAt,
                    UserName = user?.Username ?? "Người dùng ẩn danh",
                    UserAvatar = user?.Avatar,
                    Images = imagePaths // Trả về danh sách đường dẫn ảnh
                };

                return ApiResponse<ReviewItemDto>.SuccessResult(resultDto, "Đánh giá của bạn đã được gửi thành công!");
            }
            catch (Exception ex)
            {
                // Nếu có lỗi, hoàn tác tất cả thay đổi
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi tạo review cho TourID {TourId}", reviewDto.TourId);
                return ApiResponse<ReviewItemDto>.ErrorResult("Đã xảy ra lỗi nghiêm trọng ở máy chủ.");
            }
        }

        public async Task<ApiResponse<ReviewItemDto>> UpdateReviewAsync(int reviewId, CreateReviewDto reviewDto, int userId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Kiểm tra review có tồn tại và thuộc về user hiện tại không
                var existingReview = await _context.Reviews
                    .Include(r => r.ReviewImages)
                    .Include(r => r.User)
                    .FirstOrDefaultAsync(r => r.Id == reviewId);

                if (existingReview == null)
                {
                    return ApiResponse<ReviewItemDto>.ErrorResult("Đánh giá không tồn tại.");
                }

                if (existingReview.UserId != userId)
                {
                    return ApiResponse<ReviewItemDto>.ErrorResult("Bạn không có quyền chỉnh sửa đánh giá này.");
                }

                // Cập nhật thông tin cơ bản
                existingReview.Rating = reviewDto.Rating;
                existingReview.Comment = reviewDto.Comment;

                // Xử lý hình ảnh mới nếu có
                var imagePaths = new List<string>();
                if (reviewDto.Images != null && reviewDto.Images.Any())
                {
                    // Xóa ảnh cũ
                    foreach (var oldImage in existingReview.ReviewImages)
                    {
                        try
                        {
                            await _fileService.DeleteFileAsync(oldImage.ImageUrl);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Không thể xóa ảnh cũ: {ImageUrl}", oldImage.ImageUrl);
                        }
                    }
                    
                    _context.Review_Images.RemoveRange(existingReview.ReviewImages);
                    
                    // Upload ảnh mới
                    var uploadPath = "review_images";
                    imagePaths = await _fileService.SaveFilesAsync(reviewDto.Images, uploadPath);
                    
                    // Tạo ReviewImage mới
                    var newReviewImages = imagePaths.Select(path => new ReviewImage
                    {
                        ReviewId = existingReview.Id,
                        ImageUrl = path
                    }).ToList();
                    
                    await _context.Review_Images.AddRangeAsync(newReviewImages);
                }
                else
                {
                    // Giữ nguyên ảnh cũ
                    imagePaths = existingReview.ReviewImages.Select(ri => ri.ImageUrl).ToList();
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Trả về DTO
                var resultDto = new ReviewItemDto
                {
                    Id = existingReview.Id,
                    Rating = existingReview.Rating,
                    Comment = existingReview.Comment,
                    CreatedAt = existingReview.CreatedAt,
                    UserName = existingReview.User?.Username ?? "Người dùng ẩn danh",
                    UserAvatar = existingReview.User?.Avatar,
                    Images = imagePaths
                };

                return ApiResponse<ReviewItemDto>.SuccessResult(resultDto, "Đánh giá đã được cập nhật thành công!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi cập nhật review ID {ReviewId}", reviewId);
                return ApiResponse<ReviewItemDto>.ErrorResult("Đã xảy ra lỗi khi cập nhật đánh giá.");
            }
        }

        public async Task<ApiResponse<bool>> DeleteReviewAsync(int reviewId, int userId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existingReview = await _context.Reviews
                    .Include(r => r.ReviewImages)
                    .FirstOrDefaultAsync(r => r.Id == reviewId);

                if (existingReview == null)
                {
                    return ApiResponse<bool>.ErrorResult("Đánh giá không tồn tại.");
                }

                if (existingReview.UserId != userId)
                {
                    return ApiResponse<bool>.ErrorResult("Bạn không có quyền xóa đánh giá này.");
                }

                // Xóa ảnh liên quan
                foreach (var reviewImage in existingReview.ReviewImages)
                {
                    try
                    {
                        await _fileService.DeleteFileAsync(reviewImage.ImageUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Không thể xóa ảnh: {ImageUrl}", reviewImage.ImageUrl);
                    }
                }

                // Xóa review và ảnh khỏi database
                _context.Review_Images.RemoveRange(existingReview.ReviewImages);
                _context.Reviews.Remove(existingReview);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<bool>.SuccessResult(true, "Đánh giá đã được xóa thành công!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi xóa review ID {ReviewId}", reviewId);
                return ApiResponse<bool>.ErrorResult("Đã xảy ra lỗi khi xóa đánh giá.");
            }
        }

        public async Task<ApiResponse<ReviewItemDto>> GetReviewByIdAsync(int reviewId)
        {
            try
            {
                var review = await _context.Reviews
                    .Include(r => r.User)
                    .Include(r => r.ReviewImages)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == reviewId);

                if (review == null)
                {
                    return ApiResponse<ReviewItemDto>.ErrorResult("Đánh giá không tồn tại.");
                }

                var resultDto = new ReviewItemDto
                {
                    Id = review.Id,
                    Rating = review.Rating,
                    Comment = review.Comment,
                    CreatedAt = review.CreatedAt,
                    UserName = review.User?.Username ?? "Người dùng ẩn danh",
                    UserAvatar = review.User?.Avatar,
                    Images = review.ReviewImages?.Select(ri => ri.ImageUrl).ToList() ?? new List<string>()
                };

                return ApiResponse<ReviewItemDto>.SuccessResult(resultDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy review ID {ReviewId}", reviewId);
                return ApiResponse<ReviewItemDto>.ErrorResult("Đã xảy ra lỗi khi lấy thông tin đánh giá.");
            }
        }
    }
}