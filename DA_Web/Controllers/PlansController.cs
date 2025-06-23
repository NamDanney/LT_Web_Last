//using DA_Web.Data;
//using DA_Web.Models;
//using DA_Web.ViewModels;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Rendering;
//using Microsoft.EntityFrameworkCore;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Security.Claims;
//using System.Threading.Tasks;

//namespace DA_Web.Controllers
//{
//    [Route("plans")]
//    public class PlansController : Controller
//    {
//        private readonly ApplicationDbContext _context;
//        private readonly IWebHostEnvironment _webHostEnvironment;

//        public PlansController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
//        {
//            _context = context;
//            _webHostEnvironment = webHostEnvironment;
//        }
//        [HttpGet("")]
//        public async Task<IActionResult> Index(int page = 1)
//        {
//            try
//            {
//                int pageSize = 6;
//                int skip = (page - 1) * pageSize;

//                var tours = await _context.Tours
//                    .Include(t => t.User)
//                    .Where(t => t.Status == Models.Enums.TourStatus.approved)
//                    .OrderByDescending(t => t.CreatedAt)
//                    .Skip(skip)
//                    .Take(pageSize)
//                    .ToListAsync();

//                int totalTours = await _context.Tours
//                    .Where(t => t.Status == Models.Enums.TourStatus.approved)
//                    .CountAsync();
//                int totalPages = (int)Math.Ceiling(totalTours / (double)pageSize);

//                ViewData["CurrentPage"] = page;
//                ViewData["TotalPages"] = totalPages;

//                return View(tours);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Lỗi trong PlanController.Index: {ex.Message}");
//                return Content($"Đã xảy ra lỗi khi tải trang: {ex.Message}");
//            }
//        }

//            [HttpGet("details/{id}")]
//            public async Task<IActionResult> Details(int id)
//            {
//                var tour = await _context.Tours
//                    .Include(t => t.User)
//                    .Include(t => t.TourHighlights)
//                    .Include(t => t.TourSchedules)
//                        .ThenInclude(s => s.ScheduleActivities)
//                    .Include(t => t.TourSchedules)
//                        .ThenInclude(s => s.TourLocations)
//                            .ThenInclude(tl => tl.Location)
//                    .Include(t => t.TourIncludes)
//                    .Include(t => t.TourExcludes)
//                    .Include(t => t.TourNotes)
//                    .Include(t => t.Reviews)
//                        .ThenInclude(r => r.User) // Lấy cả thông tin người dùng của mỗi đánh giá
//                    .AsSplitQuery() // Tối ưu hóa hiệu suất truy vấn
//                    .FirstOrDefaultAsync(t => t.Id == id);

//                if (tour == null)
//                {
//                    return NotFound();
//                }

//                if (tour.Status != Models.Enums.TourStatus.approved)
//                {
//                    var currentUserId = GetCurrentUserId();
//                    if (tour.UserId != currentUserId && !User.IsInRole("admin"))
//                    {
//                        return Forbid("Bạn không có quyền truy cập vào tour này.");
//                    }
//                }

//                return View(tour);
//            }

//        // GET: Plan/Create
//        [HttpGet("create")]
//        public async Task<IActionResult> Create()
//        {
//            ViewBag.Locations = await _context.Locations.ToListAsync();
//            return View();
//        }

//        // POST: Plan/Create
//        [HttpPost("create")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Create(CreateTourRequest request)
//        {
//            request.Highlights ??= new List<string>();
//            request.Includes ??= new List<string>();
//            request.Excludes ??= new List<string>();
//            request.Notes ??= new List<string>();
//            request.Schedules ??= new List<ScheduleRequest>();
//            foreach (var schedule in request.Schedules)
//            {
//                schedule.Activities ??= new List<string>();
//                schedule.LocationIds ??= new List<int>();
//            }

//            if (!ModelState.IsValid)
//            {
//                ViewBag.Locations = await _context.Locations.ToListAsync();
//                ModelState.AddModelError(string.Empty, "Dữ liệu gửi lên không hợp lệ. Vui lòng kiểm tra lại các thông báo lỗi.");
//                return View(request);
//            }

//            var tour = new Tour
//            {
//                Destination = request.Destination,
//                DepartureFrom = request.DepartureFrom,
//                Duration = request.Duration,
//                Description = request.Description,
//                CreatedAt = DateTime.Now,
//                Status = Models.Enums.TourStatus.pending,
//                UserId = GetCurrentUserId(),
//                TourHighlights = request.Highlights.Where(i => !string.IsNullOrWhiteSpace(i)).Select(h => new TourHighlight { Highlight = h.Trim() }).ToList(),
//                TourIncludes = request.Includes.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => new TourInclude { Description = i.Trim() }).ToList(),
//                TourExcludes = request.Excludes.Where(i => !string.IsNullOrWhiteSpace(i)).Select(e => new TourExclude { Description = e.Trim() }).ToList(),
//                TourNotes = request.Notes.Where(i => !string.IsNullOrWhiteSpace(i)).Select(n => new TourNote { Description = n.Trim() }).ToList(),
//                TourSchedules = new List<TourSchedule>()
//            };

