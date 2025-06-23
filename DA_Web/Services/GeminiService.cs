using DA_Web.Data;
using DA_Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace DA_Web.Services
{
    public class GeminiService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GeminiService> _logger;
        private readonly HttpClient _httpClient;

        public GeminiService(
            IConfiguration configuration,
            ApplicationDbContext context,
            ILogger<GeminiService> logger,
            HttpClient httpClient)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<string> ProcessMessageAsync(int userId, string message)
        {
            try
            {
                _logger.LogInformation($"Xử lý tin nhắn từ người dùng {userId}: {message}");

                // Lấy dữ liệu từ database để đưa vào context cho AI
                var locations = await _context.Locations
                    .Include(l => l.LocationDetail)
                    .OrderBy(l => l.Id)
                    .Take(20)
                    .Select(l => new
                    {
                        l.Id,
                        l.Name,
                        l.Type,
                        l.Description,
                        Subtitle = l.LocationDetail != null ? l.LocationDetail.Subtitle : "",
                    })
                    .ToListAsync();

                var tours = await _context.Tours
                    .OrderBy(t => t.Id)
                    .Take(10)
                    .Select(t => new
                    {
                        t.Id,
                        t.Description,
                        t.Duration,
                    })
                    .ToListAsync();

                _logger.LogInformation($"Lấy được {locations.Count} địa điểm và {tours.Count} tour du lịch");

                // Tạo system prompt
                string systemPrompt = CreateSystemPrompt(locations, tours);

                // Gọi API Gemini
                _logger.LogInformation("Gọi API Gemini");

                // In ra API Key để debug (chỉ ở môi trường phát triển)
                string apiKey = _configuration["Gemini:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("API key Gemini không được cấu hình");
                    return "Xin lỗi, dịch vụ chưa được cấu hình đúng cách. Vui lòng liên hệ quản trị viên.";
                }

                _logger.LogInformation($"API Key: {apiKey?.Substring(0, 3)}...{(apiKey?.Length > 5 ? apiKey.Substring(apiKey.Length - 3) : "")}");

                // Thử các API theo thứ tự ưu tiên
                string response = null;

                // Thử với API đơn giản trước
                response = await CallSimpleGeminiApi(systemPrompt, message);

                if (string.IsNullOrEmpty(response) || response.Contains("Xin lỗi, API trả về lỗi"))
                {
                    _logger.LogWarning("Thử lại với API fallback");
                    response = await CallGeminiProAsync(systemPrompt, message);
                }

                return response ?? "Xin lỗi, tôi đang gặp sự cố kỹ thuật. Vui lòng thử lại sau.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi xử lý tin nhắn: {ex.Message}");
                _logger.LogError(ex.StackTrace);
                return "Xin lỗi, tôi đang gặp sự cố kỹ thuật. Vui lòng thử lại sau.";
            }
        }

        private string CreateSystemPrompt(dynamic locations, dynamic tours)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Bạn là trợ lý du lịch Phú Yên, giúp du khách tìm hiểu về các địa điểm du lịch và tour ở Phú Yên.");
            sb.AppendLine("Dưới đây là thông tin về các địa điểm du lịch ở Phú Yên:");

            foreach (var loc in locations)
            {
                sb.AppendLine($"- ID: {loc.Id}, Tên: {loc.Name}, Loại: {loc.Type}, Mô tả: {loc.Description}, Chi tiết: {loc.Subtitle}");
            }

            sb.AppendLine("\nDưới đây là thông tin về các tour du lịch ở Phú Yên:");

            foreach (var tour in tours)
            {
                // Sửa để tránh lỗi null reference
                sb.AppendLine($"- ID: {tour.Id}, Mô tả: {tour.Description}, Thời gian: {tour.Duration}");
            }

            sb.AppendLine("\nTHÔNG TIN THỜI TIẾT PHÚ YÊN:");
            sb.AppendLine("- Mùa khô (tháng 1-8): Thời tiết đẹp, ít mưa, thích hợp du lịch");
            sb.AppendLine("- Mùa mưa (tháng 9-12): Mưa nhiều, có thể có bão");
            sb.AppendLine("- Nhiệt độ trung bình: 25-32°C");
            sb.AppendLine("- Thời điểm lý tưởng: Tháng 2-8");

            sb.AppendLine("\nQUY TẮC QUAN TRỌNG:");
            sb.AppendLine("1. Khi đề cập đến một địa điểm cụ thể, LUÔN thêm ID của địa điểm đó vào cuối tên địa điểm với định dạng (ID: X), ví dụ: Ghềnh Đá Đĩa (ID: 1)");
            sb.AppendLine("2. Khi đề cập đến một tour du lịch cụ thể, LUÔN thêm ID của tour đó vào cuối tên tour với định dạng (Tour ID: X), ví dụ: Tour Khám phá Phú Yên 3 ngày 2 đêm (Tour ID: 1)");
            sb.AppendLine("3. Trả lời ngắn gọn, súc tích nhưng đầy đủ thông tin.");
            sb.AppendLine("4. Nếu không biết câu trả lời cho một câu hỏi nào đó, hãy thừa nhận và gợi ý người dùng hỏi về các địa điểm hoặc tour du lịch có sẵn.");

            return sb.ToString();
        }

        private async Task<string> CallSimpleGeminiApi(string systemPrompt, string userMessage)
        {
            string apiKey = _configuration["Gemini:ApiKey"];

            // Sử dụng model và version đã test thành công
            string model = "gemini-1.5-flash-latest"; // hoặc model khác từ test
            string version = "v1beta"; // hoặc version khác
            string endpoint = $"https://generativelanguage.googleapis.com/{version}/models/{model}:generateContent?key={apiKey}";

            _logger.LogInformation($"Using model: {model}, version: {version}");

            try
            {
                var payload = new
                {
                    contents = new[]
                    {
                new
                {
                    parts = new[]
                    {
                        new { text = $"{systemPrompt}\n\nCâu hỏi: {userMessage}\n\nTrả lời:" }
                    }
                }
            },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        topK = 1,
                        topP = 1,
                        maxOutputTokens = 2048,
                        stopSequences = new string[] { }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                _logger.LogDebug($"Request: {jsonContent}");

                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(endpoint, content);
                var responseString = await response.Content.ReadAsStringAsync();

                _logger.LogDebug($"Response Status: {response.StatusCode}");
                _logger.LogDebug($"Response: {responseString}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"API Error: {response.StatusCode} - {responseString}");

                    // Parse error message
                    try
                    {
                        using var errorDoc = JsonDocument.Parse(responseString);
                        if (errorDoc.RootElement.TryGetProperty("error", out var error) &&
                            error.TryGetProperty("message", out var errorMessage))
                        {
                            return $"Lỗi API: {errorMessage.GetString()}";
                        }
                    }
                    catch { }

                    return GetFallbackResponse(userMessage);
                }

                // Parse response
                using var document = JsonDocument.Parse(responseString);
                var root = document.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var content1) &&
                    content1.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString() ?? GetFallbackResponse(userMessage);
                }

                _logger.LogError("Invalid response structure");
                return GetFallbackResponse(userMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in CallSimpleGeminiApi: {ex}");
                return GetFallbackResponse(userMessage);
            }
        }

        private async Task<string> CallGeminiProAsync(string systemPrompt, string userMessage)
        {
            try
            {
                string apiKey = _configuration["Gemini:ApiKey"];
                string endpoint = $"https://generativelanguage.googleapis.com/v1/models/gemini-pro:generateContent?key={apiKey}";

                var combinedPrompt = $"{systemPrompt}\n\nNgười dùng hỏi: {userMessage}\n\nTravel Assistant:";

                // Sử dụng cấu trúc đơn giản
                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = combinedPrompt }
                            }
                        }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation("Trying with gemini-pro model (fallback)");

                var response = await _httpClient.PostAsync(endpoint, content);
                var responseString = await response.Content.ReadAsStringAsync();

                _logger.LogDebug($"Gemini Pro response: {responseString}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Gemini Pro API error: {response.StatusCode}, Body: {responseString}");
                    return GetFallbackResponse(userMessage);
                }

                using var document = JsonDocument.Parse(responseString);
                var root = document.RootElement;

                if (!root.TryGetProperty("candidates", out var candidates) ||
                    candidates.GetArrayLength() == 0 ||
                    !candidates[0].TryGetProperty("content", out var content1) ||
                    !content1.TryGetProperty("parts", out var parts) ||
                    parts.GetArrayLength() == 0 ||
                    !parts[0].TryGetProperty("text", out var textElement))
                {
                    _logger.LogError($"Invalid response format: {responseString}");
                    return GetFallbackResponse(userMessage);
                }

                var text = textElement.GetString();
                return text ?? GetFallbackResponse(userMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calling gemini-pro API: {ex.Message}");
                return GetFallbackResponse(userMessage);
            }
        }

        // Fallback response khi API thất bại
        private string GetFallbackResponse(string message)
        {
            message = message.ToLower().Trim();

            if (message.Contains("xin chào") || message.Contains("chào") || message.Contains("hello") || message.Contains("hi"))
            {
                return "Xin chào! Tôi là trợ lý du lịch Phú Yên. Tôi có thể giúp bạn tìm hiểu về các địa điểm du lịch và tour tại Phú Yên. Bạn muốn biết về điều gì?";
            }

            if (message.Contains("địa điểm") || message.Contains("tham quan") || message.Contains("du lịch"))
            {
                return "Phú Yên có rất nhiều địa điểm du lịch nổi tiếng như: Ghềnh Đá Đĩa (ID: 1), Bãi Xép (ID: 2), Mũi Điện (ID: 3) - nơi đón ánh bình minh đầu tiên trên đất liền của Việt Nam. Bạn muốn tìm hiểu địa điểm nào cụ thể?";
            }

            if (message.Contains("thời tiết") || message.Contains("mùa") || message.Contains("khi nào"))
            {
                return "Phú Yên có khí hậu nhiệt đới gió mùa. Mùa khô từ tháng 1-8 là thời điểm lý tưởng để du lịch với nhiệt độ từ 25-32°C. Mùa mưa từ tháng 9-12 thường có mưa nhiều và đôi khi có bão. Tôi khuyên bạn nên đi Phú Yên vào khoảng tháng 3-7 để có thời tiết đẹp nhất.";
            }

            if (message.Contains("tour") || message.Contains("chi phí") || message.Contains("giá"))
            {
                return "Phú Yên có nhiều tour du lịch đa dạng với nhiều mức giá khác nhau. Tour phổ biến nhất là tour 3 ngày 2 đêm khám phá các điểm đến nổi tiếng như Ghềnh Đá Đĩa (ID: 1), Bãi Xép (ID: 2) và Mũi Điện (ID: 3). Chi phí tour thường từ 1.5 triệu đến 3 triệu đồng tùy vào dịch vụ và thời điểm.";
            }

            return "Xin chào! Tôi là trợ lý du lịch Phú Yên. Tôi có thể giúp bạn tìm hiểu về các địa điểm du lịch, thông tin về thời tiết, các tour du lịch và nhiều điều thú vị khác về Phú Yên. Bạn muốn biết điều gì?";
        }
    }
}

