using DA_Web.DTOs.Common;
using DA_Web.DTOs.Review;
using DA_Web.Models; 

namespace DA_Web.Services.Interfaces
{
    public interface IReviewService
    {
        Task<ApiResponse<ReviewItemDto>> CreateReviewAsync(CreateReviewDto reviewDto, int userId);
    }
}