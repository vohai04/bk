using BookInfoFinder.Services.Interface;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Data;
using BookInfoFinder.Models.Entity;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services;

public class ChatbotService : IChatbotService
{
    private readonly IBookService _bookService;
    private readonly ICategoryService _categoryService;
    private readonly IAuthorService _authorService;
    private readonly ITagService _tagService;
    private readonly BookContext _db;
    private readonly GeminiClient _gemini;
    private readonly ILogger<ChatbotService> _logger;

    public ChatbotService(
        IBookService bookService,
        ICategoryService categoryService,
        IAuthorService authorService,
        ITagService tagService,
        BookContext db,
        GeminiClient gemini,
        ILogger<ChatbotService> logger)
    {
        _bookService = bookService;
        _categoryService = categoryService;
        _authorService = authorService;
        _tagService = tagService;
        _db = db;
        _gemini = gemini;
        _logger = logger;
    }

    // ==================== MAIN ENTRY POINT ====================
    public async Task<string> GetChatbotReplyAsync(string message, string? sessionId)
    {
        try
        {
            // Lưu tin nhắn của user
            if (!string.IsNullOrEmpty(sessionId))
            {
                await AddMessageAsync(new ChatbotDto
                {
                    SessionId = sessionId,
                    Role = "user",
                    Message = message,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Xử lý với AI
            var response = await ProcessWithGeminiAI(message, sessionId);

            // Lưu phản hồi của bot
            if (!string.IsNullOrEmpty(sessionId))
            {
                await AddMessageAsync(new ChatbotDto
                {
                    SessionId = sessionId,
                    Role = "assistant",
                    Message = response,
                    CreatedAt = DateTime.UtcNow
                });
            }

            return response;
        }
        catch (Exception)
        {
            return "😔 Xin lỗi, đã có lỗi xảy ra. Vui lòng thử lại.";
        }
    }

    // ==================== AI PROCESSING ====================
    private async Task<string> ProcessWithGeminiAI(string userMessage, string? sessionId)
    {
        // Bước 1: Lấy context từ database
        var dbContext = await BuildDatabaseContext();

        // Bước 2: Lấy lịch sử hội thoại (nếu có)
        var conversationHistory = "";
        if (!string.IsNullOrEmpty(sessionId))
        {
            var history = await GetHistoryAsync(sessionId);
            var recentMessages = history.OrderByDescending(h => h.CreatedAt).Take(5).ToList();

            if (recentMessages.Any())
            {
                conversationHistory = "\n=== LỊCH SỬ HỘI THOẠI GẦN ĐÂY ===\n";
                foreach (var msg in recentMessages.OrderBy(m => m.CreatedAt))
                {
                    conversationHistory += $"{msg.Role}: {msg.Message}\n";
                }
            }
        }

        // Bước 3: Tạo prompt cho AI
        var systemPrompt = BuildSystemPrompt(dbContext, conversationHistory);
        var fullPrompt = $"{systemPrompt}\n\nNgười dùng: {userMessage}";

        // Bước 4: Gọi Gemini API
        return await _gemini.ReplyAsync(fullPrompt);
    }

    // ==================== BUILD DATABASE CONTEXT ====================
    private async Task<string> BuildDatabaseContext()
    {
        var context = new StringBuilder();
        context.AppendLine("=== THÔNG TIN DATABASE ===\n");

        try
        {
            // Thống kê tổng quan
            var books = await _bookService.GetAllBooksAsync();
            var totalBooks = books.Count();
            var totalAuthors = (await _authorService.GetAllAuthorsAsync()).Count();
            var totalCategories = (await _categoryService.GetAllCategoriesAsync()).Count();
            var totalTags = (await _tagService.GetAllTagsAsync()).Count();

            context.AppendLine("📊 THỐNG KÊ:");
            context.AppendLine($"- Sách: {totalBooks}");
            context.AppendLine($"- Tác giả: {totalAuthors}");
            context.AppendLine($"- Thể loại: {totalCategories}");
            context.AppendLine($"- Tags: {totalTags}\n");

            // Danh sách sách (10 cuốn đầu)
            context.AppendLine("📚 MỘT SỐ SÁCH:");
            foreach (var book in books.Take(10))
            {
                context.AppendLine($"- '{book.Title}' - {book.AuthorName} ({book.CategoryName})");
            }
            context.AppendLine();

            // Danh sách tác giả
            var authors = await _authorService.GetAllAuthorsAsync();
            context.AppendLine("👤 MỘT SỐ TÁC GIẢ:");
            foreach (var author in authors.Take(10))
            {
                context.AppendLine($"- {author.Name} ({author.BookCount} sách)");
            }
            context.AppendLine();

            // Danh sách thể loại
            var categories = await _categoryService.GetAllCategoriesAsync();
            context.AppendLine("📖 THỂ LOẠI:");
            foreach (var category in categories)
            {
                context.AppendLine($"- {category.Name} ({category.BookCount} sách)");
            }
            context.AppendLine();

            // Tags
            var tags = await _tagService.GetAllTagsAsync();
            context.AppendLine($"🏷️ TAGS: {string.Join(", ", tags.Take(20).Select(t => t.Name))}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Unable to build chatbot catalog context: {ErrorType}", ex.GetType().Name);
            context.AppendLine("Danh mục sách hiện không khả dụng. Hãy báo người dùng thử lại sau.");
        }

        return context.ToString();
    }

    // ==================== BUILD SYSTEM PROMPT ====================
    private string BuildSystemPrompt(string dbContext, string conversationHistory)
    {
        return $@"Bạn là trợ lý AI thông minh cho hệ thống quản lý sách. 

{dbContext}

{conversationHistory}

NHIỆM VỤ:
1. Trả lời câu hỏi người dùng dựa trên thông tin database ở trên
2. Hiểu ngôn ngữ tự nhiên tiếng Việt (nhiều cách hỏi khác nhau)
3. Trả lời ngắn gọn, chính xác, thân thiện
4. Nếu không tìm thấy thông tin, nói rõ và gợi ý

CÁC LOẠI CÂU HỎI THƯỜNG GẶP:
- Thống kê: ""Có bao nhiêu sách?"", ""Tổng số tác giả?"", ""Số lượng thể loại?""
- Tìm kiếm: ""Sách về AI"", ""Sách của Nguyễn Nhật Ánh"", ""Thể loại Fantasy""
- Chi tiết: ""Chi tiết sách Harry Potter"", ""Thông tin tác giả..."", ""Mô tả thể loại...""
- Đề xuất: ""Sách hay nhất"", ""Sách được yêu thích"", ""Sách xu hướng""
- Chào hỏi: ""Xin chào"", ""Hello"", ""Hi""

QUY TẮC TRẢ LỜI:
- Với câu hỏi về số lượng/thống kê: Trả lời trực tiếp con số từ DATABASE CONTEXT
- Với câu hỏi liệt kê: Chỉ liệt kê 3-5 items nếu có nhiều
- Với câu hỏi tìm kiếm: Tìm trong DATABASE CONTEXT và trả về kết quả phù hợp nhất
- Với câu chào: Chào lại thân thiện và giới thiệu bản thân
- Nếu không chắc chắn: Nói rõ và hỏi lại người dùng

ĐỊNH DẠNG TRẢ LỜI:
- Sử dụng emoji phù hợp (📚 🔍 ✨ 👤 📊)
- Ngắn gọn, dễ đọc
- Không dùng markdown phức tạp
- Chỉ liệt kê thông tin CÓ TRONG DATABASE CONTEXT

LƯU Ý QUAN TRỌNG:
- TUYỆT ĐỐI chỉ trả lời dựa trên DATABASE CONTEXT ở trên
- KHÔNG bịa thông tin không có trong context
- Nếu không tìm thấy, nói: ""Không tìm thấy [thông tin] trong hệ thống""";
    }

    // ==================== HISTORY MANAGEMENT ====================
    public async Task<List<ChatbotDto>> GetHistoryAsync(string sessionId)
    {
        try
        {
            return await _db.Chatbots
                .Where(c => c.SessionId == sessionId)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new ChatbotDto
                {
                    ChatbotId = c.ChatbotId,
                    SessionId = c.SessionId,
                    Role = c.Role,
                    Message = c.Message,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();
        }
        catch (Exception)
        {
            return new List<ChatbotDto>();
        }
    }

    public async Task AddMessageAsync(ChatbotDto message)
    {
        try
        {
            var chatbotMessage = new Chatbot
            {
                SessionId = message.SessionId,
                Role = message.Role,
                Message = message.Message,
                CreatedAt = message.CreatedAt
            };

            _db.Chatbots.Add(chatbotMessage);
            await _db.SaveChangesAsync();
        }
        catch (Exception)
        {
        }
    }
}