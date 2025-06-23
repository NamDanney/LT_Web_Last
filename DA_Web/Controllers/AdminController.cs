
using DA_Web.Data;
using DA_Web.Models;
using DA_Web.Models.Enums;
using DA_Web.Repository;
using DA_Web.Services.Interfaces;
using DA_Web.ViewModels;
using DA_Web.ViewModels.Locations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace DA_Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly IUserService _userService;
        private readonly ApplicationDbContext _context;
        private readonly IImageService _imageService;
        private readonly ILogger<AdminController> _logger;
        private readonly string _weatherApiKey = "095cde61e730fd9406235de1237e97c1";
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminController(
            ApplicationDbContext context,
            IUserService userService,
            IImageService imageService,
            ILogger<AdminController> logger,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userService = userService;
            _imageService = imageService;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Locations/MgtListLocation
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> MgtListLocation(string search, string type)
        {
            // Lấy danh sách location từ database
            var locationsQuery = _context.Locations
                .Include(l => l.LocationDetail)
                .Include(l => l.LocationImages.Where(img => img.ImageType == LocationImageType.introduction))
                .AsQueryable();

            // Lọc theo từ khóa tìm kiếm
            if (!string.IsNullOrEmpty(search))
            {
                locationsQuery = locationsQuery.Where(l =>
                    l.Name.Contains(search) ||
                    l.Description.Contains(search) ||
                    l.LocationDetail.Subtitle.Contains(search));
            }

            // Lọc theo loại
            if (!string.IsNullOrEmpty(type))
            {
                locationsQuery = locationsQuery.Where(l => l.Type == type);
            }

            // Sắp xếp theo tên
            locationsQuery = locationsQuery.OrderBy(l => l.Name);

            // Lấy kết quả
            var locations = await locationsQuery.ToListAsync();

            // Truyền các tham số tìm kiếm vào ViewBag để hiển thị trên giao diện
            ViewBag.SearchTerm = search;
            ViewBag.SelectedType = type;

            return View(locations);
        }

        public async Task<IActionResult> DetailLocation(int? id)
        {
            if (id == null)
                return NotFound();

            var location = await _context.Locations
                .Include(l => l.LocationDetail)
                .Include(l => l.TravelInfo)
                .Include(l => l.BestTimes)
                .Include(l => l.TravelMethods)
                .Include(l => l.Experiences)
                .Include(l => l.Cuisines)
                .Include(l => l.Tips)
                .Include(l => l.LocationImages)
                .Include(l => l.NearbyLocations)
                    .ThenInclude(nl => nl.Nearby)
                .Include(l => l.LocationHotels)
                    .ThenInclude(lh => lh.Hotel)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (location == null)
                return NotFound();

            using var client = new HttpClient();
            var weatherResponse = await client.GetAsync(
                $"https://api.openweathermap.org/data/2.5/weather?lat={location.Latitude}&lon={location.Longitude}&units=metric&appid={_weatherApiKey}"
            );

            if (weatherResponse.IsSuccessStatusCode)
            {
                var weatherData = await weatherResponse.Content.ReadAsStringAsync();
                ViewBag.WeatherData = weatherData;
            }

            // Fetch forecast data
            var forecastResponse = await client.GetAsync(
                $"https://api.openweathermap.org/data/2.5/forecast?lat={location.Latitude}&lon={location.Longitude}&units=metric&appid={_weatherApiKey}"
            );

            if (forecastResponse.IsSuccessStatusCode)
            {
                var forecastData = await forecastResponse.Content.ReadAsStringAsync();
                ViewBag.ForecastData = forecastData;
            }
            return View(location);
        }

        // GET: Locations/Create
        [Authorize(Roles = "admin")]
        public IActionResult CreateLocation()
        {
            ViewBag.NearbyLocations = new SelectList(_context.Locations, "Id", "Name");
            ViewBag.Hotels = new SelectList(_context.Hotels, "Id", "Name");
            return View();
        }

        // POST: Locations/Create
        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLocation(LocationCreateViewModel model, IFormFile introductionImage,
    IFormFile architectureImage, IFormFile[] ExperienceImages, IFormFile[] CuisineImages)
        {
            if (ModelState.IsValid)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _logger.LogInformation("Tạo mới Location");

                    // 1. Tạo Location
                    var location = new Location
                    {
                        Name = model.Name,
                        Type = model.Type,
                        Description = model.Description,
                        Latitude = model.Latitude,
                        Longitude = model.Longitude,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _context.Locations.Add(location);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Location created with ID: {location.Id}");

                    // 2. Xử lý chi tiết địa điểm
                    await HandleLocationDetails(location, model);

                    // 3. Xử lý thông tin du lịch
                    await HandleTravelInfo(location, model);

                    // 4. Xử lý các collections
                    await HandleCollections(location, model);
                    _logger.LogInformation("Saved all related collections");

                    // 5. Xử lý hình ảnh
                    await HandleImages(location, introductionImage, architectureImage,
                                     ExperienceImages, CuisineImages);
                    _logger.LogInformation("All images processed");

                    await transaction.CommitAsync();

                    TempData["Success"] = "Địa điểm đã được tạo thành công!";
                    return RedirectToAction(nameof(MgtListLocation));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Error creating location: {ex.Message}");
                    _logger.LogError($"Stack trace: {ex.StackTrace}");

                    if (ex.InnerException != null)
                    {
                        _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                        _logger.LogError($"Inner stack trace: {ex.InnerException.StackTrace}");
                    }

                    ModelState.AddModelError("", $"Lỗi khi tạo địa điểm: {ex.Message}");
                }
            }

            // Nếu ModelState không hợp lệ
            PrepareViewBagData();
            return View(model);
        }

        private async Task HandleCollections(Location location, LocationCreateViewModel model)
        {
            // Handle Best Times
            if (model.BestTimes != null && model.BestTimes.Any(bt => !string.IsNullOrWhiteSpace(bt)))
            {
                foreach (var time in model.BestTimes.Where(bt => !string.IsNullOrWhiteSpace(bt)))
                {
                    _context.BestTimes.Add(new BestTime
                    {
                        LocationId = location.Id,
                        TimeDescription = time
                    });
                }
            }

            // Handle Travel Methods
            if (model.TravelMethodsFromTuyHoa != null && model.TravelMethodsFromTuyHoa.Any())
            {
                foreach (var method in model.TravelMethodsFromTuyHoa.Where(tm => !string.IsNullOrWhiteSpace(tm)))
                {
                    _context.TravelMethods.Add(new TravelMethod
                    {
                        LocationId = location.Id,
                        Description = method,
                        MethodType = TravelMethodType.fromTuyHoa
                    });
                }
            }

            if (model.TravelMethodsFromElsewhere != null && model.TravelMethodsFromElsewhere.Any())
            {
                foreach (var method in model.TravelMethodsFromElsewhere.Where(tm => !string.IsNullOrWhiteSpace(tm)))
                {
                    _context.TravelMethods.Add(new TravelMethod
                    {
                        LocationId = location.Id,
                        Description = method,
                        MethodType = TravelMethodType.fromElsewhere
                    });
                }
            }

            // Handle Experiences
            if (model.Experiences != null && model.Experiences.Any())
            {
                foreach (var experience in model.Experiences.Where(e => !string.IsNullOrWhiteSpace(e)))
                {
                    _context.Experiences.Add(new Experience
                    {
                        LocationId = location.Id,
                        Description = experience
                    });
                }
            }

            // Handle Cuisines
            if (model.Cuisines != null && model.Cuisines.Any())
            {
                foreach (var cuisine in model.Cuisines.Where(c => !string.IsNullOrWhiteSpace(c)))
                {
                    _context.Cuisines.Add(new Cuisine
                    {
                        LocationId = location.Id,
                        Description = cuisine
                    });
                }
            }

            // Handle Tips
            if (model.Tips != null && model.Tips.Any())
            {
                foreach (var tip in model.Tips.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    _context.Tips.Add(new Tip
                    {
                        LocationId = location.Id,
                        Description = tip
                    });
                }
            }

            // Handle Nearby Locations
            if (model.NearbyLocationIds != null && model.NearbyLocationIds.Any())
            {
                foreach (var nearbyId in model.NearbyLocationIds)
                {
                    var nearbyLocation = new NearbyLocation
                    {
                        LocationId = location.Id,
                        NearbyLocationId = nearbyId
                    };
                    _context.NearbyLocations.Add(nearbyLocation);
                }
            }

            // Handle Hotels
            if (model.HotelIds != null && model.HotelIds.Any())
            {
                foreach (var hotelId in model.HotelIds)
                {
                    var locationHotel = new LocationHotel
                    {
                        LocationId = location.Id,
                        HotelId = hotelId
                    };
                    _context.LocationHotels.Add(locationHotel);
                }
            }

            await _context.SaveChangesAsync();
        }

        // ✅ FIXED: Handle Images for Create
        private async Task HandleImages(Location location, IFormFile introductionImage, IFormFile architectureImage,
            IFormFile[] experienceImages, IFormFile[] cuisineImages)
        {
            string locationFolder = $"locations/{location.Id}";

            _logger.LogInformation("=== STARTING IMAGE PROCESSING ===");
            _logger.LogInformation($"Location ID: {location.Id}");
            _logger.LogInformation($"Experience images count: {experienceImages?.Length ?? 0}");
            _logger.LogInformation($"Cuisine images count: {cuisineImages?.Length ?? 0}");

            if (introductionImage != null)
            {
                _logger.LogInformation("Processing introduction image");
                string fileName = $"{location.Id}-intro{Path.GetExtension(introductionImage.FileName)}";
                var imageUrl = await _imageService.SaveImageAsync(introductionImage, locationFolder, fileName);
                var locationImage = new LocationImage
                {
                    LocationId = location.Id,
                    ImageUrl = imageUrl,
                    ImageType = LocationImageType.introduction,
                    ReferenceId = null,
                    CreatedAt = DateTime.Now
                };
                _context.Location_Images.Add(locationImage);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Introduction image saved: {imageUrl}");
            }

            if (architectureImage != null)
            {
                _logger.LogInformation("Processing architecture image");
                string fileName = $"{location.Id}-arch{Path.GetExtension(architectureImage.FileName)}";
                var imageUrl = await _imageService.SaveImageAsync(architectureImage, locationFolder, fileName);
                var locationImage = new LocationImage
                {
                    LocationId = location.Id,
                    ImageUrl = imageUrl,
                    ImageType = LocationImageType.architecture,
                    ReferenceId = null,
                    CreatedAt = DateTime.Now
                };
                _context.Location_Images.Add(locationImage);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Architecture image saved: {imageUrl}");
            }

            // Experience Images
            if (experienceImages != null && experienceImages.Any(img => img != null && img.Length > 0))
            {
                var experiences = await _context.Experiences
                    .Where(e => e.LocationId == location.Id)
                    .OrderBy(e => e.Id)
                    .ToListAsync();

                _logger.LogInformation($"Found {experiences.Count} experiences in database");

                var validExperienceImages = experienceImages
                    .Select((img, index) => new { Image = img, Index = index })
                    .Where(x => x.Image != null && x.Image.Length > 0)
                    .ToList();

                for (int i = 0; i < Math.Min(validExperienceImages.Count, experiences.Count); i++)
                {
                    var validImage = validExperienceImages[i];
                    var experienceId = experiences[i].Id;

                    try
                    {
                        string fileName = $"{location.Id}-exp-{experienceId}{Path.GetExtension(validImage.Image.FileName)}";
                        var imageUrl = await _imageService.SaveImageAsync(validImage.Image, locationFolder, fileName);

                        var locationImage = new LocationImage
                        {
                            LocationId = location.Id,
                            ImageUrl = imageUrl,
                            ImageType = LocationImageType.experience,
                            ReferenceId = experienceId,
                            CreatedAt = DateTime.Now
                        };

                        _context.Location_Images.Add(locationImage);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"Experience image saved: {imageUrl}, ReferenceId: {experienceId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error saving experience image {i}: {ex.Message}");
                        throw;
                    }
                }
            }

            // Cuisine Images
            if (cuisineImages != null && cuisineImages.Any(img => img != null && img.Length > 0))
            {
                var cuisines = await _context.Cuisines
                    .Where(c => c.LocationId == location.Id)
                    .OrderBy(c => c.Id)
                    .ToListAsync();

                _logger.LogInformation($"Found {cuisines.Count} cuisines in database");

                var validCuisineImages = cuisineImages
                    .Select((img, index) => new { Image = img, Index = index })
                    .Where(x => x.Image != null && x.Image.Length > 0)
                    .ToList();

                for (int i = 0; i < Math.Min(validCuisineImages.Count, cuisines.Count); i++)
                {
                    var validImage = validCuisineImages[i];
                    var cuisineId = cuisines[i].Id;

                    try
                    {
                        string fileName = $"{location.Id}-cui-{cuisineId}{Path.GetExtension(validImage.Image.FileName)}";
                        var imageUrl = await _imageService.SaveImageAsync(validImage.Image, locationFolder, fileName);

                        var locationImage = new LocationImage
                        {
                            LocationId = location.Id,
                            ImageUrl = imageUrl,
                            ImageType = LocationImageType.cuisine,
                            ReferenceId = cuisineId,
                            CreatedAt = DateTime.Now
                        };

                        _context.Location_Images.Add(locationImage);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"Cuisine image saved: {imageUrl}, ReferenceId: {cuisineId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error saving cuisine image {i}: {ex.Message}");
                        throw;
                    }
                }
            }

            _logger.LogInformation("=== IMAGE PROCESSING COMPLETED ===");
        }

        // Helper methods
        private async Task HandleLocationDetails(Location location, LocationCreateViewModel model)
        {
            if (!string.IsNullOrEmpty(model.Subtitle) || !string.IsNullOrEmpty(model.Introduction))
            {
                var locationDetail = new LocationDetail
                {
                    LocationId = location.Id,
                    Subtitle = model.Subtitle,
                    Introduction = model.Introduction,
                    WhyVisitArchitectureTitle = model.WhyVisitArchitectureTitle,
                    WhyVisitArchitectureText = model.WhyVisitArchitectureText,
                    WhyVisitCulture = model.WhyVisitCulture
                };
                _context.LocationDetails.Add(locationDetail);
                await _context.SaveChangesAsync();
            }
        }

        private async Task HandleTravelInfo(Location location, LocationCreateViewModel model)
        {
            if (!string.IsNullOrEmpty(model.TicketPrice) || model.Tip != null)
            {
                var travelInfo = new TravelInfo
                {
                    LocationId = location.Id,
                    TicketPrice = model.TicketPrice,
                    Tip = model.Tip != null ? string.Join(", ", model.Tip) : null
                };
                _context.TravelInfos.Add(travelInfo);
                await _context.SaveChangesAsync();
            }
        }

        private void PrepareViewBagData(int? excludeLocationId = null)
        {
            var locationsQuery = _context.Locations.AsQueryable();
            if (excludeLocationId.HasValue)
            {
                locationsQuery = locationsQuery.Where(l => l.Id != excludeLocationId);
            }
            ViewBag.NearbyLocations = new SelectList(locationsQuery, "Id", "Name");
            ViewBag.Hotels = new SelectList(_context.Hotels, "Id", "Name");
        }

        // ✅ FIXED: GET EditLocation
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> EditLocation(int? id)
        {
            if (id == null)
                return NotFound();

            var location = await _context.Locations
                .Include(l => l.LocationDetail)
                .Include(l => l.TravelInfo)
                .Include(l => l.BestTimes)
                .Include(l => l.TravelMethods)
                .Include(l => l.Experiences)
                .Include(l => l.Cuisines)
                .Include(l => l.Tips)
                .Include(l => l.LocationImages)
                .Include(l => l.NearbyLocations)
                .Include(l => l.LocationHotels)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (location == null)
                return NotFound();

            var viewModel = new LocationEditViewModel
            {
                Id = location.Id,
                Name = location.Name,
                Type = location.Type,
                Description = location.Description,
                Latitude = location.Latitude,
                Longitude = location.Longitude,

                // Thông tin chi tiết
                Subtitle = location.LocationDetail?.Subtitle,
                Introduction = location.LocationDetail?.Introduction,
                WhyVisitArchitectureTitle = location.LocationDetail?.WhyVisitArchitectureTitle,
                WhyVisitArchitectureText = location.LocationDetail?.WhyVisitArchitectureText,
                WhyVisitCulture = location.LocationDetail?.WhyVisitCulture,

                // Thông tin du lịch
                TicketPrice = location.TravelInfo?.TicketPrice,
                Tip = location.TravelInfo?.Tip,

                // Các danh sách
                BestTimes = location.BestTimes?.Select(bt => bt.TimeDescription).ToList(),
                TravelMethodsFromTuyHoa = location.TravelMethods?
                    .Where(tm => tm.MethodType == TravelMethodType.fromTuyHoa)
                    .Select(tm => tm.Description)
                    .ToList(),
                TravelMethodsFromElsewhere = location.TravelMethods?
                    .Where(tm => tm.MethodType == TravelMethodType.fromElsewhere)
                    .Select(tm => tm.Description)
                    .ToList(),
                Experiences = location.Experiences?.Select(e => e.Description).ToList(),
                Cuisines = location.Cuisines?.Select(c => c.Description).ToList(),
                Tips = location.Tips?.Select(t => t.Description).ToList(),

                // Các ID liên quan
                NearbyLocationIds = location.NearbyLocations?.Select(nl => nl.NearbyLocationId).ToList(),
                HotelIds = location.LocationHotels?.Select(lh => lh.HotelId).ToList(),
            };

            // Chuẩn bị dữ liệu cho các select lists
            PrepareViewBagData(id);

            // ✅ FIXED: Chuẩn bị dữ liệu cho hiển thị các hình ảnh hiện tại
            var currentImages = location.LocationImages?.Select(img => new {
                Id = img.Id,
                ImageUrl = img.ImageUrl,
                ImageType = img.ImageType,
                ReferenceId = img.ReferenceId,
                LocationId = img.LocationId
            }).ToList();

            ViewBag.CurrentImages = currentImages;

            // ✅ FIXED: Sắp xếp IDs theo thứ tự để mapping chính xác
            ViewBag.ExperienceIds = location.Experiences?
                .OrderBy(e => e.Id)
                .Select(e => e.Id)
                .ToList() ?? new List<int>();

            ViewBag.CuisineIds = location.Cuisines?
                .OrderBy(c => c.Id)
                .Select(c => c.Id)
                .ToList() ?? new List<int>();

            // Debug logging
            _logger.LogInformation($"EditLocation GET - Location {id}:");
            _logger.LogInformation($"- Experiences count: {location.Experiences?.Count ?? 0}");
            _logger.LogInformation($"- Cuisines count: {location.Cuisines?.Count ?? 0}");
            _logger.LogInformation($"- Total images: {currentImages?.Count ?? 0}");

            return View(viewModel);
        }

        // ✅ FIXED: POST EditLocation
        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLocation(int id, LocationEditViewModel model, IFormFile introductionImage,
            IFormFile architectureImage, IFormFile[] ExperienceImages, IFormFile[] CuisineImages)
        {
            if (id != model.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Lấy location từ database
                    var location = await _context.Locations.FindAsync(id);
                    if (location == null)
                        return NotFound();

                    // Cập nhật thông tin cơ bản
                    location.Name = model.Name;
                    location.Type = model.Type;
                    location.Description = model.Description;
                    location.Latitude = model.Latitude;
                    location.Longitude = model.Longitude;
                    location.UpdatedAt = DateTime.Now;

                    _context.Update(location);
                    await _context.SaveChangesAsync();

                    // Cập nhật chi tiết địa điểm
                    await UpdateLocationDetails(location, model);

                    // Cập nhật thông tin du lịch
                    await UpdateTravelInfo(location, model);

                    // Cập nhật các collections
                    await UpdateCollections(location, model);

                    // Kiểm tra có ảnh mới được upload
                    bool hasImageUploads =
                        (introductionImage != null && introductionImage.Length > 0) ||
                        (architectureImage != null && architectureImage.Length > 0) ||
                        (ExperienceImages != null && ExperienceImages.Any(img => img != null && img.Length > 0)) ||
                        (CuisineImages != null && CuisineImages.Any(img => img != null && img.Length > 0)) ||
                        (model.Images != null && model.Images.Any(img => img != null && img.Length > 0));

                    if (hasImageUploads)
                    {
                        await HandleEditImages(location, model.Images, introductionImage, architectureImage,
                                             ExperienceImages, CuisineImages);
                    }

                    await transaction.CommitAsync();

                    TempData["Success"] = "Cập nhật địa điểm thành công!";
                    return RedirectToAction(nameof(MgtListLocation));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Error updating location: {ex.Message}");
                    ModelState.AddModelError("", $"Có lỗi xảy ra khi cập nhật địa điểm: {ex.Message}");
                }
            }

            // Nếu có lỗi, chuẩn bị lại dữ liệu
            PrepareViewBagData(id);

            var locationWithImages = await _context.Locations
                .Include(l => l.LocationImages)
                .Include(l => l.Experiences)
                .Include(l => l.Cuisines)
                .FirstOrDefaultAsync(l => l.Id == id);

            ViewBag.CurrentImages = locationWithImages?.LocationImages?.Select(img => new {
                Id = img.Id,
                ImageUrl = img.ImageUrl,
                ImageType = img.ImageType,
                ReferenceId = img.ReferenceId,
                LocationId = img.LocationId
            }).ToList();

            ViewBag.ExperienceIds = locationWithImages?.Experiences?.OrderBy(e => e.Id).Select(e => e.Id).ToList();
            ViewBag.CuisineIds = locationWithImages?.Cuisines?.OrderBy(c => c.Id).Select(c => c.Id).ToList();

            return View(model);
        }

        // Update methods
        private async Task UpdateLocationDetails(Location location, LocationEditViewModel model)
        {
            var locationDetail = await _context.LocationDetails.FirstOrDefaultAsync(ld => ld.LocationId == location.Id);

            if (locationDetail == null)
            {
                if (!string.IsNullOrEmpty(model.Subtitle) || !string.IsNullOrEmpty(model.Introduction) ||
                    !string.IsNullOrEmpty(model.WhyVisitArchitectureTitle) || !string.IsNullOrEmpty(model.WhyVisitArchitectureText) ||
                    !string.IsNullOrEmpty(model.WhyVisitCulture))
                {
                    locationDetail = new LocationDetail
                    {
                        LocationId = location.Id,
                        Subtitle = model.Subtitle,
                        Introduction = model.Introduction,
                        WhyVisitArchitectureTitle = model.WhyVisitArchitectureTitle,
                        WhyVisitArchitectureText = model.WhyVisitArchitectureText,
                        WhyVisitCulture = model.WhyVisitCulture
                    };
                    _context.LocationDetails.Add(locationDetail);
                }
            }
            else
            {
                locationDetail.Subtitle = model.Subtitle;
                locationDetail.Introduction = model.Introduction;
                locationDetail.WhyVisitArchitectureTitle = model.WhyVisitArchitectureTitle;
                locationDetail.WhyVisitArchitectureText = model.WhyVisitArchitectureText;
                locationDetail.WhyVisitCulture = model.WhyVisitCulture;
                _context.LocationDetails.Update(locationDetail);
            }

            await _context.SaveChangesAsync();
        }

        private async Task UpdateTravelInfo(Location location, LocationEditViewModel model)
        {
            var travelInfo = await _context.TravelInfos.FirstOrDefaultAsync(ti => ti.LocationId == location.Id);

            if (travelInfo == null)
            {
                if (!string.IsNullOrEmpty(model.TicketPrice) || !string.IsNullOrEmpty(model.Tip))
                {
                    travelInfo = new TravelInfo
                    {
                        LocationId = location.Id,
                        TicketPrice = model.TicketPrice,
                        Tip = model.Tip
                    };
                    _context.TravelInfos.Add(travelInfo);
                }
            }
            else
            {
                travelInfo.TicketPrice = model.TicketPrice;
                travelInfo.Tip = model.Tip;
                _context.TravelInfos.Update(travelInfo);
            }

            await _context.SaveChangesAsync();
        }

        // ✅ FIXED: Update Collections
        private async Task UpdateCollections(Location location, LocationEditViewModel model)
        {
            // Xóa tất cả collections cũ
            _context.BestTimes.RemoveRange(_context.BestTimes.Where(bt => bt.LocationId == location.Id));
            _context.TravelMethods.RemoveRange(_context.TravelMethods.Where(tm => tm.LocationId == location.Id));
            _context.Tips.RemoveRange(_context.Tips.Where(t => t.LocationId == location.Id));

            // Lưu danh sách IDs cũ để xóa ảnh liên quan
            var oldExperienceIds = await _context.Experiences
                .Where(e => e.LocationId == location.Id)
                .Select(e => e.Id)
                .ToListAsync();

            var oldCuisineIds = await _context.Cuisines
                .Where(c => c.LocationId == location.Id)
                .Select(c => c.Id)
                .ToListAsync();

            // Xóa ảnh liên quan trước
            if (oldExperienceIds.Any())
            {
                var expImagesToRemove = _context.Location_Images
                    .Where(img => img.LocationId == location.Id &&
                                 img.ImageType == LocationImageType.experience &&
                                 oldExperienceIds.Contains(img.ReferenceId ?? 0));
                _context.Location_Images.RemoveRange(expImagesToRemove);
            }

            if (oldCuisineIds.Any())
            {
                var cuiImagesToRemove = _context.Location_Images
                    .Where(img => img.LocationId == location.Id &&
                                 img.ImageType == LocationImageType.cuisine &&
                                 oldCuisineIds.Contains(img.ReferenceId ?? 0));
                _context.Location_Images.RemoveRange(cuiImagesToRemove);
            }

            // Xóa experiences và cuisines cũ
            _context.Experiences.RemoveRange(_context.Experiences.Where(e => e.LocationId == location.Id));
            _context.Cuisines.RemoveRange(_context.Cuisines.Where(c => c.LocationId == location.Id));

            // Thêm experiences mới
            if (model.Experiences != null && model.Experiences.Any())
            {
                foreach (var experience in model.Experiences.Where(e => !string.IsNullOrWhiteSpace(e)))
                {
                    _context.Experiences.Add(new Experience
                    {
                        LocationId = location.Id,
                        Description = experience
                    });
                }
            }

            // Thêm cuisines mới
            if (model.Cuisines != null && model.Cuisines.Any())
            {
                foreach (var cuisine in model.Cuisines.Where(c => !string.IsNullOrWhiteSpace(c)))
                {
                    _context.Cuisines.Add(new Cuisine
                    {
                        LocationId = location.Id,
                        Description = cuisine
                    });
                }
            }

            // Thêm các collections khác
            if (model.BestTimes != null && model.BestTimes.Any())
            {
                foreach (var time in model.BestTimes.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    _context.BestTimes.Add(new BestTime
                    {
                        LocationId = location.Id,
                        TimeDescription = time
                    });
                }
            }

            if (model.TravelMethodsFromTuyHoa != null && model.TravelMethodsFromTuyHoa.Any())
            {
                foreach (var method in model.TravelMethodsFromTuyHoa.Where(tm => !string.IsNullOrWhiteSpace(tm)))
                {
                    _context.TravelMethods.Add(new TravelMethod
                    {
                        LocationId = location.Id,
                        Description = method,
                        MethodType = TravelMethodType.fromTuyHoa
                    });
                }
            }

            if (model.TravelMethodsFromElsewhere != null && model.TravelMethodsFromElsewhere.Any())
            {
                foreach (var method in model.TravelMethodsFromElsewhere.Where(tm => !string.IsNullOrWhiteSpace(tm)))
                {
                    _context.TravelMethods.Add(new TravelMethod
                    {
                        LocationId = location.Id,
                        Description = method,
                        MethodType = TravelMethodType.fromElsewhere
                    });
                }
            }

            if (model.Tips != null && model.Tips.Any())
            {
                foreach (var tip in model.Tips.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    _context.Tips.Add(new Tip
                    {
                        LocationId = location.Id,
                        Description = tip
                    });
                }
            }

            // Xử lý NearbyLocations và Hotels
            _context.NearbyLocations.RemoveRange(_context.NearbyLocations.Where(nl => nl.LocationId == location.Id));
            if (model.NearbyLocationIds != null && model.NearbyLocationIds.Any())
            {
                foreach (var nearbyId in model.NearbyLocationIds)
                {
                    _context.NearbyLocations.Add(new NearbyLocation
                    {
                        LocationId = location.Id,
                        NearbyLocationId = nearbyId
                    });
                }
            }

            _context.LocationHotels.RemoveRange(_context.LocationHotels.Where(lh => lh.LocationId == location.Id));
            if (model.HotelIds != null && model.HotelIds.Any())
            {
                foreach (var hotelId in model.HotelIds)
                {
                    _context.LocationHotels.Add(new LocationHotel
                    {
                        LocationId = location.Id,
                        HotelId = hotelId
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        // ✅ FIXED: Handle Edit Images
        private async Task HandleEditImages(Location location, IFormFileCollection images, IFormFile introductionImage,
            IFormFile architectureImage, IFormFile[] experienceImages, IFormFile[] cuisineImages)
        {
            string locationFolder = $"locations/{location.Id}";

            _logger.LogInformation("=== STARTING EDIT IMAGE PROCESSING ===");

            // Xử lý ảnh giới thiệu
            if (introductionImage != null && introductionImage.Length > 0)
            {
                var oldIntroImage = await _context.Location_Images
                    .FirstOrDefaultAsync(img => img.LocationId == location.Id && img.ImageType == LocationImageType.introduction);

                if (oldIntroImage != null)
                {
                    await _imageService.DeleteImageAsync(oldIntroImage.ImageUrl);
                    _context.Location_Images.Remove(oldIntroImage);
                    await _context.SaveChangesAsync();
                }

                string fileName = $"{location.Id}-intro{Path.GetExtension(introductionImage.FileName)}";
                var imageUrl = await _imageService.SaveImageAsync(introductionImage, locationFolder, fileName);
                _context.Location_Images.Add(new LocationImage
                {
                    LocationId = location.Id,
                    ImageUrl = imageUrl,
                    ImageType = LocationImageType.introduction,
                    ReferenceId = null,
                    CreatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }

            // Xử lý ảnh kiến trúc
            if (architectureImage != null && architectureImage.Length > 0)
            {
                var oldArchImage = await _context.Location_Images
                    .FirstOrDefaultAsync(img => img.LocationId == location.Id && img.ImageType == LocationImageType.architecture);

                if (oldArchImage != null)
                {
                    await _imageService.DeleteImageAsync(oldArchImage.ImageUrl);
                    _context.Location_Images.Remove(oldArchImage);
                    await _context.SaveChangesAsync();
                }

                string fileName = $"{location.Id}-arch{Path.GetExtension(architectureImage.FileName)}";
                var imageUrl = await _imageService.SaveImageAsync(architectureImage, locationFolder, fileName);
                _context.Location_Images.Add(new LocationImage
                {
                    LocationId = location.Id,
                    ImageUrl = imageUrl,
                    ImageType = LocationImageType.architecture,
                    ReferenceId = null,
                    CreatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }

            // Xử lý ảnh experience
            if (experienceImages != null && experienceImages.Any(img => img != null && img.Length > 0))
            {
                var experiences = await _context.Experiences
                    .Where(e => e.LocationId == location.Id)
                    .OrderBy(e => e.Id)
                    .ToListAsync();

                var validExperienceImages = experienceImages
                    .Select((img, index) => new { Image = img, Index = index })
                    .Where(x => x.Image != null && x.Image.Length > 0)
                    .ToList();

                for (int i = 0; i < Math.Min(validExperienceImages.Count, experiences.Count); i++)
                {
                    var validImage = validExperienceImages[i];
                    var experienceId = experiences[i].Id;

                    string fileName = $"{location.Id}-exp-{experienceId}{Path.GetExtension(validImage.Image.FileName)}";
                    var imageUrl = await _imageService.SaveImageAsync(validImage.Image, locationFolder, fileName);

                    _context.Location_Images.Add(new LocationImage
                    {
                        LocationId = location.Id,
                        ImageUrl = imageUrl,
                        ImageType = LocationImageType.experience,
                        ReferenceId = experienceId,
                        CreatedAt = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }
            }

            // Xử lý ảnh cuisine
            if (cuisineImages != null && cuisineImages.Any(img => img != null && img.Length > 0))
            {
                var cuisines = await _context.Cuisines
                    .Where(c => c.LocationId == location.Id)
                    .OrderBy(c => c.Id)
                    .ToListAsync();

                var validCuisineImages = cuisineImages
                    .Select((img, index) => new { Image = img, Index = index })
                    .Where(x => x.Image != null && x.Image.Length > 0)
                    .ToList();

                for (int i = 0; i < Math.Min(validCuisineImages.Count, cuisines.Count); i++)
                {
                    var validImage = validCuisineImages[i];
                    var cuisineId = cuisines[i].Id;

                    string fileName = $"{location.Id}-cui-{cuisineId}{Path.GetExtension(validImage.Image.FileName)}";
                    var imageUrl = await _imageService.SaveImageAsync(validImage.Image, locationFolder, fileName);

                    _context.Location_Images.Add(new LocationImage
                    {
                        LocationId = location.Id,
                        ImageUrl = imageUrl,
                        ImageType = LocationImageType.cuisine,
                        ReferenceId = cuisineId,
                        CreatedAt = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }
            }

            // Xử lý các hình ảnh khác
            if (images != null && images.Any())
            {
                foreach (var image in images.Where(img => img.Length > 0))
                {
                    string fileName = $"{location.Id}-img-{Guid.NewGuid().ToString("N").Substring(0, 8)}{Path.GetExtension(image.FileName)}";
                    var imageUrl = await _imageService.SaveImageAsync(image, locationFolder, fileName);
                    _context.Location_Images.Add(new LocationImage
                    {
                        LocationId = location.Id,
                        ImageUrl = imageUrl,
                        ImageType = LocationImageType.@default,
                        CreatedAt = DateTime.Now
                    });
                }
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("=== EDIT IMAGE PROCESSING COMPLETED ===");
        }

        // DELETE methods (unchanged)
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteLocation(int? id)
        {
            if (id == null)
                return NotFound();

            var location = await _context.Locations
                .Include(l => l.LocationDetail)
                .Include(l => l.LocationImages.Take(1))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (location == null)
                return NotFound();

            return View(location);
        }

        [Authorize(Roles = "admin")]
        [HttpPost, ActionName("DeleteLocation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmedLoaction(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var location = await _context.Locations
                    .Include(l => l.LocationDetail)
                    .Include(l => l.TravelInfo)
                    .Include(l => l.BestTimes)
                    .Include(l => l.TravelMethods)
                    .Include(l => l.Experiences)
                    .Include(l => l.Cuisines)
                    .Include(l => l.Tips)
                    .Include(l => l.LocationImages)
                    .Include(l => l.NearbyLocations)
                    .Include(l => l.LocationHotels)
                    .FirstOrDefaultAsync(l => l.Id == id);

                if (location == null)
                    return NotFound();

                var tourLocations = await _context.Tour_Locations
                    .Where(tl => tl.LocationId == id)
                    .ToListAsync();

                if (tourLocations.Any())
                {
                    throw new InvalidOperationException($"Không thể xóa địa điểm này vì nó đang được sử dụng trong {tourLocations.Count} tour.");
                }

                if (location.LocationImages != null && location.LocationImages.Any())
                {
                    foreach (var image in location.LocationImages)
                    {
                        await _imageService.DeleteImageAsync(image.ImageUrl);
                    }
                }

                if (location.NearbyLocations != null && location.NearbyLocations.Any())
                {
                    _context.NearbyLocations.RemoveRange(location.NearbyLocations);
                }

                if (location.LocationHotels != null && location.LocationHotels.Any())
                {
                    _context.LocationHotels.RemoveRange(location.LocationHotels);
                }

                var reverseNearbyLocations = await _context.NearbyLocations
                    .Where(nl => nl.NearbyLocationId == id)
                    .ToListAsync();

                if (reverseNearbyLocations.Any())
                {
                    _context.NearbyLocations.RemoveRange(reverseNearbyLocations);
                }

                _context.Locations.Remove(location);
                await _context.SaveChangesAsync();

                try
                {
                    string locationFolder = Path.Combine("wwwroot", "uploads", $"locations/{id}");
                    if (Directory.Exists(locationFolder))
                    {
                        Directory.Delete(locationFolder, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Không thể xóa thư mục chứa ảnh: {ex.Message}");
                }

                await transaction.CommitAsync();
                TempData["Success"] = "Xóa địa điểm thành công!";
                return RedirectToAction(nameof(MgtListLocation));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error deleting location {id}: {ex.Message}");

                if (ex is InvalidOperationException)
                {
                    TempData["Error"] = ex.Message;
                }
                else
                {
                    TempData["Error"] = $"Có lỗi xảy ra khi xóa địa điểm: {ex.Message}";
                }

                return RedirectToAction(nameof(MgtListLocation));
            }
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteImage(int id)
        {
            var image = await _context.Location_Images.FindAsync(id);

            if (image == null)
                return NotFound();

            try
            {
                await _imageService.DeleteImageAsync(image.ImageUrl);
                _context.Location_Images.Remove(image);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Xóa hình ảnh thành công!";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting image {id}: {ex.Message}");
                TempData["Error"] = $"Có lỗi xảy ra khi xóa hình ảnh: {ex.Message}";
            }

            return RedirectToAction(nameof(EditLocation), new { id = image.LocationId });
        }

        // Other methods (unchanged)
        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> UserManagement()
        {
            var users = await _userService.GetAllUsersAsync();
            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _userService.GetUserByIdForClaimsAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(int id, [FromForm] RoleType role)
        {
            var result = await _userService.UpdateUserRoleAsync(id, role);

            if (result.Success)
            {
                TempData["SuccessMessage"] = $"Cập nhật vai trò cho người dùng ID {id} thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(currentUserId) && int.Parse(currentUserId) == id)
            {
                TempData["ErrorMessage"] = "Bạn không thể xóa chính mình.";
                return RedirectToAction("UserManagement");
            }

            var result = await _userService.DeleteUserAsync(id);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message ?? "Có lỗi xảy ra, có thể do người dùng này còn dữ liệu liên quan.";
            }

            return RedirectToAction("UserManagement");
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> ManagePlans(string status)
        {
            ViewData["CurrentStatus"] = status;

            var plansQuery = _context.Tours
                .Include(t => t.User)
                .OrderByDescending(t => t.UpdatedAt)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                if (Enum.TryParse<TourStatus>(status, true, out var statusEnum))
                {
                    plansQuery = plansQuery.Where(t => t.Status == statusEnum);
                }
            }

            var plans = await plansQuery.ToListAsync();

            ViewBag.AllCount = await _context.Tours.CountAsync();
            ViewBag.PendingCount = await _context.Tours.CountAsync(t => t.Status == TourStatus.pending);
            ViewBag.ApprovedCount = await _context.Tours.CountAsync(t => t.Status == TourStatus.approved);
            ViewBag.RejectedCount = await _context.Tours.CountAsync(t => t.Status == TourStatus.rejected);

            return View(plans);
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePlanStatus(int planId, string newStatus)
        {
            var plan = await _context.Tours.FindAsync(planId);
            if (plan == null)
            {
                return NotFound();
            }

            if (Enum.TryParse<TourStatus>(newStatus, true, out var statusEnum))
            {
                plan.Status = statusEnum;
                plan.UpdatedAt = DateTime.Now;
                _context.Update(plan);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Trạng thái của tour đã được cập nhật thành '{newStatus}'.";
                return RedirectToAction(nameof(ManagePlans), new { status = plan.Status.ToString().ToLower() });
            }

            TempData["ErrorMessage"] = "Trạng thái không hợp lệ.";
            return RedirectToAction(nameof(ManagePlans));
        }

        // GET: CreatePlan
        [HttpGet]
        public async Task<IActionResult> CreatePlan()
        {
            ViewBag.Locations = await _context.Locations.ToListAsync();
            return View();
        }




        // ✅ FIXED VERSION - PlansController Create Method
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePlan(CreateTourRequest request)
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
                return RedirectToAction(nameof(ManagePlans));
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

        [HttpGet]


        public async Task<IActionResult> EditPlan(int id)
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

                if (tour.UserId != GetCurrentUserId() && !User.IsInRole("admin"))
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
        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> EditPlan(int id, EditTourRequest request)
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
                return RedirectToAction(nameof(ManagePlans));
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

        [HttpGet]
        public async Task<IActionResult> DeletePlan(int id)
        {
            var tour = await _context.Tours.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (tour == null)
            {
                return NotFound();
            }
            return View(tour);
        }

        [HttpPost, ActionName("DeletePlan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePlanConfirmed(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var tour = await _context.Tours.FindAsync(id);
                if (tour == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy kế hoạch.";
                    return RedirectToAction(nameof(ManagePlans));
                }

                await RemoveRelatedTourData(id);
                await DeleteOldImage(tour.Image);

                _context.Tours.Remove(tour);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = "Xóa kế hoạch thành công!";
                return RedirectToAction(nameof(ManagePlans));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting plan in AdminController");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xóa kế hoạch.";
                return RedirectToAction(nameof(ManagePlans));
            }
        }

        #region Helper Methods from PlansController

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            return 0;
        }

        private async Task<string> SaveImageFile(IFormFile imageFile)
        {
            string uploadsDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tours");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
            string filePath = Path.Combine(uploadsDir, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return "/uploads/tours/" + uniqueFileName;
        }

        private async Task DeleteOldImage(string imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath) && !imagePath.Contains("default-tour.jpg"))
            {
                var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, imagePath.TrimStart('/'));
                if (System.IO.File.Exists(oldImagePath))
                {
                    await Task.Run(() => System.IO.File.Delete(oldImagePath));
                }
            }
        }

        private async Task<Tour> GetTourForEdit(int tourId)
        {
            return await _context.Tours
                .Include(t => t.TourHighlights)
                .Include(t => t.TourSchedules)
                    .ThenInclude(s => s.ScheduleActivities)
                .Include(t => t.TourSchedules)
                    .ThenInclude(s => s.TourLocations)
                .Include(t => t.TourIncludes)
                .Include(t => t.TourExcludes)
                .Include(t => t.TourNotes)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tourId);
        }

        private async Task RemoveRelatedTourData(int tourId)
        {
            var highlights = await _context.Tour_Highlights.Where(h => h.TourId == tourId).ToListAsync();
            if (highlights.Any()) _context.Tour_Highlights.RemoveRange(highlights);

            var schedules = await _context.Tour_Schedules.Where(s => s.TourId == tourId).Include(s => s.ScheduleActivities).ToListAsync();
            if (schedules.Any())
            {
                foreach (var schedule in schedules)
                {
                    if (schedule.ScheduleActivities.Any()) _context.Schedule_Activities.RemoveRange(schedule.ScheduleActivities);
                }
                var tourLocations = await _context.Tour_Locations.Where(tl => tl.TourId == tourId).ToListAsync();
                if (tourLocations.Any()) _context.Tour_Locations.RemoveRange(tourLocations);

                _context.Tour_Schedules.RemoveRange(schedules);
            }

            var includes = await _context.Tour_Includes.Where(i => i.TourId == tourId).ToListAsync();
            if (includes.Any()) _context.Tour_Includes.RemoveRange(includes);

            var excludes = await _context.Tour_Excludes.Where(e => e.TourId == tourId).ToListAsync();
            if (excludes.Any()) _context.Tour_Excludes.RemoveRange(excludes);

            var notes = await _context.Tour_Notes.Where(n => n.TourId == tourId).ToListAsync();
            if (notes.Any()) _context.Tour_Notes.RemoveRange(notes);

            await _context.SaveChangesAsync();
        }

        private async Task AddTourRelatedData(int tourId, CreateTourRequest request)
        {
            if (request.Highlights != null)
            {
                _context.Tour_Highlights.AddRange(request.Highlights.Where(h => !string.IsNullOrWhiteSpace(h)).Select(h => new TourHighlight { TourId = tourId, Highlight = h }));
            }
            if (request.Includes != null)
            {
                _context.Tour_Includes.AddRange(request.Includes.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => new TourInclude { TourId = tourId, Description = i }));
            }
            if (request.Excludes != null)
            {
                _context.Tour_Excludes.AddRange(request.Excludes.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => new TourExclude { TourId = tourId, Description = e }));
            }
            if (request.Notes != null)
            {
                _context.Tour_Notes.AddRange(request.Notes.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => new TourNote { TourId = tourId, Description = n }));
            }

            if (request.Schedules != null)
            {
                foreach (var scheduleRequest in request.Schedules)
                {
                    var tourSchedule = new TourSchedule
                    {
                        TourId = tourId,
                        Day = scheduleRequest.Day,
                        Title = scheduleRequest.Title
                    };
                    _context.Tour_Schedules.Add(tourSchedule);
                    await _context.SaveChangesAsync(); // Save to get Id

                    if (scheduleRequest.Activities != null)
                    {
                        _context.Schedule_Activities.AddRange(scheduleRequest.Activities.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => new ScheduleActivity { ScheduleId = tourSchedule.Id, Activity = a }));
                    }
                    if (scheduleRequest.LocationIds != null)
                    {
                        _context.Tour_Locations.AddRange(scheduleRequest.LocationIds.Select(locId => new TourLocation { TourId = tourId, LocationId = locId, ScheduleId = tourSchedule.Id }));
                    }
                }
            }
        }
        #endregion
    }
}