//using DA_Web.Data;
//using DA_Web.Models;
//using Microsoft.EntityFrameworkCore;
//using System.Text;
//using System.Text.Json;
//using System.Text.RegularExpressions;

//namespace DA_Web.Services
//{
//    public class GeminiService
//    {
//        private readonly IConfiguration _configuration;
//        private readonly ApplicationDbContext _context;
//        private readonly ILogger<GeminiService> _logger;
//        private readonly HttpClient _httpClient;

//        public GeminiService(
//            IConfiguration configuration,
//            ApplicationDbContext context,
//            ILogger<GeminiService> logger,
//            HttpClient httpClient)
//        {
//            _configuration = configuration;
//            _context = context;
//            _logger = logger;
//            _httpClient = httpClient;
//        }

//        public async Task<string> ProcessMessageAsync(int userId, string message, string language = "vi")
//        {
//            try
//            {
//                _logger.LogInformation($"Processing message from user {userId} in {language}: {message}");

//                // Lấy dữ liệu từ database
//                var locations = await _context.Locations
//                    .Include(l => l.LocationDetail)
//                    .OrderBy(l => l.Id)
//                    .Take(20)
//                    .Select(l => new {
//                        l.Id,
//                        l.Name,
//                        l.Type,
//                        l.Description,
//                        Subtitle = l.LocationDetail != null ? l.LocationDetail.Subtitle : "",
//                    })
//                    .ToListAsync();