//            if (request.ImageFile != null && request.ImageFile.Length > 0)
//            {
//                tour.Image = await SaveImageFile(request.ImageFile);
//            }
//            else
//            {
//                tour.Image = "/uploads/tours/default-tour.jpg";
//            }

//            foreach (var scheduleRequest in request.Schedules.Where(s => !string.IsNullOrWhiteSpace(s.Title)))
//            {
//                var newSchedule = new TourSchedule
//                {
//                    Day = scheduleRequest.Day,
//                    Title = scheduleRequest.Title.Trim(),
//                    ScheduleActivities = scheduleRequest.Activities.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => new ScheduleActivity { Activity = a.Trim() }).ToList(),
//                    TourLocations = scheduleRequest.LocationIds.Select(locId => new TourLocation { LocationId = locId }).ToList()
//                };
//                tour.TourSchedules.Add(newSchedule);
//            }

//            using var transaction = await _context.Database.BeginTransactionAsync();
//            try
//            {
//                _context.Tours.Add(tour);
//                await _context.SaveChangesAsync();
//                await transaction.CommitAsync();

//                TempData["Success"] = "Tạo lịch trình thành công! Lịch trình đang chờ được duyệt.";
//                return RedirectToAction(nameof(MyPlans));
//            }
//            catch (Exception ex)
//            {
//                await transaction.RollbackAsync();
//                Console.WriteLine($"Lỗi khi tạo tour: {ex.ToString()}");
//                TempData["Error"] = "Có lỗi nghiêm trọng xảy ra khi lưu lịch trình. Vui lòng thử lại.";
//                ViewBag.Locations = await _context.Locations.ToListAsync();
//                return View(request);
//            }
//        }

//        // GET: Plan/Edit/5
//        [Authorize]
//        [HttpGet("edit/{id}")]
//        public async Task<IActionResult> Edit(int id)
//        {
//            var tour = await _context.Tours
//                .Include(t => t.TourHighlights)
//                .Include(t => t.TourSchedules).ThenInclude(s => s.ScheduleActivities)
//                .Include(t => t.TourSchedules).ThenInclude(s => s.TourLocations).ThenInclude(tl => tl.Location)
//                .Include(t => t.TourIncludes)
//                .Include(t => t.TourExcludes)
//                .Include(t => t.TourNotes)
//                .AsNoTracking()
//                .FirstOrDefaultAsync(t => t.Id == id);

//            if (tour == null) return NotFound();

//            if (tour.UserId != GetCurrentUserId() && !User.IsInRole("Admin"))
//            {
//                return Forbid();
//            }

//            var viewModel = new EditTourRequest
//            {
//                Id = tour.Id,
//                Destination = tour.Destination,
//                DepartureFrom = tour.DepartureFrom,
//                Duration = tour.Duration,
//                Description = tour.Description,
//                CurrentImagePath = tour.Image,
//                Highlights = tour.TourHighlights.Select(h => h.Highlight).ToList(),
//                Includes = tour.TourIncludes.Select(i => i.Description).ToList(),
//                Excludes = tour.TourExcludes.Select(e => e.Description).ToList(),
//                Notes = tour.TourNotes.Select(n => n.Description).ToList(),
//                Schedules = tour.TourSchedules.OrderBy(s => s.Day).Select(s => new ScheduleRequest
//                {
//                    Day = s.Day,
//                    Title = s.Title,
//                    Activities = s.ScheduleActivities.Select(a => a.Activity).ToList(),
//                    LocationIds = s.TourLocations.Select(tl => tl.LocationId).ToList()
//                }).ToList()
//            };

//            ViewBag.Locations = await _context.Locations.ToListAsync();
//            return View(viewModel);
//        }

//        // POST: Plan/Edit/5

//        [HttpPost("edit/{id}")]
//        [ValidateAntiForgeryToken]
//        [Authorize]
//        public async Task<IActionResult> Edit(int id, EditTourRequest request)
//        {
//            if (id != request.Id) return BadRequest();

//            if (!ModelState.IsValid)
//            {
//                ViewBag.Locations = await _context.Locations.ToListAsync();
//                return View(request);
//            }

//            var tourToUpdate = await _context.Tours
//                .Include(t => t.TourHighlights)
//                .Include(t => t.TourSchedules).ThenInclude(s => s.ScheduleActivities)
//                .Include(t => t.TourSchedules).ThenInclude(s => s.TourLocations)
//                .Include(t => t.TourIncludes)
//                .Include(t => t.TourExcludes)
//                .Include(t => t.TourNotes)
//                .FirstOrDefaultAsync(t => t.Id == id);

//            if (tourToUpdate == null) return NotFound();

//            if (tourToUpdate.UserId != GetCurrentUserId() && !User.IsInRole("Admin"))
//            {
//                return Forbid();
//            }

