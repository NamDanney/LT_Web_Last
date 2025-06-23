using DA_Web.Configurations;
using DA_Web.Data;
using DA_Web.Helpers;
using DA_Web.Hubs;
using DA_Web.Repository;
using DA_Web.Services;
using DA_Web.Services.Implementations;
using DA_Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// =================================================================
// 1. CẤU HÌNH & DỊCH VỤ (SERVICE REGISTRATION)
// =================================================================

// --- Đọc cấu hình từ appsettings.json ---
builder.Services.Configure<JwtConfig>(builder.Configuration.GetSection("JwtConfig"));
builder.Services.Configure<EmailConfig>(builder.Configuration.GetSection("EmailConfig"));
builder.Services.Configure<FileUploadConfig>(builder.Configuration.GetSection("FileUploadConfig"));

// --- Database ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// --- Authentication (Quan trọng) ---
// Cấu hình song song 2 cơ chế: Cookie cho web và JWT cho API
builder.Services.AddAuthentication(options =>
{
    // Đặt Cookie làm cơ chế mặc định cho các trang web MVC
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
})
.AddJwtBearer(options => // Cấu hình cho API
{
    var jwtConfig = builder.Configuration.GetSection("JwtConfig").Get<JwtConfig>();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtConfig.Issuer,
        ValidAudience = jwtConfig.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig.SecretKey))
    };
});

// --- Đăng ký các dịch vụ tùy chỉnh (Custom Services) ---
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<ITourService, TourService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// *** SỬA LỖI: THÊM DÒNG NÀY - ĐÂY LÀ NGUYÊN NHÂN CHÍNH ***
builder.Services.AddScoped<IReviewService, ReviewService>();

// --- Đăng ký các lớp Helper ---
builder.Services.AddScoped<JwtHelper>();

// --- Cấu hình HTTP Client ---
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<GeminiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "DA_Web/1.0");
});

// --- Đăng ký GeminiService ---
builder.Services.AddScoped<GeminiService>();

// --- Các dịch vụ khác ---
builder.Services.AddAutoMapper(typeof(Program).Assembly);
builder.Services.AddHttpContextAccessor(); // Cần thiết cho nhiều tác vụ web

// --- Cấu hình CORS (gộp lại thành một chính sách duy nhất) ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // URL của React App
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// --- Cấu hình SignalR ---
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// --- Cấu hình cho Controller và API ---
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true; // Format JSON cho dễ đọc
    });

// --- Cấu hình Logging ---
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
    loggingBuilder.SetMinimumLevel(LogLevel.Information); // Đổi từ Debug sang Information để giảm noise
});

// --- Swagger cho API Documentation (chỉ trong môi trường Development) ---
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Phú Yên Travel API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Authorization header. Enter 'Bearer' [space] and then your token.",
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                new string[] {}
            }
        });
    });
}

// =================================================================
// 2. CẤU HÌNH HTTP REQUEST PIPELINE (MIDDLEWARE)
// =================================================================

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Phú Yên Travel API V1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Cho phép truy cập các file trong wwwroot

app.UseRouting();

// *** SỬA LỖI: Sử dụng một chính sách CORS duy nhất ***
app.UseCors("DefaultPolicy");

// Quan trọng: Bật Authentication và Authorization. Thứ tự này là bắt buộc.
app.UseAuthentication();
app.UseAuthorization();

// Map các endpoint
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map API Controllers với CORS
app.MapControllers();

// Đăng ký SignalR Hub
app.MapHub<ChatHub>("/chathub");

app.Run();