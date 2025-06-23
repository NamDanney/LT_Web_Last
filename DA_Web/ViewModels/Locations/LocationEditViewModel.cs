using System.ComponentModel.DataAnnotations;

namespace DA_Web.ViewModels.Locations
{
    public class LocationEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên địa điểm")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại địa điểm")]
        public string Type { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mô tả")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập vĩ độ")]
        public decimal Latitude { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập kinh độ")]
        public decimal Longitude { get; set; }

        // Location Details
        public string? Subtitle { get; set; }
        public string? Introduction { get; set; }
        public string? WhyVisitArchitectureTitle { get; set; }
        public string? WhyVisitArchitectureText { get; set; }
        public string? WhyVisitCulture { get; set; }

        // Travel Info
        public string? TicketPrice { get; set; }
        public string? Tip { get; set; }

        // Collections
        public List<string>? BestTimes { get; set; } = new List<string>();
        public List<string>? Tips { get; set; } = new List<string>();
        public List<string>? TravelMethodsFromTuyHoa { get; set; } = new List<string>();
        public List<string>? TravelMethodsFromElsewhere { get; set; } = new List<string>();
        public List<string>? Experiences { get; set; } = new List<string>();
        public List<string>? Cuisines { get; set; } = new List<string>();
        public List<int>? NearbyLocationIds { get; set; } = new List<int>();
        public List<int>? HotelIds { get; set; } = new List<int>();

        // Images
        [Display(Name = "Thêm hình ảnh mới")]
        public IFormFileCollection? Images { get; set; }
        public IFormFile? IntroductionImage { get; set; }
        public IFormFile? ArchitectureImage { get; set; }
        public List<IFormFile>? ExperienceImages { get; set; }
        public List<IFormFile>? CuisineImages { get; set; }
    }
}