//            using var transaction = await _context.Database.BeginTransactionAsync();
//            try
//            {
//                tourToUpdate.Destination = request.Destination;
//                tourToUpdate.DepartureFrom = request.DepartureFrom;
//                tourToUpdate.Duration = request.Duration;
//                tourToUpdate.Description = request.Description;
//                tourToUpdate.Status = Models.Enums.TourStatus.pending;

//                if (request.ImageFile != null)
//                {
//                    if (!string.IsNullOrEmpty(tourToUpdate.Image) && tourToUpdate.Image != "/uploads/tours/default-tour.jpg")
//                    {
//                        await DeleteOldImage(tourToUpdate.Image);
//                    }
//                    tourToUpdate.Image = await SaveImageFile(request.ImageFile);
//                }

//                _context.Tour_Highlights.RemoveRange(tourToUpdate.TourHighlights);
//                _context.Tour_Includes.RemoveRange(tourToUpdate.TourIncludes);
//                _context.Tour_Excludes.RemoveRange(tourToUpdate.TourExcludes);
//                _context.Tour_Notes.RemoveRange(tourToUpdate.TourNotes);
//                foreach (var schedule in tourToUpdate.TourSchedules)
//                {
//                    _context.Schedule_Activities.RemoveRange(schedule.ScheduleActivities);
//                    _context.Tour_Locations.RemoveRange(schedule.TourLocations);
//                }
//                _context.Tour_Schedules.RemoveRange(tourToUpdate.TourSchedules);
//                await _context.SaveChangesAsync();

//                request.Highlights ??= new List<string>();
//                request.Includes ??= new List<string>();
//                request.Excludes ??= new List<string>();
//                request.Notes ??= new List<string>();
//                request.Schedules ??= new List<ScheduleRequest>();

//                tourToUpdate.TourHighlights = request.Highlights.Where(i => !string.IsNullOrWhiteSpace(i)).Select(h => new TourHighlight { Highlight = h.Trim() }).ToList();
//                tourToUpdate.TourIncludes = request.Includes.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => new TourInclude { Description = i.Trim() }).ToList();
//                tourToUpdate.TourExcludes = request.Excludes.Where(i => !string.IsNullOrWhiteSpace(i)).Select(e => new TourExclude { Description = e.Trim() }).ToList();
//                tourToUpdate.TourNotes = request.Notes.Where(i => !string.IsNullOrWhiteSpace(i)).Select(n => new TourNote { Description = n.Trim() }).ToList();
//                foreach (var scheduleRequest in request.Schedules.Where(s => !string.IsNullOrWhiteSpace(s.Title)))
//                {
//                    var newSchedule = new TourSchedule
//                    {
//                        Day = scheduleRequest.Day,
//                        Title = scheduleRequest.Title.Trim(),
//                        ScheduleActivities = scheduleRequest.Activities.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => new ScheduleActivity { Activity = a.Trim() }).ToList(),
//                        TourLocations = scheduleRequest.LocationIds.Select(locId => new TourLocation { LocationId = locId }).ToList()
//                    };
//                    tourToUpdate.TourSchedules.Add(newSchedule);
//                }

//                await _context.SaveChangesAsync();
//                await transaction.CommitAsync();

//                TempData["Success"] = "Cập nhật lịch trình thành công! Lịch trình đã được gửi để chờ duyệt lại.";
//                return RedirectToAction(nameof(MyPlans));
//            }
//            catch (Exception ex)
//            {
//                await transaction.RollbackAsync();
//                Console.WriteLine($"Lỗi khi cập nhật tour: {ex.ToString()}");
//                TempData["Error"] = "Có lỗi xảy ra khi cập nhật. Vui lòng thử lại.";
//                ViewBag.Locations = await _context.Locations.ToListAsync();
//                return View(request);
//            }
//        }

//        // POST: Plan/Delete/5
//        [HttpPost("delete/{id}")]
//        [ValidateAntiForgeryToken]
//        [Authorize]
//        public async Task<IActionResult> Delete(int id)
//        {
//            var tour = await _context.Tours
//                .Include(t => t.TourSchedules).ThenInclude(s => s.ScheduleActivities)
//                .Include(t => t.TourSchedules).ThenInclude(s => s.TourLocations)
//                .Include(t => t.TourHighlights)
//                .Include(t => t.TourIncludes)
//                .Include(t => t.TourExcludes)
//                .Include(t => t.TourNotes)
//                .FirstOrDefaultAsync(t => t.Id == id);

//            if (tour == null) return NotFound();

//            if (tour.UserId != GetCurrentUserId() && !User.IsInRole("Admin"))
//            {
//                return Forbid();
//            }