//                var tours = await _context.Tours
//                    .OrderBy(t => t.Id)
//                    .Take(10)
//                    .Select(t => new {
//                        t.Id,

//                        t.Description,
//                        t.Duration,
//                    })
//                    .ToListAsync();

//                _logger.LogInformation($"Found {locations.Count} locations and {tours.Count} tours");

//                // Tạo system prompt theo ngôn ngữ
//                string systemPrompt = CreateSystemPromptWithLanguage(locations, tours, language);

//                // Gọi API
//                string response = await CallGeminiAPI(systemPrompt, message, language);

//                if (string.IsNullOrEmpty(response))
//                {
//                    return GetFallbackResponse(message, language);
//                }

//                // Post-process để thêm ID vào response
//                response = await ProcessResponseWithIds(response);

//                return response;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError($"Error processing message: {ex.Message}");
//                return GetFallbackResponse(message, language);
//            }
//        }

//        private string CreateSystemPromptWithLanguage(dynamic locations, dynamic tours, string language)
//        {
//            var sb = new StringBuilder();

//            switch (language)
//            {
//                case "en":
//                    sb.AppendLine("You are a Phu Yen travel assistant helping tourists learn about tourist attractions and tours in Phu Yen, Vietnam.");
//                    sb.AppendLine("\nTOURIST ATTRACTIONS IN PHU YEN:");
//                    break;
//                case "ko":
//                    sb.AppendLine("당신은 베트남 푸옌의 관광 명소와 투어에 대해 관광객들을 돕는 푸옌 여행 도우미입니다.");
//                    sb.AppendLine("\n푸옌의 관광 명소:");
//                    break;
//                case "zh":
//                    sb.AppendLine("您是富安旅游助手，帮助游客了解越南富安的旅游景点和旅游线路。");
//                    sb.AppendLine("\n富安旅游景点：");
//                    break;
//                default: // Vietnamese
//                    sb.AppendLine("Bạn là trợ lý du lịch Phú Yên, giúp du khách tìm hiểu về các địa điểm du lịch và tour ở Phú Yên.");
//                    sb.AppendLine("\nĐỊA ĐIỂM DU LỊCH PHÚ YÊN:");
//                    break;
//            }

