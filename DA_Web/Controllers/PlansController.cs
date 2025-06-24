using DA_Web.Data;
using DA_Web.Models;
using DA_Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DA_Web.Controllers
{
    [Route("plans")]
    public class PlansController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public PlansController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }
        [HttpGet("")]
        public async Task<IActionResult> Index(int page = 1)
        {
            try
            {
                int pageSize = 6;
                int skip = (page - 1) * pageSize;

                var tours = await _context.Tours
                    .Include(t => t.User)
                    .Where(t => t.Status == Models.Enums.TourStatus.approved)
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                int totalTours = await _context.Tours
                    .Where(t => t.Status == Models.Enums.TourStatus.approved)
                    .CountAsync();
                int totalPages = (int)Math.Ceiling(totalTours / (double)pageSize);

                ViewData["CurrentPage"] = page;
                ViewData["TotalPages"] = totalPages;

                return View(tours);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong PlanController.Index: {ex.Message}");
                return Content($"Đã xảy ra lỗi khi tải trang: {ex.Message}");
            }
        }

            [HttpGet("details/{id}")]
            public async Task<IActionResult> Details(int id)
            {
                var tour = await _context.Tours
                    .Include(t => t.User)
                    .Include(t => t.TourHighlights)
                    .Include(t => t.TourSchedules)
                        .ThenInclude(s => s.ScheduleActivities)
                    .Include(t => t.TourSchedules)
                        .ThenInclude(s => s.TourLocations)
                            .ThenInclude(tl => tl.Location)
                    .Include(t => t.TourIncludes)
                    .Include(t => t.TourExcludes)
                    .Include(t => t.TourNotes)
                    .Include(t => t.Reviews)
                        .ThenInclude(r => r.User)
                    .Include(t => t.Reviews)
                        .ThenInclude(r => r.ReviewImages)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (tour == null)
                {
                    return NotFound();
                }

                if (tour.Status != Models.Enums.TourStatus.approved)
                {
                    var currentUserId = GetCurrentUserId();
                    if (tour.UserId != currentUserId && !User.IsInRole("admin"))
                    {
                        return Forbid("Bạn không có quyền truy cập vào tour này.");
                    }
                }

                // Truyền CurrentUserId để view có thể hiển thị nút sửa/xóa review
                ViewBag.CurrentUserId = GetCurrentUserId();

                return View(tour);
            }

        // GET: Plan/Create
        [HttpGet("create")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Locations = await _context.Locations.ToListAsync();
            return View();
        }

        // POST: Plan/Create
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateTourRequest request)
        {
            request.Highlights ??= new List<string>();
            request.Includes ??= new List<string>();
            request.Excludes ??= new List<string>();
            request.Notes ??= new List<string>();
            request.Schedules ??= new List<ScheduleRequest>();
            foreach (var schedule in request.Schedules)
            {
                schedule.Activities ??= new List<string>();
                schedule.LocationIds ??= new List<int>();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Locations = await _context.Locations.ToListAsync();
                ModelState.AddModelError(string.Empty, "Dữ liệu gửi lên không hợp lệ. Vui lòng kiểm tra lại các thông báo lỗi.");
                return View(request);
            }

            var tour = new Tour
            {
                Destination = request.Destination,
                DepartureFrom = request.DepartureFrom,
                Duration = request.Duration,
                Description = request.Description,
                CreatedAt = DateTime.Now,
                Status = Models.Enums.TourStatus.pending,
                UserId = GetCurrentUserId(),
                TourHighlights = request.Highlights.Where(i => !string.IsNullOrWhiteSpace(i)).Select(h => new TourHighlight { Highlight = h.Trim() }).ToList(),
                TourIncludes = request.Includes.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => new TourInclude { Description = i.Trim() }).ToList(),
                TourExcludes = request.Excludes.Where(i => !string.IsNullOrWhiteSpace(i)).Select(e => new TourExclude { Description = e.Trim() }).ToList(),
                TourNotes = request.Notes.Where(i => !string.IsNullOrWhiteSpace(i)).Select(n => new TourNote { Description = n.Trim() }).ToList(),
                TourSchedules = new List<TourSchedule>()
            };

            if (request.ImageFile != null && request.ImageFile.Length > 0)
            {
                tour.Image = await SaveImageFile(request.ImageFile);
            }
            else
            {
                tour.Image = "/uploads/tours/default-tour.jpg";
            }

            foreach (var scheduleRequest in request.Schedules.Where(s => !string.IsNullOrWhiteSpace(s.Title)))
            {
                var newSchedule = new TourSchedule
                {
                    Day = scheduleRequest.Day,
                    Title = scheduleRequest.Title.Trim(),
                    ScheduleActivities = scheduleRequest.Activities.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => new ScheduleActivity { Activity = a.Trim() }).ToList(),
                    TourLocations = scheduleRequest.LocationIds.Select(locId => new TourLocation { LocationId = locId }).ToList()
                };
                tour.TourSchedules.Add(newSchedule);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Tours.Add(tour);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Tạo lịch trình thành công! Lịch trình đang chờ được duyệt.";
                return RedirectToAction(nameof(MyPlans));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Lỗi khi tạo tour: {ex.ToString()}");
                TempData["Error"] = "Có lỗi nghiêm trọng xảy ra khi lưu lịch trình. Vui lòng thử lại.";
                ViewBag.Locations = await _context.Locations.ToListAsync();
                return View(request);
            }
        }

        // GET: Plan/Edit/5
        [Authorize]
        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var tour = await _context.Tours
                .Include(t => t.TourHighlights)
                .Include(t => t.TourSchedules).ThenInclude(s => s.ScheduleActivities)
                .Include(t => t.TourSchedules).ThenInclude(s => s.TourLocations).ThenInclude(tl => tl.Location)
                .Include(t => t.TourIncludes)
                .Include(t => t.TourExcludes)
                .Include(t => t.TourNotes)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tour == null) return NotFound();

            if (tour.UserId != GetCurrentUserId() && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var viewModel = new EditTourRequest
            {
                Id = tour.Id,
                Destination = tour.Destination,
                DepartureFrom = tour.DepartureFrom,
                Duration = tour.Duration,
                Description = tour.Description,
                CurrentImagePath = tour.Image,
                Highlights = tour.TourHighlights.Select(h => h.Highlight).ToList(),
                Includes = tour.TourIncludes.Select(i => i.Description).ToList(),
                Excludes = tour.TourExcludes.Select(e => e.Description).ToList(),
                Notes = tour.TourNotes.Select(n => n.Description).ToList(),
                Schedules = tour.TourSchedules.OrderBy(s => s.Day).Select(s => new ScheduleRequest
                {
                    Day = s.Day,
                    Title = s.Title,
                    Activities = s.ScheduleActivities.Select(a => a.Activity).ToList(),
                    LocationIds = s.TourLocations.Select(tl => tl.LocationId).ToList()
                }).ToList()
            };

            ViewBag.Locations = await _context.Locations.ToListAsync();
            return View(viewModel);
        }

        // POST: Plan/Edit/5
        [HttpPost("edit/{id}")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, EditTourRequest request)
        {
            if (id != request.Id) return BadRequest();

            if (!ModelState.IsValid)
            {
                ViewBag.Locations = await _context.Locations.ToListAsync();
                return View(request);
            }

            var tourToUpdate = await _context.Tours
                .Include(t => t.TourHighlights)
                .Include(t => t.TourSchedules).ThenInclude(s => s.ScheduleActivities)
                .Include(t => t.TourSchedules).ThenInclude(s => s.TourLocations)
                .Include(t => t.TourIncludes)
                .Include(t => t.TourExcludes)
                .Include(t => t.TourNotes)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tourToUpdate == null) return NotFound();

            if (tourToUpdate.UserId != GetCurrentUserId() && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                tourToUpdate.Destination = request.Destination;
                tourToUpdate.DepartureFrom = request.DepartureFrom;
                tourToUpdate.Duration = request.Duration;
                tourToUpdate.Description = request.Description;
                tourToUpdate.Status = Models.Enums.TourStatus.pending;

                if (request.ImageFile != null)
                {
                    if (!string.IsNullOrEmpty(tourToUpdate.Image) && tourToUpdate.Image != "/uploads/tours/default-tour.jpg")
                    {
                        await DeleteOldImage(tourToUpdate.Image);
                    }
                    tourToUpdate.Image = await SaveImageFile(request.ImageFile);
                }

                _context.Tour_Highlights.RemoveRange(tourToUpdate.TourHighlights);
                _context.Tour_Includes.RemoveRange(tourToUpdate.TourIncludes);
                _context.Tour_Excludes.RemoveRange(tourToUpdate.TourExcludes);
                _context.Tour_Notes.RemoveRange(tourToUpdate.TourNotes);
                foreach (var schedule in tourToUpdate.TourSchedules)
                {
                    _context.Schedule_Activities.RemoveRange(schedule.ScheduleActivities);
                    _context.Tour_Locations.RemoveRange(schedule.TourLocations);
                }
                _context.Tour_Schedules.RemoveRange(tourToUpdate.TourSchedules);
                await _context.SaveChangesAsync();

                request.Highlights ??= new List<string>();
                request.Includes ??= new List<string>();
                request.Excludes ??= new List<string>();
                request.Notes ??= new List<string>();
                request.Schedules ??= new List<ScheduleRequest>();

                tourToUpdate.TourHighlights = request.Highlights.Where(i => !string.IsNullOrWhiteSpace(i)).Select(h => new TourHighlight { Highlight = h.Trim() }).ToList();
                tourToUpdate.TourIncludes = request.Includes.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => new TourInclude { Description = i.Trim() }).ToList();
                tourToUpdate.TourExcludes = request.Excludes.Where(i => !string.IsNullOrWhiteSpace(i)).Select(e => new TourExclude { Description = e.Trim() }).ToList();
                tourToUpdate.TourNotes = request.Notes.Where(i => !string.IsNullOrWhiteSpace(i)).Select(n => new TourNote { Description = n.Trim() }).ToList();
                foreach (var scheduleRequest in request.Schedules.Where(s => !string.IsNullOrWhiteSpace(s.Title)))
                {
                    var newSchedule = new TourSchedule
                    {
                        Day = scheduleRequest.Day,
                        Title = scheduleRequest.Title.Trim(),
                        ScheduleActivities = scheduleRequest.Activities.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => new ScheduleActivity { Activity = a.Trim() }).ToList(),
                        TourLocations = scheduleRequest.LocationIds.Select(locId => new TourLocation { LocationId = locId }).ToList()
                    };
                    tourToUpdate.TourSchedules.Add(newSchedule);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Cập nhật lịch trình thành công! Lịch trình đã được gửi để chờ duyệt lại.";
                return RedirectToAction(nameof(MyPlans));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Lỗi khi cập nhật tour: {ex.ToString()}");
                TempData["Error"] = "Có lỗi xảy ra khi cập nhật. Vui lòng thử lại.";
                ViewBag.Locations = await _context.Locations.ToListAsync();
                return View(request);
            }
        }

        // POST: Plan/Delete/5
        [HttpPost("delete/{id}")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var tour = await _context.Tours
                .Include(t => t.TourSchedules).ThenInclude(s => s.ScheduleActivities)
                .Include(t => t.TourSchedules).ThenInclude(s => s.TourLocations)
                .Include(t => t.TourHighlights)
                .Include(t => t.TourIncludes)
                .Include(t => t.TourExcludes)
                .Include(t => t.TourNotes)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tour == null) return NotFound();

            if (tour.UserId != GetCurrentUserId() && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var schedule in tour.TourSchedules)
                {
                    _context.Schedule_Activities.RemoveRange(schedule.ScheduleActivities);
                    _context.Tour_Locations.RemoveRange(schedule.TourLocations);
                }
                _context.Tour_Schedules.RemoveRange(tour.TourSchedules);
                _context.Tour_Highlights.RemoveRange(tour.TourHighlights);
                _context.Tour_Includes.RemoveRange(tour.TourIncludes);
                _context.Tour_Excludes.RemoveRange(tour.TourExcludes);
                _context.Tour_Notes.RemoveRange(tour.TourNotes);
                _context.Tours.Remove(tour);

                if (!string.IsNullOrEmpty(tour.Image) && tour.Image != "/uploads/tours/default-tour.jpg")
                {
                    await DeleteOldImage(tour.Image);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Xóa lịch trình thành công.";
                return Ok(new { success = true, redirectUrl = Url.Action("MyPlans") });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine(ex.ToString());
                return StatusCode(500, new { success = false, message = "Lỗi xóa tour." });
            }
        }

        [Authorize]
        [HttpGet("my-plans")]
        public async Task<IActionResult> MyPlans()
        {
            var userId = GetCurrentUserId();
            var myPlans = await _context.Tours
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return View(myPlans);
        }

        // API: Lấy danh sách ảnh tours
        [HttpGet("images")]
        public IActionResult GetTourImages()
        {
            try
            {
                var uploadsDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tours");

                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var imageFiles = Directory.GetFiles(uploadsDir)
                    .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLower()))
                    .Select(file => new
                    {
                        name = Path.GetFileName(file),
                        url = $"/upload/tours/{Path.GetFileName(file)}",
                        size = new FileInfo(file).Length,
                        createdAt = new FileInfo(file).CreationTime
                    })
                    .ToList();

                return Json(new { success = true, images = imageFiles });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy danh sách ảnh: {ex.Message}");
                return Json(new { success = false, message = "Lỗi khi lấy danh sách ảnh", error = ex.Message });
            }
        }

        // API: Upload ảnh tour
        [HttpPost]
        public async Task<IActionResult> UploadTourImage(IFormFile image)
        {
            try
            {
                if (image == null || image.Length == 0)
                {
                    return Json(new { success = false, message = "Không có file nào được upload" });
                }

                var fileExt = Path.GetExtension(image.FileName).ToLower();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                if (!allowedExtensions.Contains(fileExt))
                {
                    return Json(new { success = false, message = "Định dạng file không được hỗ trợ." });
                }

                if (image.Length > 5 * 1024 * 1024)
                {
                    return Json(new { success = false, message = "Kích thước file quá lớn. Giới hạn tối đa 5MB" });
                }

                var imageUrl = await SaveImageFile(image);
                var fileName = Path.GetFileName(imageUrl);

                return Json(new
                {
                    success = true,
                    message = "Upload thành công",
                    imageUrl = imageUrl,
                    fileName = fileName,
                    size = image.Length
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi upload ảnh: {ex.Message}");
                return Json(new { success = false, message = "Lỗi khi upload ảnh", error = ex.Message });
            }
        }

        #region Helper Methods

        private int GetCurrentUserId()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdValue, out var id) ? id : 0;
        }

        private async Task<string> SaveImageFile(IFormFile imageFile)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tours");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }
            return "/uploads/tours/" + uniqueFileName;
        }

        private Task DeleteOldImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return Task.CompletedTask;

            string fullPath = Path.Combine(_webHostEnvironment.WebRootPath, imagePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
            return Task.CompletedTask;
        }

        #endregion
    }
}