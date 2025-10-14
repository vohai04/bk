using BookInfoFinder.Services.Interface;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Collections.Concurrent;
using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services;

public class ChatbotService : IChatbotService
{
    private readonly IBookService _bookService;
    private readonly IRatingService _ratingService;
    private readonly IFavoriteService _favoriteService;
    private readonly ICategoryService _categoryService;
    private readonly IConfiguration _config;
    private readonly string? _geminiKey;
    
    // Lưu trữ ngữ cảnh cuộc trò chuyện theo session
    private static readonly ConcurrentDictionary<string, ConversationContext> _conversations = new();

    public ChatbotService(IBookService bookService, IRatingService ratingService, 
        IFavoriteService favoriteService, ICategoryService categoryService, IConfiguration config)
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
        Console.WriteLine($"Gemini API Key loaded: {(string.IsNullOrEmpty(key) ? "NULL/EMPTY" : "PRESENT")}");
        return (!string.IsNullOrEmpty(key) ) ? key : null;
    }

    public async Task<string> GetChatbotReplyAsync(string message)
    {
        return await GetChatbotReplyAsync(message, null);
    }

    public async Task<string> GetChatbotReplyAsync(string message, string? sessionId = null)
    {
        sessionId ??= "default";
        
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Xin chào! Tôi là trợ lý tư vấn sách của BookInfoFinder. Bạn có thể hỏi tôi về sách, tác giả, thể loại hoặc nhờ tôi gợi ý sách hay. 📚";
        }

        try
        {
            // Lấy hoặc tạo context cho session
            var context = _conversations.GetOrAdd(sessionId, _ => new ConversationContext());
            context.AddMessage("user", message);

            string lowerMessage = message.ToLower().Trim();

            // Xử lý chào hỏi cơ bản
            if (IsGreeting(lowerMessage) && context.MessageCount <= 1)
            {
                var greeting = GetGreetingResponse();
                context.AddMessage("assistant", greeting);
                return greeting;
            }

            if (IsFarewell(lowerMessage))
            {
                var farewell = "Tạm biệt! Hẹn gặp lại bạn. Chúc bạn tìm được những cuốn sách hay! 👋📖";
                context.Clear(); // Xóa ngữ cảnh khi tạm biệt
                return farewell;
            }

            if (IsThanking(lowerMessage))
            {
                return "Không có gì! Rất vui được giúp bạn. Nếu cần thêm tư vấn về sách, cứ hỏi tôi nhé! 😊";
            }

            // Sử dụng AI để phân tích và trả lời với context
            if (_geminiKey != null)
            {
                return await HandleAIConversation(message, context);
            }

            return "Xin lỗi, chatbot cần API key để hoạt động tốt nhất. Bạn có thể tìm kiếm trực tiếp trên trang! 🔍";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chatbot Error: {ex.Message}");
            return "Xin lỗi, có lỗi xảy ra. Bạn có thể thử hỏi theo cách khác hoặc tìm kiếm trực tiếp trên trang nhé! 🔍";
        }
    }

    private async Task<string> HandleAIConversation(string userMessage, ConversationContext context)
    {
        // Bước 1: AI phân tích ý định và truy vấn database
        var analysisResult = await AnalyzeUserIntentWithAI(userMessage, context);
        
        // Bước 2: Thực hiện truy vấn database dựa trên phân tích
        var databaseResults = await QueryDatabase(analysisResult);
        
        // Bước 3: AI tạo câu trả lời tự nhiên dựa trên kết quả
        var response = await GenerateResponse(userMessage, context, databaseResults);
        
        context.AddMessage("assistant", response);
        return response;
    }

    private async Task<IntentAnalysis> AnalyzeUserIntentWithAI(string message, ConversationContext context)
    {
        string conversationHistory = context.GetFormattedHistory(5); // Lấy 5 tin nhắn gần nhất
        
        string analysisPrompt = $@"Bạn là AI phân tích ý định người dùng trong hệ thống tìm kiếm sách.

LỊCH SỬ HỘI THOẠI:
{conversationHistory}

TIN NHẮN MỚI: ""{message}""

NHIỆM VỤ: Phân tích ý định và đưa ra câu lệnh truy vấn database.

Trả về JSON với format sau:
{{
  ""intent"": ""search_book|search_author|search_category|recommend|ask_about_book|ask_about_author|follow_up|general_chat"",
  ""query_type"": ""title|author|category|rating|favorite|new|mixed"",
  ""search_params"": {{
    ""title"": ""tên sách nếu có"",
    ""author"": ""tên tác giả nếu có"",
    ""category"": ""thể loại nếu có"",
    ""book_mentioned"": ""tên sách được nhắc đến trong lịch sử chat""
  }},
  ""context_reference"": ""tên sách/tác giả từ tin nhắn trước nếu người dùng đang hỏi tiếp"",
  ""explanation"": ""giải thích ngắn gọn về ý định người dùng""
}}

CHÚ Ý:
- Nếu người dùng hỏi tiếp về ""sách đó"", ""cuốn này"", ""tác giả ấy"" => lấy thông tin từ lịch sử
- Nếu hỏi về thể loại, tác giả của sách vừa nhắc => điền book_mentioned
- Phân biệt rõ giữa tìm sách MỚI vs hỏi thêm về sách ĐÃ NHẮC

Chỉ trả về JSON, không thêm text khác.";

        try
        {
            var aiResponse = await CallGemini(analysisPrompt, _geminiKey!);
            
            // Parse JSON response
            var jsonStart = aiResponse.IndexOf('{');
            var jsonEnd = aiResponse.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = aiResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var analysis = JsonSerializer.Deserialize<IntentAnalysis>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (analysis != null)
                {
                    Console.WriteLine($"AI Analysis: {analysis.Explanation}");
                    return analysis;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Intent Analysis Error: {ex.Message}");
        }

        // Fallback: phân tích đơn giản
        return new IntentAnalysis
        {
            Intent = "search_book",
            QueryType = "mixed",
            SearchParams = new SearchParams { Title = message },
            Explanation = "Fallback search"
        };
    }

    private async Task<DatabaseResults> QueryDatabase(IntentAnalysis analysis)
    {
        var results = new DatabaseResults();

        try
        {
            var searchParams = analysis.SearchParams;
            
            // Xác định sách được nhắc đến từ context
            string? bookTitle = searchParams.BookMentioned ?? searchParams.Title;
            string? author = searchParams.Author;
            string? category = searchParams.Category;

            // Query books
            if (!string.IsNullOrEmpty(bookTitle) || !string.IsNullOrEmpty(author) || !string.IsNullOrEmpty(category))
            {
                var (books, total) = await _bookService.SearchBooksWithStatsPagedAsync(
                    bookTitle, author, category, null, 1, 10, null);
                
                results.Books = books.ToList();
                results.TotalBooks = total;
            }

            // Nếu hỏi về rating/trending
            if (analysis.QueryType.Contains("rating"))
            {
                var (topBooks, total) = await _ratingService.GetTopRatedBooksPagedAsync(1, 10);
                results.TopRatedBooks = topBooks.ToList();
            }

            if (analysis.QueryType.Contains("favorite"))
            {
                var (favBooks, total) = await _favoriteService.GetMostFavoritedBooksPagedAsync(1, 10);
                results.TrendingBooks = favBooks.ToList();
            }

            // Lấy categories để có thông tin đầy đủ
            results.AllCategories = (await _categoryService.GetAllCategoriesAsync()).ToList();

            // Nếu có sách cụ thể, lấy thông tin chi tiết
            if (results.Books.Any() && (analysis.Intent == "ask_about_book" || analysis.Intent == "follow_up"))
            {
                var firstBook = results.Books.First();
                results.FocusedBook = firstBook;
                
                // Tìm sách cùng thể loại
                if (!string.IsNullOrEmpty(firstBook.CategoryName))
                {
                    var (relatedBooks, _) = await _bookService.SearchBooksWithStatsPagedAsync(
                        null, null, firstBook.CategoryName, null, 1, 5, null);
                    results.RelatedBooks = relatedBooks.Where(b => b.BookId != firstBook.BookId).ToList();
                }
                
                // Tìm sách cùng tác giả
                if (!string.IsNullOrEmpty(firstBook.AuthorName))
                {
                    var (authorBooks, _) = await _bookService.SearchBooksWithStatsPagedAsync(
                        null, firstBook.AuthorName, null, null, 1, 5, null);
                    results.AuthorBooks = authorBooks.Where(b => b.BookId != firstBook.BookId).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Query Error: {ex.Message}");
        }

        return results;
    }

    private async Task<string> GenerateResponse(string userMessage, ConversationContext context, DatabaseResults dbResults)
    {
        string conversationHistory = context.GetFormattedHistory(5);
        
        string dataContext = BuildDataContext(dbResults);
        
        string systemPrompt = @"Bạn là trợ lý sách thân thiện và thông minh của BookInfoFinder.

TÍNH CÁCH:
- Thân thiện, nhiệt tình, gần gũi như người bạn
- Nhớ ngữ cảnh cuộc trò chuyện và tiếp tục tự nhiên
- Sử dụng emoji phù hợp để sinh động
- Không lặp lại thông tin đã nói trước đó
- Giọng điệu tự nhiên, không máy móc

KỸ NĂNG:
✅ Nhớ sách vừa nhắc đến và trả lời câu hỏi tiếp theo về sách đó
✅ Hiểu câu hỏi mơ hồ như ""sách đó"", ""tác giả ấy"", ""thể loại gì""
✅ Gợi ý sách liên quan thông minh (cùng tác giả, cùng thể loại)
✅ Trả lời ngắn gọn, súc tích (2-4 câu)
✅ Luôn khuyến khích khám phá thêm

QUY TẮC:
❌ KHÔNG lặp lại thông tin đã nói
❌ KHÔNG liệt kê dài dòng
❌ KHÔNG nói ""theo database"" hay ""hệ thống""
❌ KHÔNG bịa đặt thông tin
✅ Nếu không có dữ liệu, gợi ý thay thế
✅ Luôn duy trì ngữ cảnh cuộc trò chuyện";

        string prompt = $@"{systemPrompt}

LỊCH SỬ HỘI THOẠI:
{conversationHistory}

DỮ LIỆU TỪ DATABASE:
{dataContext}

NGƯỜI DÙNG VỪA HỎI: ""{userMessage}""

Hãy trả lời tự nhiên, thân thiện và tiếp nối cuộc trò chuyện. Nếu người dùng hỏi về sách vừa nhắc, trả lời trực tiếp mà không cần nhắc lại tên sách.";

        try
        {
            var response = await CallGemini(prompt, _geminiKey!);
            
            // Làm sạch response
            response = response.Trim();
            
            // Thêm call-to-action nếu phù hợp
            if (dbResults.Books.Any() && !response.Contains("🔍") && !response.Contains("tìm kiếm"))
            {
                if (new Random().Next(100) < 30) // 30% chance
                {
                    response += "\n\n🔍 Bạn có thể tìm kiếm để xem chi tiết nhé!";
                }
            }
            
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Response Generation Error: {ex.Message}");
            return "Xin lỗi, tôi gặp chút vấn đề. Bạn có thể hỏi lại được không? 😅";
        }
    }

    private string BuildDataContext(DatabaseResults results)
    {
        var context = "";

        if (results.FocusedBook != null)
        {
            var book = results.FocusedBook;
            context += $"📖 SÁCH ĐANG BÀN: '{book.Title}'\n";
            context += $"   - Tác giả: {book.AuthorName ?? "Không rõ"}\n";
            context += $"   - Thể loại: {book.CategoryName ?? "Không rõ"}\n";
            if (book.AverageRating > 0)
                context += $"   - Đánh giá: {book.AverageRating:F1}⭐ ({book.RatingCount} lượt)\n";
            context += "\n";
        }

        if (results.Books.Any() && results.FocusedBook == null)
        {
            context += $"📚 TÌM THẤY {results.TotalBooks} SÁCH:\n";
            foreach (var book in results.Books.Take(5))
            {
                context += $"   - '{book.Title}' - {book.AuthorName ?? "?"} ({book.CategoryName ?? "?"})\n";
            }
            context += "\n";
        }

        if (results.AuthorBooks.Any())
        {
            context += $"✍️ SÁCH CÙNG TÁC GIẢ ({results.AuthorBooks.Count} cuốn):\n";
            foreach (var book in results.AuthorBooks.Take(3))
            {
                context += $"   - '{book.Title}'\n";
            }
            context += "\n";
        }

        if (results.RelatedBooks.Any())
        {
            context += $"🔗 SÁCH CÙNG THỂ LOẠI ({results.RelatedBooks.Count} cuốn):\n";
            foreach (var book in results.RelatedBooks.Take(3))
            {
                context += $"   - '{book.Title}' - {book.AuthorName ?? "?"}\n";
            }
            context += "\n";
        }

        if (results.TopRatedBooks.Any())
        {
            context += $"⭐ TOP SÁCH ĐÁNH GIÁ CAO:\n";
            foreach (var book in results.TopRatedBooks.Take(3))
            {
                context += $"   - '{book.Title}' - {book.AverageRating:F1}⭐\n";
            }
            context += "\n";
        }

        if (results.TrendingBooks.Any())
        {
            context += $"🔥 SÁCH HOT:\n";
            foreach (var book in results.TrendingBooks.Take(3))
            {
                context += $"   - '{book.Title}'\n";
            }
            context += "\n";
        }

        if (results.AllCategories.Any())
        {
            context += $"📑 CÁC THỂ LOẠI: {string.Join(", ", results.AllCategories.Select(c => c.Name).Take(10))}\n";
        }

        if (string.IsNullOrEmpty(context))
        {
            context = "Không tìm thấy dữ liệu phù hợp trong database.\n";
        }

        return context;
    }

    #region Helper Methods

    private bool IsGreeting(string message)
    {
        string[] greetings = { "xin chào", "chào", "hello", "hi", "hey", "hế nhô", "hế lô", "chào bạn", "chào bot" };
        return greetings.Any(g => message.StartsWith(g) || message == g);
    }

    private bool IsFarewell(string message)
    {
        string[] farewells = { "tạm biệt", "bye", "goodbye", "hẹn gặp lại", "thôi", "thoát" };
        return farewells.Any(f => message.Contains(f));
    }

    private bool IsThanking(string message)
    {
        string[] thanks = { "cảm ơn", "cám ơn", "thank", "thanks" };
        return thanks.Any(t => message.Contains(t));
    }

    private string GetGreetingResponse()
    {
        string[] responses = {
            "Xin chào! 👋 Tôi là trợ lý sách của BookInfoFinder. Bạn muốn tìm sách gì hôm nay?",
            "Chào bạn! 😊 Hãy cho tôi biết bạn thích đọc sách gì, tôi sẽ gợi ý cho bạn nhé!",
            "Hello! 📚 Tôi có thể giúp bạn khám phá thế giới sách. Bạn muốn tìm gì?"
        };
        return responses[new Random().Next(responses.Length)];
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
                    temperature = 0.8,
                    maxOutputTokens = 600,
                    topP = 0.95,
                    topK = 40
                }
            };

            Console.WriteLine($"Calling Gemini API with key: {key.Substring(0, 10)}...");
            var response = await client.PostAsJsonAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={key}", 
                payload
            );

            Console.WriteLine($"Gemini API Response Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                
                var text = result
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();
                
                Console.WriteLine($"Gemini API Success: Response length = {text?.Length ?? 0}");
                return text ?? "Xin lỗi, tôi không thể xử lý câu trả lời lúc này.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Gemini API Error Response: {errorContent}");
                return "Xin lỗi, không thể kết nối với AI lúc này. 🤖";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Gemini API Exception: {ex.Message}");
            return "Xin lỗi, không thể kết nối với AI lúc này. 🤖";
        }
    }

    #endregion
}

