
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DA_Web.ViewModels
{
    /// <summary>
    /// Request model cho việc tạo tour mới - FIXED VERSION
    /// </summary>
    public class CreateTourRequest
    {
        [Required(ErrorMessage = "Vui lòng nhập điểm đến")]
        [StringLength(200, ErrorMessage = "Điểm đến không được vượt quá 200 ký tự")]
        public string Destination { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn điểm khởi hành")]
        [StringLength(100, ErrorMessage = "Điểm khởi hành không được vượt quá 100 ký tự")]
        public string DepartureFrom { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập thời gian tour")]
        [StringLength(50, ErrorMessage = "Thời gian không được vượt quá 50 ký tự")]
        public string Duration { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mô tả tour")]
        [StringLength(2000, ErrorMessage = "Mô tả không được vượt quá 2000 ký tự")]
        public string Description { get; set; }

        [Display(Name = "Ngày khởi hành")]
        public System.DateTime? DepartureDate { get; set; }

        [Display(Name = "Ngày kết thúc")]
        public System.DateTime? ReturnDate { get; set; }

        [Display(Name = "Ảnh đại diện")]
        public IFormFile ImageFile { get; set; }

        [Display(Name = "Điểm nổi bật")]
        public List<string> Highlights { get; set; } = new List<string>();

        [Display(Name = "Lịch trình")]
        public List<ScheduleRequest> Schedules { get; set; } = new List<ScheduleRequest>();

        [Display(Name = "Dịch vụ bao gồm")]
        public List<string> Includes { get; set; } = new List<string>();

        [Display(Name = "Dịch vụ không bao gồm")]
        public List<string> Excludes { get; set; } = new List<string>();

        [Display(Name = "Lưu ý quan trọng")]
        public List<string> Notes { get; set; } = new List<string>();
    }

    /// <summary>
    /// Request model cho việc chỉnh sửa tour - FIXED VERSION
    /// </summary>
    public class EditTourRequest : CreateTourRequest
    {
        [Required]
        public int Id { get; set; }

        /// <summary>
        /// Đường dẫn ảnh hiện tại (để hiển thị preview)
        /// </summary>
        public string CurrentImagePath { get; set; }

        /// <summary>
        /// Có giữ ảnh cũ hay không (khi không upload ảnh mới)
        /// </summary>
        public bool KeepExistingImage { get; set; } = true;
    }

    /// <summary>
    /// Request model cho lịch trình của từng ngày - FIXED VERSION
    /// </summary>
    public class ScheduleRequest
    {
        [StringLength(50, ErrorMessage = "Tên ngày không được vượt quá 50 ký tự")]
        [Display(Name = "Tên ngày")]
        public string Day { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề cho ngày này")]
        [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự")]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; }

        [Display(Name = "Hoạt động")]
        public List<string> Activities { get; set; } = new List<string>();

        [Display(Name = "Địa điểm trong ngày")]
        public List<int> LocationIds { get; set; } = new List<int>();

        /// <summary>
        /// Thứ tự của ngày trong lịch trình
        /// </summary>
        public int Order { get; set; }
    }

    /// <summary>
    /// Response model cho việc hiển thị tour
    /// </summary>
    public class TourDetailViewModel
    {
        public int Id { get; set; }
        public string Destination { get; set; }
        public string DepartureFrom { get; set; }
        public string Duration { get; set; }
        public string Description { get; set; }
        public string Image { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Thông tin người tạo
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string UserFullName { get; set; }
        public string UserAvatar { get; set; }

        // Chi tiết tour
        public List<string> Highlights { get; set; } = new List<string>();
        public List<ScheduleDetailViewModel> Schedules { get; set; } = new List<ScheduleDetailViewModel>();
        public List<string> Includes { get; set; } = new List<string>();
        public List<string> Excludes { get; set; } = new List<string>();
        public List<string> Notes { get; set; } = new List<string>();
        public List<LocationViewModel> Locations { get; set; } = new List<LocationViewModel>();
    }

    /// <summary>
    /// View model cho lịch trình chi tiết
    /// </summary>
    public class ScheduleDetailViewModel
    {
        public int Id { get; set; }
        public string Day { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public List<string> Activities { get; set; } = new List<string>();
        public List<LocationViewModel> Locations { get; set; } = new List<LocationViewModel>();
    }

    /// <summary>
    /// View model cho địa điểm
    /// </summary>
    public class LocationViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Image { get; set; }
        public string Address { get; set; }
    }

    /// <summary>
    /// Request model cho tìm kiếm và lọc tour
    /// </summary>
    public class TourSearchRequest
    {
        [Display(Name = "Từ khóa")]
        public string Keyword { get; set; }

        [Display(Name = "Điểm đến")]
        public string Destination { get; set; }

        [Display(Name = "Điểm khởi hành")]
        public string DepartureFrom { get; set; }

        [Display(Name = "Thời gian tối thiểu (ngày)")]
        public int? MinDuration { get; set; }

        [Display(Name = "Thời gian tối đa (ngày)")]
        public int? MaxDuration { get; set; }

        [Display(Name = "Trạng thái")]
        public string Status { get; set; }

        [Display(Name = "Người tạo")]
        public int? UserId { get; set; }

        [Display(Name = "Từ ngày")]
        public DateTime? FromDate { get; set; }

        [Display(Name = "Đến ngày")]
        public DateTime? ToDate { get; set; }

        // Pagination
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        // Sorting
        public string SortBy { get; set; } = "CreatedAt";
        public string SortDirection { get; set; } = "desc";
    }

    /// <summary>
    /// Response model cho danh sách tour có phân trang
    /// </summary>
    public class TourListResponse
    {
        public List<TourDetailViewModel> Tours { get; set; } = new List<TourDetailViewModel>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasPrevious { get; set; }
        public bool HasNext { get; set; }
    }

    /// <summary>
    /// Request model cho cập nhật trạng thái tour (admin)
    /// </summary>
    public class UpdateTourStatusRequest
    {
        [Required]
        public int TourId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái")]
        public string Status { get; set; }

        [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
        public string AdminNote { get; set; }
    }
}