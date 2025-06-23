using System.ComponentModel.DataAnnotations;

namespace DA_Web.DTOs.Review
{
    public class CreateReviewDto
    {
        [Required]
        public int TourId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn số sao đánh giá.")]
        [Range(1, 5, ErrorMessage = "Đánh giá phải từ 1 đến 5 sao.")]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung đánh giá.")]
        [StringLength(1000)]
        public string Comment { get; set; }

        
        public List<IFormFile> Images { get; set; } = new List<IFormFile>();
    }
}