#region Supporting Classes

public class ConversationContext
{
    private readonly List<ChatMessage> _messages = new();
    private readonly int _maxMessages = 20;
    public int MessageCount => _messages.Count;

    public void AddMessage(string role, string content)
    {
        _messages.Add(new ChatMessage 
        { 
            Role = role, 
            Content = content, 
            Timestamp = DateTime.UtcNow 
        });
        
        // Giữ tối đa N tin nhắn
        if (_messages.Count > _maxMessages)
        {
            _messages.RemoveAt(0);
        }
    }

    public string GetFormattedHistory(int count = 5)
    {
        var recent = _messages.TakeLast(count).ToList();
        if (!recent.Any()) return "Chưa có lịch sử hội thoại.";
        
        return string.Join("\n", recent.Select(m => 
            $"{(m.Role == "user" ? "Người dùng" : "Trợ lý")}: {m.Content}"));
    }

    public void Clear()
    {
        _messages.Clear();
    }
}

public class ChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public class IntentAnalysis
{
    public string Intent { get; set; } = "";
    public string QueryType { get; set; } = "";
    public SearchParams SearchParams { get; set; } = new();
    public string? ContextReference { get; set; }
    public string Explanation { get; set; } = "";
}

public class SearchParams
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Category { get; set; }
    public string? BookMentioned { get; set; }
}

public class DatabaseResults
{
    public List<BookListDto> Books { get; set; } = new();
    public int TotalBooks { get; set; }
    public BookListDto? FocusedBook { get; set; }
    public List<BookListDto> RelatedBooks { get; set; } = new();
    public List<BookListDto> AuthorBooks { get; set; } = new();
    public List<BookListDto> TopRatedBooks { get; set; } = new();
    public List<BookListDto> TrendingBooks { get; set; } = new();
    public List<CategoryDto> AllCategories { get; set; } = new();
}

#endregion