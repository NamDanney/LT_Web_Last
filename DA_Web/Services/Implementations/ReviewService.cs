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
                    var uploadPath = $"reviews/tour_{reviewDto.TourId}";
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
    }
}