//            // Add locations
//            foreach (var loc in locations)
//            {
//                sb.AppendLine($"- {loc.Name}: {loc.Type}. {loc.Description}");
//                if (!string.IsNullOrEmpty(loc.Subtitle))
//                {
//                    sb.AppendLine($"  {GetDetailLabel(language)}: {loc.Subtitle}");
//                }
//            }

//            // Add tours
//            sb.AppendLine($"\n{GetTourSectionTitle(language)}:");
//            foreach (var tour in tours)
//            {
//                sb.AppendLine($"- {tour.Name ?? tour.Description}: {GetDurationLabel(language)} {tour.Duration}");
//            }

//            // Add weather info
//            sb.AppendLine($"\n{GetWeatherSectionTitle(language)}:");
//            AddWeatherInfo(sb, language);

//            // Add response guidelines
//            sb.AppendLine($"\n{GetGuidelinesTitle(language)}:");
//            AddResponseGuidelines(sb, language);

//            return sb.ToString();
//        }

//        private void AddWeatherInfo(StringBuilder sb, string language)
//        {
//            switch (language)
//            {
//                case "en":
//                    sb.AppendLine("- Dry season (Jan-Aug): Beautiful weather, little rain, suitable for tourism");
//                    sb.AppendLine("- Rainy season (Sep-Dec): Heavy rain, possible storms");
//                    sb.AppendLine("- Average temperature: 25-32°C (77-90°F)");
//                    sb.AppendLine("- Best time to visit: February to August");
//                    break;
//                case "ko":
//                    sb.AppendLine("- 건기 (1-8월): 아름다운 날씨, 비가 적어 관광에 적합");
//                    sb.AppendLine("- 우기 (9-12월): 많은 비, 태풍 가능성");
//                    sb.AppendLine("- 평균 기온: 25-32°C");
//                    sb.AppendLine("- 방문 최적기: 2월부터 8월까지");
//                    break;
//                case "zh":
//                    sb.AppendLine("- 旱季（1-8月）：天气晴朗，少雨，适合旅游");
//                    sb.AppendLine("- 雨季（9-12月）：多雨，可能有台风");
//                    sb.AppendLine("- 平均温度：25-32°C");
//                    sb.AppendLine("- 最佳游览时间：2月至8月");
//                    break;
//                default:
//                    sb.AppendLine("- Mùa khô (tháng 1-8): Thời tiết đẹp, ít mưa, thích hợp du lịch");
//                    sb.AppendLine("- Mùa mưa (tháng 9-12): Mưa nhiều, có thể có bão");
//                    sb.AppendLine("- Nhiệt độ trung bình: 25-32°C");
//                    sb.AppendLine("- Thời điểm lý tưởng: Tháng 2-8");
//                    break;
//            }
//        }