//            using var transaction = await _context.Database.BeginTransactionAsync();
//            try
//            {
//                foreach (var schedule in tour.TourSchedules)
//                {
//                    _context.Schedule_Activities.RemoveRange(schedule.ScheduleActivities);
//                    _context.Tour_Locations.RemoveRange(schedule.TourLocations);
//                }
//                _context.Tour_Schedules.RemoveRange(tour.TourSchedules);
//                _context.Tour_Highlights.RemoveRange(tour.TourHighlights);
//                _context.Tour_Includes.RemoveRange(tour.TourIncludes);
//                _context.Tour_Excludes.RemoveRange(tour.TourExcludes);
//                _context.Tour_Notes.RemoveRange(tour.TourNotes);
//                _context.Tours.Remove(tour);

//                if (!string.IsNullOrEmpty(tour.Image) && tour.Image != "/uploads/tours/default-tour.jpg")
//                {
//                    await DeleteOldImage(tour.Image);
//                }

//                await _context.SaveChangesAsync();
//                await transaction.CommitAsync();

//                TempData["Success"] = "Xóa lịch trình thành công.";
//                return Ok(new { success = true, redirectUrl = Url.Action("MyPlans") });
//            }
//            catch (Exception ex)
//            {
//                await transaction.RollbackAsync();
//                Console.WriteLine(ex.ToString());
//                return StatusCode(500, new { success = false, message = "Lỗi xóa tour." });
//            }
//        }

//        [Authorize]
//        [HttpGet("my-plans")]
//        public async Task<IActionResult> MyPlans()
//        {
//            var userId = GetCurrentUserId();
//            var myPlans = await _context.Tours
//                .Where(t => t.UserId == userId)
//                .OrderByDescending(t => t.CreatedAt)
//                .ToListAsync();
//            return View(myPlans);
//        }

//        // API: Lấy danh sách ảnh tours
//        [HttpGet("images")]
//        public IActionResult GetTourImages()
//        {
//            try
//            {
//                var uploadsDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tours");

//                if (!Directory.Exists(uploadsDir))
//                {
//                    Directory.CreateDirectory(uploadsDir);
//                }

//                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
//                var imageFiles = Directory.GetFiles(uploadsDir)
//                    .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLower()))
//                    .Select(file => new
//                    {
//                        name = Path.GetFileName(file),
//                        url = $"/upload/tours/{Path.GetFileName(file)}",
//                        size = new FileInfo(file).Length,
//                        createdAt = new FileInfo(file).CreationTime
//                    })
//                    .ToList();

//                return Json(new { success = true, images = imageFiles });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Lỗi khi lấy danh sách ảnh: {ex.Message}");
//                return Json(new { success = false, message = "Lỗi khi lấy danh sách ảnh", error = ex.Message });
//            }
//        }

//        // API: Upload ảnh tour
//        [HttpPost]
//        public async Task<IActionResult> UploadTourImage(IFormFile image)
//        {
//            try
//            {
//                if (image == null || image.Length == 0)
//                {
//                    return Json(new { success = false, message = "Không có file nào được upload" });
//                }

//                var fileExt = Path.GetExtension(image.FileName).ToLower();
//                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

//                if (!allowedExtensions.Contains(fileExt))
//                {
//                    return Json(new { success = false, message = "Định dạng file không được hỗ trợ." });
//                }

//                if (image.Length > 5 * 1024 * 1024)
//                {
//                    return Json(new { success = false, message = "Kích thước file quá lớn. Giới hạn tối đa 5MB" });
//                }

//                var imageUrl = await SaveImageFile(image);
//                var fileName = Path.GetFileName(imageUrl);

//                return Json(new
//                {
//                    success = true,
//                    message = "Upload thành công",
//                    imageUrl = imageUrl,
//                    fileName = fileName,
//                    size = image.Length
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Lỗi khi upload ảnh: {ex.Message}");
//                return Json(new { success = false, message = "Lỗi khi upload ảnh", error = ex.Message });
//            }
//        }

//        #region Helper Methods

//        private int GetCurrentUserId()
//        {
//            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
//            return int.TryParse(userIdValue, out var id) ? id : 0;
//        }

//        private async Task<string> SaveImageFile(IFormFile imageFile)
//        {
//            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tours");
//            if (!Directory.Exists(uploadsFolder))
//            {
//                Directory.CreateDirectory(uploadsFolder);
//            }
//            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
//            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
//            using (var fileStream = new FileStream(filePath, FileMode.Create))
//            {
//                await imageFile.CopyToAsync(fileStream);
//            }
//            return "/uploads/tours/" + uniqueFileName;
//        }

//        private Task DeleteOldImage(string imagePath)
//        {
//            if (string.IsNullOrEmpty(imagePath)) return Task.CompletedTask;

//            string fullPath = Path.Combine(_webHostEnvironment.WebRootPath, imagePath.TrimStart('/'));
//            if (System.IO.File.Exists(fullPath))
//            {
//                System.IO.File.Delete(fullPath);
//            }
//            return Task.CompletedTask;
//        }

//        #endregion

//        #region Partial View Actions

//        [HttpGet("add-highlight")]
//        public IActionResult AddHighlightRow(int index)
//        {
//            // Trả về một partial view cho một dòng "Điểm nổi bật" mới
//            return PartialView("Partials/_HighlightEntry", index);
//        }

