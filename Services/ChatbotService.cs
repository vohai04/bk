using BookInfoFinder.Services.Interface;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace BookInfoFinder.Services;

public class ChatbotService : IChatbotService
{
    private readonly IBookService _bookService;
    private readonly IRatingService _ratingService;
    private readonly IFavoriteService _favoriteService;
    private readonly ICategoryService _categoryService;
    private readonly IConfiguration _config;
    private readonly string? _geminiKey;

    public ChatbotService(IBookService bookService, IRatingService ratingService, IFavoriteService favoriteService, ICategoryService categoryService, IConfiguration config)
    {
        _bookService = bookService;
        _ratingService = ratingService;
        _favoriteService = favoriteService;
        _categoryService = categoryService;
        _config = config;
        _geminiKey = GetGeminiKey();
    }

    private string? GetGeminiKey()
    {
        var key = _config["Gemini:ApiKey"];
        return (!string.IsNullOrEmpty(key) && key != "AIzaSyDzuwVcd6_UUVrl5SPH69UUhQsMxB1_BCA") ? key : null;
    }

    public async Task<string> GetChatbotReplyAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Xin chào! Tôi là trợ lý tư vấn sách của BookInfoFinder. Bạn có thể hỏi tôi về sách, tác giả, thể loại hoặc nhờ tôi gợi ý sách hay. 📚";
        }

        try
        {
            string lowerMessage = message.ToLower().Trim();

            // 1. XỬ LÝ CHÀO HỎI VÀ GIAO TIẾP CƠ BẢN
            if (IsGreeting(lowerMessage))
            {
                return GetGreetingResponse();
            }

            if (IsFarewell(lowerMessage))
            {
                return "Tạm biệt! Hẹn gặp lại bạn. Chúc bạn tìm được những cuốn sách hay! 👋📖";
            }

            if (IsThanking(lowerMessage))
            {
                return "Không có gì! Rất vui được giúp bạn. Nếu cần thêm tư vấn về sách, cứ hỏi tôi nhé! 😊";
            }

            if (IsQuestion(lowerMessage))
            {
                return await HandleGeneralQuestion(message, lowerMessage);
            }

            // 2. PHÂN TÍCH Ý ĐỊNH TÌM KIẾM
            var intent = AnalyzeIntent(lowerMessage);

            // 3. XỬ LÝ GỢI Ý SÁCH HAY / PHỔ BIẾN
            if (intent.IsRecommendation || intent.IsTrending || intent.IsTopRated || intent.IsNewBooks)
            {
                return await HandleRecommendation(intent, message);
            }

            // 4. XỬ LÝ TÌM KIẾM THEO THỂ LOẠI
            if (!string.IsNullOrEmpty(intent.Category))
            {
                return await HandleCategorySearch(intent.Category, message);
            }

            // 5. XỬ LÝ TÌM KIẾM THEO TÁC GIẢ
            if (!string.IsNullOrEmpty(intent.Author))
            {
                return await HandleAuthorSearch(intent.Author);
            }

            // 6. TÌM KIẾM THÔNG MINH VỚI GEMINI AI
            if (_geminiKey != null)
            {
                return await HandleAISearch(message, lowerMessage);
            }

            // 7. FALLBACK: TÌM KIẾM TRUYỀN THỐNG
            return await HandleFallbackSearch(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chatbot Error: {ex.Message}");
            return "Xin lỗi, có lỗi xảy ra. Bạn có thể thử hỏi theo cách khác hoặc tìm kiếm trực tiếp trên trang nhé! 🔍";
        }
    }

    #region Intent Analysis

    private class ChatIntent
    {
        public bool IsRecommendation { get; set; }
        public bool IsTrending { get; set; }
        public bool IsTopRated { get; set; }
        public bool IsNewBooks { get; set; }
        public string? Category { get; set; }
        public string? Author { get; set; }
        public string? Title { get; set; }
    }

    private ChatIntent AnalyzeIntent(string message)
    {
        var intent = new ChatIntent();

        // Gợi ý / Recommendation keywords
        string[] recommendKeywords = { "gợi ý", "giới thiệu", "tư vấn", "đề xuất", "nên đọc", "sách hay", "sách nào hay", "sách tốt", "đáng đọc" };
        intent.IsRecommendation = recommendKeywords.Any(k => message.Contains(k));

        // Trending keywords
        string[] trendingKeywords = { "trending", "xu hướng", "yêu thích", "phổ biến", "hot", "nổi bật", "được ưa chuộng", "được yêu thích nhiều", "yêu thích nhiều", "hot nhất", "phổ biến nhất" };
        intent.IsTrending = trendingKeywords.Any(k => message.Contains(k));

        // Top rated keywords
        string[] topRatedKeywords = { "đánh giá cao", "xếp hạng cao", "rating", "điểm cao", "nổi tiếng" };
        intent.IsTopRated = topRatedKeywords.Any(k => message.Contains(k));

        // New books keywords
        string[] newBooksKeywords = { "mới", "mới ra mắt", "mới nhất", "gần đây", "recent", "new", "sản xuất mới" };
        intent.IsNewBooks = newBooksKeywords.Any(k => message.Contains(k));

        // Extract category
        if (message.Contains("thể loại") || message.Contains("genre") || message.Contains("loại sách"))
        {
            var parts = message.Split(new[] { "thể loại", "genre", "loại sách" }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                intent.Category = parts[1].Trim().Split(' ').FirstOrDefault()?.Trim(',', '.', '?', '!');
            }
        }
        else
        {
            // Nhận diện thể loại trực tiếp
            string[] categoryKeywords = { "khoa học", "tiểu thuyết", "trinh thám", "kinh dị", "lãng mạn", "văn học", "self-help", "tự phát triển", "kinh doanh", "lịch sử", "thiếu nhi", "truyện tranh", "manga", "light novel" };
            foreach (var keyword in categoryKeywords)
            {
                if (message.Contains(keyword))
                {
                    intent.Category = keyword;
                    break;
                }
            }
        }

        // Extract author
        if (message.Contains("tác giả") || message.Contains("author") || message.Contains("của"))
        {
            var parts = message.Split(new[] { "tác giả", "author", "của" }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                intent.Author = parts[1].Trim().Split(' ', 3).Take(3).Aggregate((a, b) => a + " " + b).Trim(',', '.', '?', '!');
            }
        }

        return intent;
    }

    private bool IsGreeting(string message)
    {
        string[] greetings = { "xin chào", "chào", "hello", "hi", "hey", "hế nhô", "hế lô", "chào bạn", "chào bot" };
        return greetings.Any(g => message.StartsWith(g) || message == g);
    }

    private bool IsFarewell(string message)
    {
        string[] farewells = { "tạm biệt", "bye", "goodbye", "hẹn gặp lại", "thôi", "thoát", "kết thúc" };
        return farewells.Any(f => message.Contains(f));
    }

    private bool IsThanking(string message)
    {
        string[] thanks = { "cảm ơn", "cám ơn", "thank", "thanks", "cảm ơn bạn", "cảm ơn bot" };
        return thanks.Any(t => message.Contains(t));
    }

    private bool IsQuestion(string message)
    {
        string[] questionWords = { "là gì", "như thế nào", "thế nào", "tại sao", "vì sao", "có thể", "có", "bao nhiêu", "khi nào" };
        return message.EndsWith("?") || questionWords.Any(q => message.Contains(q));
    }

    private string GetGreetingResponse()
    {
        string[] responses = {
            "Xin chào! 👋 Tôi là trợ lý ảo của BookInfoFinder. Tôi có thể giúp bạn tìm sách, gợi ý sách hay, hoặc tư vấn về tác giả và thể loại. Bạn muốn tìm gì hôm nay?",
            "Chào bạn! 😊 Rất vui được gặp bạn. Hãy cho tôi biết bạn đang tìm kiếm loại sách nào, tôi sẽ giúp bạn tìm những cuốn sách phù hợp nhất!",
            "Hello! 📚 Tôi có thể giúp bạn khám phá thế giới sách. Bạn muốn đọc thể loại gì? Hoặc có tác giả yêu thích nào không?"
        };
        return responses[new Random().Next(responses.Length)];
    }

    #endregion

    #region Handler Methods

    private async Task<string> HandleGeneralQuestion(string message, string lowerMessage)
    {
        if (lowerMessage.Contains("bạn là ai") || lowerMessage.Contains("bạn là gì"))
        {
            return "Tôi là trợ lý ảo của BookInfoFinder, được thiết kế để giúp bạn tìm kiếm và khám phá sách. Tôi có thể tư vấn sách dựa trên sở thích của bạn, giới thiệu sách hay, và trả lời câu hỏi về sách, tác giả, thể loại. Hãy hỏi tôi bất cứ điều gì về sách nhé! 📖✨";
        }

        if (lowerMessage.Contains("làm được gì") || lowerMessage.Contains("giúp gì"))
        {
            return "Tôi có thể giúp bạn:\n" +
                   "📚 Tìm sách theo tên, tác giả, hoặc thể loại\n" +
                   "⭐ Gợi ý sách đánh giá cao nhất\n" +
                   "❤️ Giới thiệu sách được yêu thích nhiều\n" +
                   "🔥 Tư vấn sách phù hợp với sở thích của bạn\n" +
                   "💡 Trả lời câu hỏi về sách, tác giả, thể loại\n\n" +
                   "Hãy thử hỏi tôi: 'gợi ý sách hay', 'sách khoa học', hoặc 'tác giả Nguyễn Nhật Ánh'!";
        }

        // Use AI to answer other questions if available
        if (_geminiKey != null)
        {
            return await HandleAISearch(message, lowerMessage);
        }

        return "Xin lỗi, tôi chưa hiểu câu hỏi của bạn. Bạn có thể hỏi về sách, tác giả, hoặc nhờ tôi gợi ý sách hay không? 🤔";
    }

    private async Task<string> HandleRecommendation(ChatIntent intent, string message)
    {
        if (intent.IsTopRated)
        {
            var (books, total) = await _ratingService.GetTopRatedBooksPagedAsync(1, 5);
            if (total > 0)
            {
                var bookList = books.Take(3).Select(b => $"📖 {b.Title} - {b.AverageRating:F1}⭐ ({b.RatingCount} đánh giá)").ToList();
                return $"Dưới đây là {total} cuốn sách có đánh giá cao nhất:\n\n" +
                       string.Join("\n", bookList) +
                       (total > 3 ? $"\n\n...và còn {total - 3} cuốn khác nữa!" : "") +
                       "\n\nBạn có thể tìm kiếm trực tiếp để xem chi tiết nhé! 🔍";
            }
        }

        if (intent.IsTrending)
        {
            var (books, total) = await _favoriteService.GetMostFavoritedBooksPagedAsync(1, 5);
            if (total > 0)
            {
                var bookList = books.Take(3).Select(b => $"❤️ {b.Title}").ToList();
                return $"Các cuốn sách đang được yêu thích nhất:\n\n" +
                       string.Join("\n", bookList) +
                       (total > 3 ? $"\n\n...và còn {total - 3} cuốn nữa!" : "") +
                       "\n\nĐây là những cuốn đang 'hot' đấy! 🔥";
            }
        }

        if (intent.IsNewBooks)
        {
            var (books, total) = await _bookService.SearchBooksWithStatsPagedAsync(null, null, null, null, 1, 5, "PublicationDate desc");
            if (total > 0)
            {
                var bookList = books.Take(3).Select(b => $"📖 {b.Title} - {b.AuthorName ?? "Không rõ"} ({b.PublicationDate.Year})").ToList();
                return $"Sách mới ra mắt gần đây:\n\n" +
                       string.Join("\n", bookList) +
                       (total > 3 ? $"\n\n...và còn {total - 3} cuốn nữa!" : "") +
                       "\n\nBạn có thể tìm kiếm để xem thêm! 🆕";
            }
        }

        // General recommendation
        var (topBooks, topTotal) = await _ratingService.GetTopRatedBooksPagedAsync(1, 5);
        if (topTotal > 0)
        {
            var bookList = topBooks.Take(3).Select(b => $"📚 {b.Title} - {b.AverageRating:F1}⭐").ToList();
            return "Tôi gợi ý những cuốn sách hay này cho bạn:\n\n" +
                   string.Join("\n", bookList) +
                   "\n\nĐây là những cuốn có đánh giá tốt nhất từ cộng đồng độc giả! 💯";
        }

        return "Hiện tại chưa có dữ liệu đánh giá. Bạn thử tìm theo thể loại hoặc tác giả yêu thích nhé! 📖";
    }

    private async Task<string> HandleCategorySearch(string category, string fullMessage)
    {
        var (books, total) = await _bookService.SearchBooksWithStatsPagedAsync(null, null, category, null, 1, 5, null);
        
        if (total > 0)
        {
            var bookList = books.Take(3).Select(b => $"📖 {b.Title} - {b.AuthorName ?? "Không rõ tác giả"}").ToList();
            return $"Trong thể loại '{category}', tôi tìm thấy {total} cuốn sách:\n\n" +
                   string.Join("\n", bookList) +
                   (total > 3 ? $"\n\n...và còn {total - 3} cuốn nữa!" : "") +
                   "\n\nBạn muốn xem chi tiết cuốn nào không? 🔍";
        }

        // Suggest similar categories using AI
        if (_geminiKey != null)
        {
            var categories = await _categoryService.GetAllCategoriesAsync();
            var categoryNames = categories.Select(c => c.Name).ToList();
            string context = $"Database có các thể loại: {string.Join(", ", categoryNames)}";
            string prompt = $"{context}\n\nNgười dùng tìm '{category}' nhưng không có. Hãy gợi ý 2-3 thể loại tương tự từ danh sách trên. Trả lời ngắn gọn, thân thiện bằng tiếng Việt.";
            
            var aiResponse = await CallGemini(prompt, _geminiKey);
            return $"Xin lỗi, tôi không tìm thấy sách nào trong thể loại '{category}'. 😔\n\n{aiResponse}";
        }

        var allCategories = await _categoryService.GetAllCategoriesAsync();
        var availableCategories = string.Join(", ", allCategories.Select(c => c.Name).Take(5));
        return $"Không tìm thấy thể loại '{category}'. Các thể loại có sẵn: {availableCategories}... 📚";
    }

    private async Task<string> HandleAuthorSearch(string author)
    {
        var (books, total) = await _bookService.SearchBooksWithStatsPagedAsync(null, author, null, null, 1, 5, null);
        
        if (total > 0)
        {
            var bookList = books.Take(3).Select(b => $"📖 {b.Title}").ToList();
            return $"Tác giả '{author}' có {total} cuốn sách trong hệ thống:\n\n" +
                   string.Join("\n", bookList) +
                   (total > 3 ? $"\n\n...và còn {total - 3} cuốn nữa!" : "") +
                   "\n\nBạn có thể tìm kiếm để xem đầy đủ! 🔎";
        }

        return $"Rất tiếc, tôi không tìm thấy sách nào của tác giả '{author}' trong hệ thống. Bạn có thể thử tên tác giả khác hoặc tìm theo thể loại nhé! ✍️";
    }

    private async Task<string> HandleAISearch(string message, string lowerMessage)
    {
        // Get context from database
        var (books, total) = await _bookService.SearchBooksAdminPagedAsync(message, null, null, null, 1, 8, null);
        
        string context = "Thông tin từ database BookInfoFinder:\n";
        if (total > 0)
        {
            foreach (var book in books.Take(5))
            {
                context += $"- '{book.Title}' của {book.AuthorName ?? "Không rõ tác giả"}, thể loại {book.CategoryName ?? "Không rõ"}, mô tả: {book.Description ?? "Không có"}.\n";
            }
        }
        else
        {
            context += "Không tìm thấy sách phù hợp với từ khóa trực tiếp.\n";
        }

        // Get categories for context
        var categories = await _categoryService.GetAllCategoriesAsync();
        context += $"\nCác thể loại có sẵn: {string.Join(", ", categories.Select(c => c.Name).Take(10))}";

        string systemPrompt = @"Bạn là trợ lý ảo thân thiện và chuyên nghiệp của BookInfoFinder, một website tìm kiếm sách.

NHIỆM VỤ:
- Tư vấn sách dựa trên database được cung cấp
- Trả lời thân thiện, nhiệt tình, dễ hiểu
- Sử dụng emoji phù hợp để sinh động
- Nếu không có sách phù hợp, gợi ý các lựa chọn tương tự
- Luôn khuyến khích người dùng tìm kiếm trực tiếp trên website

QUY TẮC:
✅ Trả lời ngắn gọn (3-5 câu)
✅ Ưu tiên sách có trong database
✅ Sử dụng tiếng Việt tự nhiên
✅ Thêm emoji để thân thiện
❌ Không bịa đặt thông tin
❌ Không trả lời dài dòng
❌ Không nói về những gì không liên quan đến sách";

        string prompt = $"{systemPrompt}\n\n{context}\n\nNgười dùng: {message}\n\nHãy tư vấn một cách hữu ích và thân thiện:";
        
        var reply = await CallGemini(prompt, _geminiKey!);
        
        // Add a call-to-action if books were found
        if (total > 0 && !reply.Contains("tìm kiếm") && !reply.Contains("🔍"))
        {
            reply += "\n\n🔍 Bạn có thể tìm kiếm trực tiếp để xem chi tiết và đánh giá nhé!";
        }
        
        return reply;
    }

    private async Task<string> HandleFallbackSearch(string message)
    {
        // Try title search
        var (booksByTitle, totalTitle) = await _bookService.SearchBooksWithStatsPagedAsync(message, null, null, null, 1, 5, null);
        
        if (totalTitle > 0)
        {
            var bookList = booksByTitle.Take(3).Select(b => $"📖 {b.Title} - {b.AuthorName ?? "Không rõ"}").ToList();
            return $"Tôi tìm thấy {totalTitle} cuốn sách liên quan:\n\n" +
                   string.Join("\n", bookList) +
                   (totalTitle > 3 ? $"\n\n...và còn {totalTitle - 3} cuốn nữa!" : "") +
                   "\n\nBạn có thể tìm kiếm chi tiết hơn nhé! 🔍";
        }

        // Try author search
        var (booksByAuthor, totalAuthor) = await _bookService.SearchBooksWithStatsPagedAsync(null, message, null, null, 1, 5, null);
        
        if (totalAuthor > 0)
        {
            var bookList = booksByAuthor.Take(3).Select(b => $"📖 {b.Title}").ToList();
            return $"Tôi tìm thấy {totalAuthor} cuốn sách của tác giả liên quan:\n\n" +
                   string.Join("\n", bookList) +
                   "\n\nBạn muốn biết thêm về cuốn nào không? 📚";
        }

        // Try category search
        var (booksByCategory, totalCategory) = await _bookService.SearchBooksWithStatsPagedAsync(null, null, message, null, 1, 5, null);
        
        if (totalCategory > 0)
        {
            var bookList = booksByCategory.Take(3).Select(b => $"📖 {b.Title}").ToList();
            return $"Trong thể loại liên quan, tôi tìm thấy {totalCategory} cuốn:\n\n" +
                   string.Join("\n", bookList) +
                   "\n\nBạn có thể xem thêm bằng cách tìm kiếm! 🔎";
        }

        return $"Xin lỗi, tôi không tìm thấy sách nào với từ khóa '{message}'. 😔\n\n" +
               "Bạn có thể thử:\n" +
               "💡 Gõ tên sách chính xác hơn\n" +
               "💡 Tìm theo tên tác giả\n" +
               "💡 Tìm theo thể loại như 'khoa học', 'tiểu thuyết'\n" +
               "💡 Hỏi tôi 'gợi ý sách hay'\n\n" +
               "Tôi luôn sẵn sàng giúp bạn! 😊";
    }

    #endregion

    #region Gemini API

    private async Task<string> CallGemini(string prompt, string key)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 500,
                    topP = 0.95,
                    topK = 40
                }
            };

            var response = await client.PostAsJsonAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={key}", 
                payload
            );

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                
                try
                {
                    var text = result
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();
                    
                    return text ?? "Xin lỗi, tôi không thể xử lý câu trả lời lúc này.";
                }
                catch
                {
                    return "Xin lỗi, có lỗi khi xử lý phản hồi từ AI. Vui lòng thử lại! 🤖";
                }
            }

            return "Xin lỗi, không thể kết nối với AI lúc này. Bạn có thể thử tìm kiếm trực tiếp nhé! 🔍";
        }
        catch (TaskCanceledException)
        {
            return "Yêu cầu mất quá nhiều thời gian. Bạn thử lại hoặc tìm kiếm trực tiếp nhé! ⏱️";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Gemini API Error: {ex.Message}");
            return "Xin lỗi, có lỗi xảy ra. Hãy thử tìm kiếm trực tiếp trên trang! 🔍";
        }
    }

    #endregion
}