//        private void AddResponseGuidelines(StringBuilder sb, string language)
//        {
//            switch (language)
//            {
//                case "en":
//                    sb.AppendLine("1. NEVER mention IDs in responses");
//                    sb.AppendLine("2. Only mention location names and their features");
//                    sb.AppendLine("3. Respond naturally and friendly like a professional tour guide");
//                    sb.AppendLine("4. Keep responses concise but informative");
//                    sb.AppendLine("5. Always respond in English");
//                    break;
//                case "ko":
//                    sb.AppendLine("1. 답변에 ID를 언급하지 마세요");
//                    sb.AppendLine("2. 장소 이름과 특징만 언급하세요");
//                    sb.AppendLine("3. 전문 가이드처럼 자연스럽고 친근하게 대답하세요");
//                    sb.AppendLine("4. 간결하면서도 유익한 답변을 제공하세요");
//                    sb.AppendLine("5. 항상 한국어로 답변하세요");
//                    break;
//                case "zh":
//                    sb.AppendLine("1. 回复中不要提及ID");
//                    sb.AppendLine("2. 只提及地点名称和特点");
//                    sb.AppendLine("3. 像专业导游一样自然友好地回答");
//                    sb.AppendLine("4. 保持回复简洁但信息丰富");
//                    sb.AppendLine("5. 始终用中文回答");
//                    break;
//                default:
//                    sb.AppendLine("1. KHÔNG BAO GIỜ đề cập đến ID trong câu trả lời");
//                    sb.AppendLine("2. Chỉ nói về tên địa điểm và đặc điểm của nó");
//                    sb.AppendLine("3. Trả lời tự nhiên, thân thiện như một hướng dẫn viên du lịch");
//                    sb.AppendLine("4. Giữ câu trả lời ngắn gọn nhưng đầy đủ thông tin");
//                    sb.AppendLine("5. Luôn trả lời bằng tiếng Việt");
//                    break;
//            }
//        }