//        [HttpGet("add-schedule")]
//        public async Task<IActionResult> AddScheduleRow(int index)
//        {
//            // Trả về một partial view cho một "Ngày" mới trong lịch trình
//            ViewBag.Locations = await _context.Locations.ToListAsync();
//            return PartialView("Partials/_ScheduleEntry", index);
//        }

//        [HttpGet("add-include")]
//        public IActionResult AddIncludeRow(int index)
//        {
//            return PartialView("Partials/_IncludeEntry", index);
//        }

//        [HttpGet("add-exclude")]
//        public IActionResult AddExcludeRow(int index)
//        {
//            return PartialView("Partials/_ExcludeEntry", index);
//        }

//        [HttpGet("add-note")]
//        public IActionResult AddNoteRow(int index)
//        {
//            return PartialView("Partials/_NoteEntry", index);
//        }

//        #endregion
//    }
//}


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
                    .ThenInclude(r => r.User) // Lấy cả thông tin người dùng của mỗi đánh giá
                .AsSplitQuery() // Tối ưu hóa hiệu suất truy vấn
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

            return View(tour);
        }

        // GET: Plan/Create
        [HttpGet("create")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Locations = await _context.Locations.ToListAsync();
            return View();
        }




        // ✅ FIXED VERSION - PlansController Create Method
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateTourRequest request)
        {
            Console.WriteLine("=== TOUR CREATE - FIXED VERSION ===");
            Console.WriteLine($"Request received at: {DateTime.Now}");

            // ✅ 1. CHECK USER AUTHENTICATION
            if (!User.Identity.IsAuthenticated)
            {
                Console.WriteLine("❌ User not authenticated");
                TempData["Error"] = "Bạn cần đăng nhập để tạo tour.";
                return RedirectToAction("Login", "Account");
            }

            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                Console.WriteLine("❌ Invalid User ID");
                TempData["Error"] = "Không thể xác định người dùng.";
                return RedirectToAction("Login", "Account");
            }

            // ✅ 2. INITIALIZE COLLECTIONS - CRITICAL FIX
            request.Highlights ??= new List<string>();
            request.Includes ??= new List<string>();
            request.Excludes ??= new List<string>();
            request.Notes ??= new List<string>();
            request.Schedules ??= new List<ScheduleRequest>();

            // Initialize nested collections
            foreach (var schedule in request.Schedules)
            {
                schedule.Activities ??= new List<string>();
                schedule.LocationIds ??= new List<int>();
            }

            // ✅ 3. DETAILED LOGGING
            Console.WriteLine("=== REQUEST DATA ===");
            Console.WriteLine($"Destination: '{request.Destination}'");
            Console.WriteLine($"DepartureFrom: '{request.DepartureFrom}'");
            Console.WriteLine($"Duration: '{request.Duration}'");
            Console.WriteLine($"Schedules count: {request.Schedules.Count}");

            if (request.Schedules != null)
            {
                for (int i = 0; i < request.Schedules.Count; i++)
                {
                    var schedule = request.Schedules[i];
                    Console.WriteLine($"Schedule[{i}]: Day='{schedule.Day}', Title='{schedule.Title}'");
                    Console.WriteLine($"  Activities: {schedule.Activities.Count}");
                    Console.WriteLine($"  LocationIds: [{string.Join(",", schedule.LocationIds)}]");
                }
            }

            // ✅ 4. MODEL STATE VALIDATION
            if (!ModelState.IsValid)
            {
                Console.WriteLine("❌ MODEL STATE INVALID");
                foreach (var error in ModelState)
                {
                    Console.WriteLine($"Field: {error.Key}");
                    foreach (var subError in error.Value.Errors)
                    {
                        Console.WriteLine($"  Error: {subError.ErrorMessage}");
                    }
                }

                ViewBag.Locations = await _context.Locations.ToListAsync();
                TempData["Error"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra và thử lại.";
                return View(request);
            }

            // ✅ 5. DATABASE TRANSACTION - FIXED LOGIC
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                Console.WriteLine("=== CREATING TOUR OBJECT ===");

                // Step 1: Create and save Tour
                var tour = new Tour
                {
                    Destination = request.Destination,
                    DepartureFrom = request.DepartureFrom,
                    Duration = request.Duration,
                    Description = request.Description,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Status = Models.Enums.TourStatus.pending,
                    UserId = userId
                };

                // Handle image upload
                if (request.ImageFile != null && request.ImageFile.Length > 0)
                {
                    try
                    {
                        tour.Image = await SaveImageFile(request.ImageFile);
                        Console.WriteLine($"✅ Image saved: {tour.Image}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Image save failed: {ex.Message}");
                        tour.Image = "/uploads/tours/default-tour.jpg";
                    }
                }
                else
                {
                    tour.Image = "/uploads/tours/default-tour.jpg";
                }

                _context.Tours.Add(tour);
                await _context.SaveChangesAsync(); // ✅ CRITICAL: Get Tour.Id first

                Console.WriteLine($"✅ Tour saved with ID: {tour.Id}");

                // Step 2: Create Highlights, Includes, Excludes, Notes
                Console.WriteLine("=== PROCESSING HIGHLIGHTS, INCLUDES, EXCLUDES, NOTES ===");

                // Highlights
                foreach (var highlight in request.Highlights.Where(h => !string.IsNullOrWhiteSpace(h)))
                {
                    _context.Tour_Highlights.Add(new TourHighlight
                    {
                        TourId = tour.Id,
                        Highlight = highlight.Trim()
                    });
                }

                // Includes
                foreach (var include in request.Includes.Where(i => !string.IsNullOrWhiteSpace(i)))
                {
                    _context.Tour_Includes.Add(new TourInclude
                    {
                        TourId = tour.Id,
                        Description = include.Trim()
                    });
                }

                // Excludes
                foreach (var exclude in request.Excludes.Where(e => !string.IsNullOrWhiteSpace(e)))
                {
                    _context.Tour_Excludes.Add(new TourExclude
                    {
                        TourId = tour.Id,
                        Description = exclude.Trim()
                    });
                }

                // Notes
                foreach (var note in request.Notes.Where(n => !string.IsNullOrWhiteSpace(n)))
                {
                    _context.Tour_Notes.Add(new TourNote
                    {
                        TourId = tour.Id,
                        Description = note.Trim()
                    });
                }

                await _context.SaveChangesAsync();
                Console.WriteLine("✅ Highlights, Includes, Excludes, Notes saved");

                // Step 3: Create Schedules, Activities, and Locations - FIXED LOGIC
                Console.WriteLine("=== PROCESSING SCHEDULES ===");

                foreach (var scheduleRequest in request.Schedules.Where(s => !string.IsNullOrWhiteSpace(s.Title)))
                {
                    Console.WriteLine($"Processing schedule: {scheduleRequest.Day} - {scheduleRequest.Title}");

                    // ✅ CRITICAL FIX: Create schedule first, then get its ID
                    var schedule = new TourSchedule
                    {
                        TourId = tour.Id,
                        Day = scheduleRequest.Day?.Trim() ?? "",
                        Title = scheduleRequest.Title.Trim()
                    };

                    _context.Tour_Schedules.Add(schedule);
                    await _context.SaveChangesAsync(); // ✅ CRITICAL: Get Schedule.Id

                    Console.WriteLine($"✅ Schedule saved with ID: {schedule.Id}");

                    // Add Activities for this schedule
                    if (scheduleRequest.Activities != null)
                    {
                        foreach (var activity in scheduleRequest.Activities.Where(a => !string.IsNullOrWhiteSpace(a)))
                        {
                            _context.Schedule_Activities.Add(new ScheduleActivity
                            {
                                ScheduleId = schedule.Id, // ✅ NOW WE HAVE THE ID
                                Activity = activity.Trim()
                            });
                        }
                    }

                    // ✅ CRITICAL FIX: Add Locations with proper mapping
                    if (scheduleRequest.LocationIds != null)
                    {
                        foreach (var locId in scheduleRequest.LocationIds.Where(id => id > 0))
                        {
                            // Validate location exists
                            var locationExists = await _context.Locations.AnyAsync(l => l.Id == locId);
                            if (locationExists)
                            {
                                _context.Tour_Locations.Add(new TourLocation
                                {
                                    TourId = tour.Id,           // ✅ FIXED: Include TourId
                                    ScheduleId = schedule.Id,   // ✅ FIXED: Include ScheduleId  
                                    LocationId = locId          // ✅ FIXED: Include LocationId
                                });
                                Console.WriteLine($"✅ Added location {locId} to schedule {schedule.Id}");
                            }
                            else
                            {
                                Console.WriteLine($"⚠️ Location ID {locId} does not exist");
                            }
                        }
                    }

                    await _context.SaveChangesAsync(); // Save activities and locations for this schedule
                    Console.WriteLine($"✅ Activities and locations saved for schedule {schedule.Id}");
                }

                // Final save and commit
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine("✅ TOUR CREATION SUCCESSFUL");
                Console.WriteLine($"✅ Final summary:");
                Console.WriteLine($"   - Tour ID: {tour.Id}");
                Console.WriteLine($"   - Schedules: {request.Schedules.Count(s => !string.IsNullOrWhiteSpace(s.Title))}");
                Console.WriteLine($"   - Highlights: {request.Highlights.Count(h => !string.IsNullOrWhiteSpace(h))}");

                TempData["Success"] = "Tạo lịch trình thành công! Lịch trình đang chờ được duyệt.";
                return RedirectToAction(nameof(MyPlans));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ DATABASE SAVE ERROR: {ex.Message}");
                Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ Inner Exception: {ex.InnerException.Message}");
                }

                TempData["Error"] = $"Lỗi lưu database: {ex.Message}";
                ViewBag.Locations = await _context.Locations.ToListAsync();
                return View(request);
            }
        }

        // GET: Plan/Edit/5
        [Authorize]
        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            Console.WriteLine($"=== GET EDIT TOUR ID: {id} ===");

            try
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

                if (tour == null)
                {
                    Console.WriteLine($"❌ Tour with ID {id} not found");
                    return NotFound();
                }

                Console.WriteLine($"✅ Tour found: {tour.Destination}");

                if (tour.UserId != GetCurrentUserId() && !User.IsInRole("Admin"))
                {
                    Console.WriteLine($"❌ Access denied for user {GetCurrentUserId()}");
                    return Forbid();
                }

                // ✅ DEBUG: Log loaded data
                Console.WriteLine($"=== TOUR DATA ===");
                Console.WriteLine($"Highlights: {tour.TourHighlights.Count}");
                Console.WriteLine($"Schedules: {tour.TourSchedules.Count}");
                Console.WriteLine($"Includes: {tour.TourIncludes.Count}");
                Console.WriteLine($"Excludes: {tour.TourExcludes.Count}");
                Console.WriteLine($"Notes: {tour.TourNotes.Count}");

                foreach (var schedule in tour.TourSchedules)
                {
                    Console.WriteLine($"Schedule '{schedule.Title}': {schedule.ScheduleActivities.Count} activities, {schedule.TourLocations.Count} locations");
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

                // ✅ DEBUG: Log view model
                Console.WriteLine($"=== VIEW MODEL ===");
                Console.WriteLine($"Highlights: {viewModel.Highlights.Count}");
                Console.WriteLine($"Schedules: {viewModel.Schedules.Count}");

                foreach (var schedule in viewModel.Schedules)
                {
                    Console.WriteLine($"Schedule '{schedule.Title}': {schedule.Activities.Count} activities, {schedule.LocationIds.Count} locations");
                }

                // ✅ CRITICAL: Debug locations loading - same as Create
                var locations = await _context.Locations.ToListAsync();
                Console.WriteLine($"=== LOCATIONS DEBUG ===");
                Console.WriteLine($"Total locations found: {locations?.Count ?? 0}");

                if (locations != null && locations.Any())
                {
                    for (int i = 0; i < Math.Min(3, locations.Count); i++)
                    {
                        var loc = locations[i];
                        Console.WriteLine($"Location[{i}]: Id={loc.Id}, Name='{loc.Name}'");
                    }
                }

                ViewBag.Locations = locations;
                Console.WriteLine($"✅ ViewBag.Locations set with {locations?.Count ?? 0} items");

                return View(viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GET Edit: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");

                TempData["Error"] = "Không thể tải tour để chỉnh sửa.";
                return RedirectToAction("MyPlans");
            }
        }

        // ✅ FIXED POST Edit method - Apply same logic as Create
        [HttpPost("edit/{id}")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, EditTourRequest request)
        {
            Console.WriteLine("=== EDIT TOUR - FIXED VERSION ===");
            Console.WriteLine($"Editing tour ID: {id}");

            if (id != request.Id) return BadRequest();

            // ✅ INITIALIZE COLLECTIONS - Same as Create
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
                Console.WriteLine("❌ MODEL STATE INVALID");
                foreach (var error in ModelState)
                {
                    Console.WriteLine($"Field: {error.Key}");
                    foreach (var subError in error.Value.Errors)
                    {
                        Console.WriteLine($"  Error: {subError.ErrorMessage}");
                    }
                }

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
                Console.WriteLine("=== UPDATING TOUR ===");

                // Update basic info
                tourToUpdate.Destination = request.Destination;
                tourToUpdate.DepartureFrom = request.DepartureFrom;
                tourToUpdate.Duration = request.Duration;
                tourToUpdate.Description = request.Description;
                tourToUpdate.UpdatedAt = DateTime.Now;
                tourToUpdate.Status = Models.Enums.TourStatus.pending; // Reset to pending

                // Handle image upload
                if (request.ImageFile != null && request.ImageFile.Length > 0)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(tourToUpdate.Image) && tourToUpdate.Image != "/uploads/tours/default-tour.jpg")
                        {
                            await DeleteOldImage(tourToUpdate.Image);
                        }
                        tourToUpdate.Image = await SaveImageFile(request.ImageFile);
                        Console.WriteLine($"✅ New image saved: {tourToUpdate.Image}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Image save failed: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync(); // Save tour updates first
                Console.WriteLine("✅ Tour basic info updated");

                // ✅ CRITICAL FIX: Remove all related data first (same as before)
                Console.WriteLine("=== REMOVING OLD DATA ===");

                // Remove old highlights, includes, excludes, notes
                _context.Tour_Highlights.RemoveRange(tourToUpdate.TourHighlights);
                _context.Tour_Includes.RemoveRange(tourToUpdate.TourIncludes);
                _context.Tour_Excludes.RemoveRange(tourToUpdate.TourExcludes);
                _context.Tour_Notes.RemoveRange(tourToUpdate.TourNotes);

                // Remove old schedules and their related data
                foreach (var schedule in tourToUpdate.TourSchedules)
                {
                    _context.Schedule_Activities.RemoveRange(schedule.ScheduleActivities);
                    _context.Tour_Locations.RemoveRange(schedule.TourLocations);
                }
                _context.Tour_Schedules.RemoveRange(tourToUpdate.TourSchedules);

                await _context.SaveChangesAsync();
                Console.WriteLine("✅ Old data removed");

                // ✅ FIXED: Re-create data with SAME LOGIC as Create method
                Console.WriteLine("=== CREATING NEW DATA ===");

                // Create new highlights, includes, excludes, notes
                foreach (var highlight in request.Highlights.Where(h => !string.IsNullOrWhiteSpace(h)))
                {
                    _context.Tour_Highlights.Add(new TourHighlight
                    {
                        TourId = tourToUpdate.Id,
                        Highlight = highlight.Trim()
                    });
                }

                foreach (var include in request.Includes.Where(i => !string.IsNullOrWhiteSpace(i)))
                {
                    _context.Tour_Includes.Add(new TourInclude
                    {
                        TourId = tourToUpdate.Id,
                        Description = include.Trim()
                    });
                }

                foreach (var exclude in request.Excludes.Where(e => !string.IsNullOrWhiteSpace(e)))
                {
                    _context.Tour_Excludes.Add(new TourExclude
                    {
                        TourId = tourToUpdate.Id,
                        Description = exclude.Trim()
                    });
                }

                foreach (var note in request.Notes.Where(n => !string.IsNullOrWhiteSpace(n)))
                {
                    _context.Tour_Notes.Add(new TourNote
                    {
                        TourId = tourToUpdate.Id,
                        Description = note.Trim()
                    });
                }

                await _context.SaveChangesAsync();
                Console.WriteLine("✅ New simple data created");

                // ✅ CRITICAL FIX: Create schedules with SAME LOGIC as Create
                Console.WriteLine("=== CREATING NEW SCHEDULES ===");

                foreach (var scheduleRequest in request.Schedules.Where(s => !string.IsNullOrWhiteSpace(s.Title)))
                {
                    Console.WriteLine($"Processing schedule: {scheduleRequest.Day} - {scheduleRequest.Title}");

                    // ✅ SAME AS CREATE: Create schedule first, then get its ID
                    var newSchedule = new TourSchedule
                    {
                        TourId = tourToUpdate.Id,
                        Day = scheduleRequest.Day?.Trim() ?? "",
                        Title = scheduleRequest.Title.Trim()
                    };

                    _context.Tour_Schedules.Add(newSchedule);
                    await _context.SaveChangesAsync(); // ✅ CRITICAL: Get Schedule.Id first

                    Console.WriteLine($"✅ Schedule saved with ID: {newSchedule.Id}");

                    // Add Activities for this schedule
                    if (scheduleRequest.Activities != null)
                    {
                        foreach (var activity in scheduleRequest.Activities.Where(a => !string.IsNullOrWhiteSpace(a)))
                        {
                            _context.Schedule_Activities.Add(new ScheduleActivity
                            {
                                ScheduleId = newSchedule.Id, // ✅ NOW WE HAVE THE ID
                                Activity = activity.Trim()
                            });
                        }
                    }

                    // ✅ CRITICAL FIX: Add Locations with proper mapping - SAME AS CREATE
                    if (scheduleRequest.LocationIds != null)
                    {
                        foreach (var locId in scheduleRequest.LocationIds.Where(id => id > 0))
                        {
                            // Validate location exists
                            var locationExists = await _context.Locations.AnyAsync(l => l.Id == locId);
                            if (locationExists)
                            {
                                _context.Tour_Locations.Add(new TourLocation
                                {
                                    TourId = tourToUpdate.Id,     // ✅ FIXED: Include TourId
                                    ScheduleId = newSchedule.Id,  // ✅ FIXED: Include ScheduleId  
                                    LocationId = locId            // ✅ FIXED: Include LocationId
                                });
                                Console.WriteLine($"✅ Added location {locId} to schedule {newSchedule.Id}");
                            }
                            else
                            {
                                Console.WriteLine($"⚠️ Location ID {locId} does not exist");
                            }
                        }
                    }

                    await _context.SaveChangesAsync(); // Save activities and locations for this schedule
                    Console.WriteLine($"✅ Activities and locations saved for schedule {newSchedule.Id}");
                }

                // Final commit
                await transaction.CommitAsync();

                Console.WriteLine("✅ TOUR UPDATE SUCCESSFUL");
                TempData["Success"] = "Cập nhật lịch trình thành công! Lịch trình đã được gửi để chờ duyệt lại.";
                return RedirectToAction(nameof(MyPlans));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ UPDATE ERROR: {ex.Message}");
                Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ Inner Exception: {ex.InnerException.Message}");
                }

                TempData["Error"] = $"Lỗi cập nhật: {ex.Message}";
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