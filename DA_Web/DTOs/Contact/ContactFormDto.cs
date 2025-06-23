using System.ComponentModel.DataAnnotations;

namespace DA_Web.DTOs.Contact
{
    public class ContactFormDto
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [StringLength(100)]
        public string Name { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
        [StringLength(100)]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề.")]
        [StringLength(200)]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung.")]
        [StringLength(2000)]
        public string Message { get; set; }
    }
}