//        private string GetDetailLabel(string language) => language switch
//        {
//            "en" => "Details",
//            "ko" => "세부사항",
//            "zh" => "详情",
//            _ => "Chi tiết"
//        };

//        private string GetTourSectionTitle(string language) => language switch
//        {
//            "en" => "TOURS",
//            "ko" => "투어",
//            "zh" => "旅游线路",
//            _ => "TOUR DU LỊCH"
//        };

//        private string GetDurationLabel(string language) => language switch
//        {
//            "en" => "Duration:",
//            "ko" => "기간:",
//            "zh" => "时长：",
//            _ => "Thời gian:"
//        };

//        private string GetWeatherSectionTitle(string language) => language switch
//        {
//            "en" => "PHU YEN WEATHER INFORMATION",
//            "ko" => "푸옌 날씨 정보",
//            "zh" => "富安天气信息",
//            _ => "THÔNG TIN THỜI TIẾT PHÚ YÊN"
//        };

//        private string GetGuidelinesTitle(string language) => language switch
//        {
//            "en" => "RESPONSE GUIDELINES",
//            "ko" => "응답 지침",
//            "zh" => "回复指南",
//            _ => "HƯỚNG DẪN TRẢ LỜI"
//        };

//        private async Task<string> CallGeminiAPI(string systemPrompt, string userMessage, string language)
//        {
//            try
//            {
//                string apiKey = _configuration["Gemini:ApiKey"];

//                // Thử nhiều models
//                string[] models = { "gemini-1.5-flash", "gemini-1.5-pro", "gemini-pro" };

//                foreach (var model in models)
//                {
//                    try
//                    {
//                        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

//                        _logger.LogInformation($"Trying model: {model}");

//                        var payload = new
//                        {
//                            contents = new[]
//                            {
//                        new
//                        {
//                            parts = new[]
//                            {
//                                new { text = $"{systemPrompt}\n\nUser: {userMessage}\n\nAssistant:" }
//                            }
//                        }
//                    }
//                        };

//                        var jsonContent = JsonSerializer.Serialize(payload);
//                        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

//                        var response = await _httpClient.PostAsync(endpoint, content);

//                        if (response.IsSuccessStatusCode)
//                        {
//                            var responseString = await response.Content.ReadAsStringAsync();
//                            _logger.LogInformation($"Success with model: {model}");

//                            // Parse response
//                            using var document = JsonDocument.Parse(responseString);
//                            var text = document.RootElement
//                                .GetProperty("candidates")[0]
//                                .GetProperty("content")
//                                .GetProperty("parts")[0]
//                                .GetProperty("text")
//                                .GetString();

//                            return text ?? GetFallbackResponse(userMessage, language);
//                        }
//                        else
//                        {
//                            _logger.LogWarning($"Model {model} failed: {response.StatusCode}");
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger.LogError($"Error with model {model}: {ex.Message}");
//                    }
//                }

//                // All models failed
//                return GetFallbackResponse(userMessage, language);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError($"CallGeminiAPI error: {ex.Message}");
//                return GetFallbackResponse(userMessage, language);
//            }
//        }

//        private async Task<string> ProcessResponseWithIds(string aiResponse)
//        {
//            try
//            {
//                var locations = await _context.Locations
//                    .Select(l => new { l.Id, l.Name })
//                    .ToListAsync();

//                var tours = await _context.Tours
//                    .Select(t => new { t.Id, t.Destination, Description = t.Description })
//                    .ToListAsync();

//                string processedResponse = aiResponse;

//                foreach (var location in locations)
//                {
//                    if (string.IsNullOrEmpty(location.Name)) continue;

