using DA_Web.Data;
using DA_Web.Models;
using DA_Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DA_Web.Hubs
{
    public class ChatHub : Hub
    {
        private readonly GeminiService _geminiService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(GeminiService geminiService, ApplicationDbContext context, ILogger<ChatHub> logger)
        {
            _geminiService = geminiService;
            _context = context;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        public async Task GetConversation()
        {
            try
            {
                var userId = GetUserId();
                var messages = await _context.ChatMessages
                    .Where(m => m.UserId == userId)
                    .OrderBy(m => m.CreatedAt)
                    .Take(50)
                    .Select(m => new
                    {
                        id = m.Id,
                        sender = m.IsBot ? "bot" : "user",
                        message = m.Message,
                        time = m.CreatedAt
                    })
                    .ToListAsync();

                await Clients.Caller.SendAsync("ConversationHistory", messages);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting conversation: {ex.Message}");
                await Clients.Caller.SendAsync("ConversationHistory", new List<object>());
            }
        }

        public async Task UserMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            try
            {
                var userId = GetUserId();
                _logger.LogInformation($"User {userId} sent: {message}");

                // Lưu tin nhắn người dùng
                var userMessage = new ChatMessage
                {
                    UserId = userId,
                    Message = message,
                    IsBot = false,
                    CreatedAt = DateTime.Now
                };

                _context.ChatMessages.Add(userMessage);
                await _context.SaveChangesAsync();

                // Gửi xác nhận
                await Clients.Caller.SendAsync("UserMessageSent", new
                {
                    id = userMessage.Id,
                    sender = "user",
                    message = message,
                    time = userMessage.CreatedAt
                });

                // Thông báo đang xử lý
                await Clients.Caller.SendAsync("BotTyping");

                // Lấy phản hồi từ bot
                var botResponse = await _geminiService.ProcessMessageAsync(userId, message);

                // Lưu tin nhắn bot
                var botMessage = new ChatMessage
                {
                    UserId = userId,
                    Message = botResponse,
                    IsBot = true,
                    CreatedAt = DateTime.Now
                };

                _context.ChatMessages.Add(botMessage);
                await _context.SaveChangesAsync();

                // Gửi tin nhắn bot
                await Clients.Caller.SendAsync("BotMessage", new
                {
                    id = botMessage.Id,
                    sender = "bot",
                    message = botResponse,
                    time = botMessage.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing message: {ex.Message}");

                // Gửi tin nhắn lỗi
                await Clients.Caller.SendAsync("BotMessage", new
                {
                    id = 0,
                    sender = "bot",
                    message = "Xin lỗi, tôi đang gặp sự cố. Vui lòng thử lại sau.",
                    time = DateTime.Now
                });
            }
        }

        public async Task Authenticate(string? token)
        {
            try
            {
                if (!string.IsNullOrEmpty(token))
                {
                    // Implement JWT validation here if needed
                    // For now, just log
                    _logger.LogInformation("User authenticated with token");
                }

                await GetConversation();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Authentication error: {ex.Message}");
            }
        }

        private int GetUserId()
        {
            // Lấy UserId từ Context nếu có authentication
            var userIdClaim = Context.User?.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            // Return 0 cho anonymous users
            return 0;
        }
    }
}

//using DA_Web.Data;
//using DA_Web.Models;
//using DA_Web.Services;
//using Microsoft.AspNetCore.SignalR;
//using Microsoft.EntityFrameworkCore;
//using System.Text.Json;

//namespace DA_Web.Hubs
//{
//    public class ChatHub : Hub
//    {
//        private readonly GeminiService _geminiService;
//        private readonly ApplicationDbContext _context;
//        private readonly ILogger<ChatHub> _logger;
//        private readonly Dictionary<string, string> _userLanguages = new();

//        public ChatHub(GeminiService geminiService, ApplicationDbContext context, ILogger<ChatHub> logger)
//        {
//            _geminiService = geminiService;
//            _context = context;
//            _logger = logger;
//        }

//        public override async Task OnConnectedAsync()
//        {
//            _logger.LogInformation($"Client connected: {Context.ConnectionId}");

//            // Set default language
//            _userLanguages[Context.ConnectionId] = "vi";

//            await base.OnConnectedAsync();
//        }

//        public override async Task OnDisconnectedAsync(Exception? exception)
//        {
//            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");

//            // Remove language preference
//            _userLanguages.Remove(Context.ConnectionId);

//            await base.OnDisconnectedAsync(exception);
//        }

//        public async Task SetLanguage(string language)
//        {
//            _userLanguages[Context.ConnectionId] = language;
//            await Clients.Caller.SendAsync("LanguageChanged", language);

//            // Send welcome message in new language
//            var welcomeMessage = GetWelcomeMessage(language);
//            await Clients.Caller.SendAsync("BotMessage", new
//            {
//                id = 0,
//                sender = "bot",
//                message = welcomeMessage,
//                time = DateTime.Now
//            });
//        }

//        public async Task GetConversation()
//        {
//            try
//            {
//                var userId = GetUserId();
//                var messages = await _context.ChatMessages
//                    .Where(m => m.UserId == userId)
//                    .OrderBy(m => m.CreatedAt)
//                    .Take(50)
//                    .Select(m => new
//                    {
//                        id = m.Id,
//                        sender = m.IsBot ? "bot" : "user",
//                        message = m.Message,
//                        time = m.CreatedAt
//                    })
//                    .ToListAsync();

//                await Clients.Caller.SendAsync("ConversationHistory", messages);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError($"Error getting conversation: {ex.Message}");
//                await Clients.Caller.SendAsync("ConversationHistory", new List<object>());
//            }
//        }

//        public async Task UserMessage(string message, bool isVoiceInput = false)
//        {
//            if (string.IsNullOrWhiteSpace(message)) return;

//            try
//            {
//                var userId = GetUserId();
//                var language = _userLanguages.GetValueOrDefault(Context.ConnectionId, "vi");

//                _logger.LogInformation($"User {userId} sent ({language}): {message}");

//                // Lưu tin nhắn người dùng
//                var userMessage = new ChatMessage
//                {
//                    UserId = userId,
//                    Message = message,
//                    IsBot = false,
//                    CreatedAt = DateTime.Now
//                };

//                _context.ChatMessages.Add(userMessage);
//                await _context.SaveChangesAsync();

//                // Gửi xác nhận
//                await Clients.Caller.SendAsync("UserMessageSent", new
//                {
//                    id = userMessage.Id,
//                    sender = "user",
//                    message = message,
//                    time = userMessage.CreatedAt,
//                    isVoiceInput = isVoiceInput
//                });

//                // Thông báo đang xử lý
//                await Clients.Caller.SendAsync("BotTyping");

//                // Lấy phản hồi từ bot với ngôn ngữ tương ứng
//                var botResponse = await _geminiService.ProcessMessageAsync(userId, message, language);

//                // Lưu tin nhắn bot
//                var botMessage = new ChatMessage
//                {
//                    UserId = userId,
//                    Message = botResponse,
//                    IsBot = true,
//                    CreatedAt = DateTime.Now
//                };

//                _context.ChatMessages.Add(botMessage);
//                await _context.SaveChangesAsync();

//                // Lấy smart suggestions
//                var suggestions = await GetSmartSuggestions(message, botResponse, userId, language);

//                // Gửi tin nhắn bot với suggestions
//                await Clients.Caller.SendAsync("BotMessage", new
//                {
//                    id = botMessage.Id,
//                    sender = "bot",
//                    message = botResponse,
//                    time = botMessage.CreatedAt,
//                    suggestions = suggestions,
//                    enableVoice = true // Enable text-to-speech
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError($"Error processing message: {ex.Message}");

//                // Gửi tin nhắn lỗi
//                var errorMessage = GetErrorMessage(_userLanguages.GetValueOrDefault(Context.ConnectionId, "vi"));
//                await Clients.Caller.SendAsync("BotMessage", new
//                {
//                    id = 0,
//                    sender = "bot",
//                    message = errorMessage,
//                    time = DateTime.Now
//                });
//            }
//        }

//        private async Task<List<string>> GetSmartSuggestions(string userMessage, string botResponse, int userId, string language)
//        {
//            var suggestions = new List<string>();
//            var lowerMessage = userMessage.ToLower();
//            var lowerResponse = botResponse.ToLower();

//            try
//            {
//                // Analyze context for location-based suggestions
//                if (lowerResponse.Contains("gành đá đĩa") || lowerResponse.Contains("ganh da dia"))
//                {
//                    suggestions.AddRange(GetLocationSuggestions("Gành Đá Đĩa", language));
//                }
//                else if (lowerResponse.Contains("bãi xép") || lowerResponse.Contains("bai xep"))
//                {
//                    suggestions.AddRange(GetLocationSuggestions("Bãi Xép", language));
//                }
//                else if (lowerResponse.Contains("mũi điện") || lowerResponse.Contains("mui dien"))
//                {
//                    suggestions.AddRange(GetLocationSuggestions("Mũi Điện", language));
//                }

//                // Tour-based suggestions
//                if (lowerMessage.Contains("tour") || lowerResponse.Contains("tour"))
//                {
//                    var tours = await _context.Tours
//                        .OrderBy(t => t.Id)
//                        .Take(3)
//                        .Select(t => t.Destination ?? t.Description)
//                        .ToListAsync();

//                    foreach (var tour in tours)
//                    {
//                        suggestions.Add(GetTourSuggestion(tour, language));
//                    }
//                }

//                // Weather suggestions
//                if (lowerMessage.Contains("thời tiết") || lowerMessage.Contains("weather") ||
//                    lowerMessage.Contains("날씨") || lowerMessage.Contains("天气"))
//                {
//                    suggestions.Add(GetWeatherSuggestion(language));
//                }

//                // General suggestions if no specific context
//                if (suggestions.Count == 0)
//                {
//                    suggestions.AddRange(GetGeneralSuggestions(language));
//                }

//                // Limit to 4 suggestions
//                return suggestions.Take(4).ToList();
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError($"Error getting smart suggestions: {ex.Message}");
//                return GetGeneralSuggestions(language);
//            }
//        }

//        private List<string> GetLocationSuggestions(string location, string language)
//        {
//            return language switch
//            {
//                "en" => new List<string>
//                {
//                    $"How to get to {location}?",
//                    $"Opening hours and ticket price?",
//                    "Nearby attractions?",
//                    "Best time to visit?"
//                },
//                "ko" => new List<string>
//                {
//                    $"{location}까지 어떻게 가나요?",
//                    "영업시간과 입장료?",
//                    "근처 관광지?",
//                    "방문하기 좋은 시간?"
//                },
//                "zh" => new List<string>
//                {
//                    $"如何前往{location}？",
//                    "开放时间和门票价格？",
//                    "附近的景点？",
//                    "最佳参观时间？"
//                },
//                _ => new List<string> // Vietnamese default
//                {
//                    $"Cách di chuyển đến {location}?",
//                    "Giờ mở cửa và giá vé?",
//                    "Địa điểm gần đó nên ghé?",
//                    "Thời gian tốt nhất để tham quan?"
//                }
//            };
//        }

//        private string GetTourSuggestion(string tourName, string language)
//        {
//            return language switch
//            {
//                "en" => $"Tell me more about {tourName}",
//                "ko" => $"{tourName}에 대해 더 알려주세요",
//                "zh" => $"告诉我更多关于{tourName}的信息",
//                _ => $"Chi tiết về {tourName}"
//            };
//        }

//        private string GetWeatherSuggestion(string language)
//        {
//            return language switch
//            {
//                "en" => "What to pack for the trip?",
//                "ko" => "여행 준비물은?",
//                "zh" => "旅行需要准备什么？",
//                _ => "Cần chuẩn bị gì cho chuyến đi?"
//            };
//        }

//        private List<string> GetGeneralSuggestions(string language)
//        {
//            return language switch
//            {
//                "en" => new List<string>
//                {
//                    "Popular tourist spots",
//                    "Recommended tours",
//                    "Local cuisine",
//                    "Weather forecast"
//                },
//                "ko" => new List<string>
//                {
//                    "인기 관광지",
//                    "추천 투어",
//                    "현지 음식",
//                    "날씨 예보"
//                },
//                "zh" => new List<string>
//                {
//                    "热门旅游景点",
//                    "推荐旅游",
//                    "当地美食",
//                    "天气预报"
//                },
//                _ => new List<string>
//                {
//                    "Địa điểm du lịch nổi bật",
//                    "Tour được đề xuất",
//                    "Ẩm thực địa phương",
//                    "Dự báo thời tiết"
//                }
//            };
//        }

//        private string GetWelcomeMessage(string language)
//        {
//            return language switch
//            {
//                "en" => "Hello! I'm Phu Yen travel assistant. How can I help you today?",
//                "ko" => "안녕하세요! 저는 푸옌 여행 도우미입니다. 무엇을 도와드릴까요?",
//                "zh" => "您好！我是富安旅游助手。有什么可以帮助您的吗？",
//                _ => "Xin chào! Tôi là trợ lý du lịch Phú Yên. Tôi có thể giúp gì cho bạn?"
//            };
//        }

//        private string GetErrorMessage(string language)
//        {
//            return language switch
//            {
//                "en" => "Sorry, I'm having technical difficulties. Please try again later.",
//                "ko" => "죄송합니다. 기술적인 문제가 발생했습니다. 나중에 다시 시도해주세요.",
//                "zh" => "抱歉，我遇到了技术问题。请稍后再试。",
//                _ => "Xin lỗi, tôi đang gặp sự cố kỹ thuật. Vui lòng thử lại sau."
//            };
//        }

//        public async Task Authenticate(string? token)
//        {
//            try
//            {
//                if (!string.IsNullOrEmpty(token))
//                {
//                    _logger.LogInformation("User authenticated with token");
//                }

//                await GetConversation();
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError($"Authentication error: {ex.Message}");
//            }
//        }

//        private int GetUserId()
//        {
//            var userIdClaim = Context.User?.FindFirst("UserId")?.Value;
//            if (int.TryParse(userIdClaim, out var userId))
//            {
//                return userId;
//            }
//            return 0;
//        }
//    }
//}