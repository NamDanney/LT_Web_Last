using DA_Web.Data;
using DA_Web.DTOs.Contact;
using DA_Web.Models;
using DA_Web.Models.Enums;
using DA_Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DA_Web.Controllers
{
    public class ContactController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService; // Tùy chọn: để gửi mail thông báo

        public ContactController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // Action để hiển thị trang liên hệ
        [HttpGet]
        public IActionResult Index()
        {
            var model = new ContactFormDto();
            return View(model);
        }

        // Action để xử lý form gửi lên (hoạt động như một API endpoint)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(ContactFormDto model)
        {
            if (!ModelState.IsValid)
            {
                // Trả về lỗi nếu dữ liệu không hợp lệ
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join("\n", errors) });
            }

            try
            {
                var contactMessage = new ContactMessage
                {
                    Name = model.Name,
                    Email = model.Email,
                    Subject = model.Subject,
                    Message = model.Message,
                    Status = ContactStatus.pending,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ContactMessages.Add(contactMessage);
                await _context.SaveChangesAsync();

                // Tùy chọn: Gửi email thông báo cho quản trị viên
                await _emailService.SendEmailAsync("yenp96334@gmail.com", "Có tin nhắn liên hệ mới", $"Từ: {model.Name} ({model.Email})<br/>Nội dung: {model.Message}");

                return Json(new { success = true, message = "Cảm ơn bạn đã liên hệ! Chúng tôi sẽ phản hồi sớm nhất có thể." });
            }
            catch (Exception ex)
            {
                // Ghi lại lỗi (log the error)
                return Json(new { success = false, message = "Đã có lỗi xảy ra từ máy chủ, vui lòng thử lại." });
            }
        }
    }
}