//                    string escapedName = Regex.Escape(location.Name);
//                    string pattern = $@"(?<=^|\s){escapedName}(?=[\s,.:;!?]|$)";

//                    processedResponse = Regex.Replace(
//                        processedResponse,
//                        pattern,
//                        m => $"{m.Value} (ID: {location.Id})",
//                        RegexOptions.IgnoreCase
//                    );
//                }

//                foreach (var tour in tours)
//                {
//                    string tourName = tour.Destination ?? tour.Description;
//                    if (string.IsNullOrEmpty(tourName)) continue;

//                    string escapedName = Regex.Escape(tourName);
//                    string pattern = $@"(?<=^|\s){escapedName}(?=[\s,.:;!?]|$)";

//                    processedResponse = Regex.Replace(
//                        processedResponse,
//                        pattern,
//                        m => $"{m.Value} (Tour ID: {tour.Id})",
//                        RegexOptions.IgnoreCase
//                    );
//                }

//                return processedResponse;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError($"Error processing response with IDs: {ex.Message}");
//                return aiResponse;
//            }
//        }

//        private string GetFallbackResponse(string message, string language)
//        {
//            message = message.ToLower().Trim();

//            switch (language)
//            {
//                case "en":
//                    return GetEnglishFallback(message);
//                case "ko":
//                    return GetKoreanFallback(message);
//                case "zh":
//                    return GetChineseFallback(message);
//                default:
//                    return GetVietnameseFallback(message);
//            }
//        }

//        private string GetEnglishFallback(string message)
//        {
//            if (message.Contains("hello") || message.Contains("hi"))
//            {
//                return "Hello! I'm the Phu Yen travel assistant. I can help you learn about tourist attractions, tours, weather, and many other interesting things about Phu Yen. What would you like to know?";
//            }

//            if (message.Contains("location") || message.Contains("place") || message.Contains("attraction"))
//            {
//                return "Phu Yen has many famous tourist attractions such as: Ganh Da Dia (Stone Plate Rapids), Bai Xep Beach, Mui Dien (Cape Dien), and many other places. Which location would you like to learn about?";
//            }

//            if (message.Contains("weather") || message.Contains("climate"))
//            {
//                return "Phu Yen has a tropical monsoon climate. The dry season from January to August is ideal for tourism with temperatures from 25-32°C. The rainy season is from September to December. I recommend visiting Phu Yen between March and July for the best weather.";
//            }

//            if (message.Contains("tour"))
//            {
//                return "We have various attractive tours: 1-day tours, 2-day-1-night tours, 3-day-2-night tours exploring all of Phu Yen. Each tour has detailed itineraries and reasonable prices. Which tour are you interested in?";
//            }

//            return "Hello! I'm the Phu Yen travel assistant. I can help you learn about tourist attractions, weather information, tours, and many interesting things about Phu Yen. What would you like to know?";
//        }

//        private string GetKoreanFallback(string message)
//        {
//            if (message.Contains("안녕") || message.Contains("하이"))
//            {
//                return "안녕하세요! 저는 푸옌 여행 도우미입니다. 관광 명소, 투어, 날씨 등 푸옌에 대한 많은 흥미로운 정보를 제공할 수 있습니다. 무엇을 알고 싶으신가요?";
//            }

//            if (message.Contains("장소") || message.Contains("관광"))
//            {
//                return "푸옌에는 간다디아(돌 접시 급류), 바이셉 해변, 무이디엔(디엔 곶) 등 많은 유명한 관광 명소가 있습니다. 어떤 장소에 대해 알고 싶으신가요?";
//            }

//            if (message.Contains("날씨") || message.Contains("기후"))
//            {
//                return "푸옌은 열대 몬순 기후입니다. 1월부터 8월까지의 건기는 25-32°C의 기온으로 관광하기에 이상적입니다. 우기는 9월부터 12월까지입니다. 3월에서 7월 사이에 푸옌을 방문하시는 것을 추천합니다.";
//            }

