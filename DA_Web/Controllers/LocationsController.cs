

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using DA_Web.Data;
using DA_Web.Models;
using DA_Web.Models.Enums;
using DA_Web.Repository;
using DA_Web.ViewModels.Locations;

namespace DA_Web.Controllers
{
    public class LocationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IImageService _imageService;
        private readonly ILogger<LocationsController> _logger;
        private readonly string _weatherApiKey = "095cde61e730fd9406235de1237e97c1";

        public LocationsController(
            ApplicationDbContext context,
            IImageService imageService,
            ILogger<LocationsController> logger)
        {
            _context = context;
            _imageService = imageService;
            _logger = logger;
        }

        // GET: Locations
        public async Task<IActionResult> Index()
        {
            var locations = await _context.Locations
                .Include(l => l.LocationDetail)
                .Include(l => l.TravelInfo)
                .Include(l => l.LocationImages.Where(img => img.ImageType == LocationImageType.introduction))
                .OrderBy(l => l.Name)
                .ToListAsync();

            return View(locations);
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

        // GET: Locations/Category/{category}
        public async Task<IActionResult> Category(string category)
        {
            if (string.IsNullOrEmpty(category))
                return RedirectToAction(nameof(Index));

            var locations = await _context.Locations
                .Include(l => l.LocationDetail)
                .Include(l => l.LocationImages.Where(img => img.ImageType == LocationImageType.introduction))
                .Where(l => l.Type.ToLower() == category.ToLower())
                .OrderBy(l => l.Name)
                .ToListAsync();

            ViewBag.Category = char.ToUpper(category[0]) + category.Substring(1);
            return View("Index", locations);
        }

        // GET: Locations/Details/5
        public async Task<IActionResult> Details(int? id)
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


    }
}
