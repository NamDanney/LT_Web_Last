using DA_Web.DTOs.Common;
using DA_Web.DTOs.Review;
using DA_Web.Models; 

namespace DA_Web.Services.Interfaces
{
    public interface IReviewService
    {
        Task<ApiResponse<ReviewItemDto>> CreateReviewAsync(CreateReviewDto reviewDto, int userId);
        Task<ApiResponse<ReviewItemDto>> UpdateReviewAsync(int reviewId, CreateReviewDto reviewDto, int userId);
        Task<ApiResponse<bool>> DeleteReviewAsync(int reviewId, int userId);
        Task<ApiResponse<ReviewItemDto>> GetReviewByIdAsync(int reviewId);
    }
}