//            if (message.Contains("투어"))
//            {
//                return "다양한 매력적인 투어가 있습니다: 1일 투어, 2일 1박 투어, 3일 2박 푸옌 전체 탐험 투어. 각 투어는 상세한 일정과 합리적인 가격을 제공합니다. 어떤 투어에 관심이 있으신가요?";
//            }

//            return "안녕하세요! 저는 푸옌 여행 도우미입니다. 관광 명소, 날씨 정보, 투어 등 푸옌에 대한 많은 정보를 제공할 수 있습니다. 무엇을 알고 싶으신가요?";
//        }

//        private string GetChineseFallback(string message)
//        {
//            if (message.Contains("你好") || message.Contains("您好"))
//            {
//                return "您好！我是富安旅游助手。我可以帮助您了解旅游景点、旅游线路、天气等富安的许多有趣信息。您想了解什么？";
//            }

//            if (message.Contains("地方") || message.Contains("景点"))
//            {
//                return "富安有许多著名的旅游景点，如：石盘滩、白沙滩、电角等许多地方。您想了解哪个景点？";
//            }

//            if (message.Contains("天气") || message.Contains("气候"))
//            {
//                return "富安属热带季风气候。1月至8月的旱季温度为25-32°C，非常适合旅游。雨季是9月至12月。我建议您在3月至7月之间访问富安，天气最好。";
//            }

//            if (message.Contains("旅游") || message.Contains("线路"))
//            {
//                return "我们有各种吸引人的旅游线路：1日游、2天1夜游、3天2夜富安全境游。每条线路都有详细的行程安排和合理的价格。您对哪条线路感兴趣？";
//            }

//            return "您好！我是富安旅游助手。我可以帮助您了解旅游景点、天气信息、旅游线路等富安的许多信息。您想了解什么？";
//        }

//        private string GetVietnameseFallback(string message)
//        {
//            if (message.Contains("xin chào") || message.Contains("chào") || message.Contains("hello") || message.Contains("hi"))
//            {
//                return "Xin chào! Tôi là trợ lý du lịch Phú Yên. Tôi có thể giúp bạn tìm hiểu về các địa điểm du lịch, tour tham quan, thời tiết và nhiều thông tin hữu ích khác về Phú Yên. Bạn muốn biết về điều gì?";
//            }

//            if (message.Contains("địa điểm") || message.Contains("tham quan"))
//            {
//                return "Phú Yên có rất nhiều địa điểm du lịch nổi tiếng như: Gành Đá Đĩa, Bãi Xép, Mũi Điện, Tháp Nhạn, Vịnh Vũng Rô, và nhiều địa điểm khác. Bạn muốn tìm hiểu về địa điểm nào?";
//            }

//            if (message.Contains("thời tiết"))
//            {
//                return "Phú Yên có khí hậu nhiệt đới gió mùa. Mùa khô từ tháng 1-8 với thời tiết nắng đẹp, ít mưa, rất thích hợp du lịch. Mùa mưa từ tháng 9-12. Thời điểm lý tưởng nhất để du lịch Phú Yên là từ tháng 3 đến tháng 7.";
//            }

//            if (message.Contains("tour"))
//            {
//                return "Chúng tôi có nhiều tour du lịch hấp dẫn: Tour 1 ngày, Tour 2 ngày 1 đêm, Tour 3 ngày 2 đêm khám phá toàn bộ Phú Yên. Mỗi tour đều có lịch trình chi tiết và giá cả hợp lý. Bạn quan tâm đến tour nào?";
//            }

//            return "Xin chào! Tôi là trợ lý du lịch Phú Yên. Tôi có thể giúp bạn tìm hiểu về các địa điểm du lịch, thông tin thời tiết, các tour du lịch, ẩm thực địa phương và nhiều thông tin hữu ích khác. Bạn cần tư vấn về vấn đề gì?";
//        }
//    }
//}