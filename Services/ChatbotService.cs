using Microsoft.EntityFrameworkCore;
using BookInfoFinder.Services.Interface;
using BookInfoFinder.Data;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Collections.Concurrent;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services;

public class ChatbotService : IChatbotService
{
    private readonly IBookService _bookService;
    private readonly IRatingService _ratingService;
    private readonly IFavoriteService _favoriteService;
    private readonly ICategoryService _categoryService;
    private readonly IAuthorService _authorService;
    private readonly IConfiguration _config;
    private readonly BookContext _db;
    private readonly string? _geminiApiKey;
    private readonly string? _geminiModel;

    public ChatbotService(IBookService bookService, IRatingService ratingService, 
        IFavoriteService favoriteService, ICategoryService categoryService, IAuthorService authorService, IConfiguration config, BookContext db)
    {
        _bookService = bookService;
        _ratingService = ratingService;
        _favoriteService = favoriteService;
        _categoryService = categoryService;
        _authorService = authorService;
        _config = config;
        _db = db;
        _geminiApiKey = _config["AI:GEMINI:ApiKey"];
        _geminiModel = _config["AI:GEMINI:Model"] ?? "models/text-bison-001";
        Console.WriteLine($"Gemini API Key: {(_geminiApiKey != null ? "✅ LOADED" : "❌ NULL")}, model={_geminiModel}");
    }

    // Similarity helpers
    private static double Similarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;
        var na = Normalize(a);
        var nb = Normalize(b);
        var lev = LevenshteinDistance(na, nb);
        var max = Math.Max(na.Length, nb.Length);
        if (max == 0) return 0.0;
        return 1.0 - (double)lev / max;
    }
    private static int LevenshteinDistance(string s, string t)
    {
        if (s == t) return 0;
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    private static bool TokenMatches(string term, string text)
    {
        if (string.IsNullOrEmpty(term) || string.IsNullOrEmpty(text)) return false;
        var qTokens = term.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => Normalize(t)).Where(t => t.Length > 0).ToArray();
        var words = System.Text.RegularExpressions.Regex.Split(Normalize(text), "[^a-z0-9]+")
            .Select(w => w.Trim()).Where(w => w.Length > 0).ToArray();
        foreach (var q in qTokens)
        {
            foreach (var w in words)
            {
                if (w.Contains(q) || q.Contains(w)) return true;
                var sim = Similarity(q, w);
                if (sim >= 0.65) return true; // slightly more tolerant
            }
        }
        return false;
    }

    private static bool TokenNearMatch(string term, string text)
    {
        if (string.IsNullOrEmpty(term) || string.IsNullOrEmpty(text)) return false;
        var qTokens = term.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => Normalize(t)).Where(t => t.Length > 0).ToArray();
        var words = System.Text.RegularExpressions.Regex.Split(Normalize(text), "[^a-z0-9]+")
            .Select(w => w.Trim()).Where(w => w.Length > 0).ToArray();
        foreach (var q in qTokens)
        {
            var matched = false;
            foreach (var w in words)
            {
                if (w.Equals(q)) { matched = true; break; }
                var lev = LevenshteinDistance(q, w);
                // allow up to 2 edits for short tokens, proportionally for longer tokens
                var allowed = Math.Max(1, (int)Math.Ceiling(Math.Min(q.Length, w.Length) * 0.25));
                if (lev <= allowed) { matched = true; break; }
                var sim = Similarity(q, w);
                if (sim >= 0.75) { matched = true; break; }
            }
            if (!matched) return false;
        }
        return true;
    }

    // Normalize text: lower, remove diacritics, collapse whitespace, remove extra punctuation
    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var normalized = s.ToLowerInvariant();
        // A reliable approach: use string normalization and remove combining diacritic marks
        normalized = normalized.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var ch in normalized)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        normalized = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);

        // remove punctuation except alphanumerics and spaces
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, "[^a-z0-9\\s]+", " ");
        // collapse spaces
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, "\\s+", " ").Trim();
        return normalized;
    }

    public async Task<string> GetChatbotReplyAsync(string message)
    {
    return await GetChatbotReplyAsync(message, null);
    }

    public async Task<string> GetChatbotReplyAsync(string message, string? sessionId = null)
    {
        sessionId ??= "default";

        // DEBUG: Log incoming message
        Console.WriteLine($"\n========== NEW MESSAGE ==========");
        Console.WriteLine($"Session: {sessionId}");
        Console.WriteLine($"User: {message}");
        Console.WriteLine($"=================================\n");

        if (string.IsNullOrWhiteSpace(message))
        {
            return "Xin chào! Tôi là trợ lý tư vấn sách của BookInfoFinder. Bạn có thể hỏi tôi về sách, tác giả, thể loại hoặc nhờ tôi gợi ý sách hay.";
        }
        try
        {
            // Lưu message user vào database
            await AddMessageAsync(new Models.Dto.ChatbotDto
            {
                SessionId = sessionId,
                Role = "user",
                Message = message,
                CreatedAt = DateTime.UtcNow
            });

            string lowerMessage = message.ToLower().Trim();
            var history = await GetHistoryAsync(sessionId);
            var context = new ConversationContext();
            foreach (var msg in history)
            {
                context.AddMessage(msg.Role ?? "user", msg.Message ?? string.Empty);
            }

            // Nếu người dùng rõ ràng yêu cầu 'top' / 'yêu thích' / 'iu thích' -> trả về ngay từ DB
            if (IsFavoriteQuery(lowerMessage))
            {
                var favReply = await HandleLocalConversation(message, context);
                var safeFav = favReply ?? "Xin lỗi, mình không có dữ liệu phù hợp.";
                await AddMessageAsync(new Models.Dto.ChatbotDto
                {
                    SessionId = sessionId,
                    Role = "assistant",
                    Message = safeFav,
                    CreatedAt = DateTime.UtcNow
                });
                return safeFav;
            }

            // Xử lý chào hỏi cơ bản
            if (IsGreeting(lowerMessage) && context.MessageCount <= 1)
            {
                var greeting = GetGreetingResponse();
                await AddMessageAsync(new Models.Dto.ChatbotDto
                {
                    SessionId = sessionId,
                    Role = "assistant",
                    Message = greeting ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                });
                var safeGreeting = greeting ?? "Xin chào!";
                context.AddMessage("assistant", safeGreeting);
                return safeGreeting;
            }
            if (IsFarewell(lowerMessage))
            {
                var farewell = "Tạm biệt! Hẹn gặp lại bạn. Chúc bạn tìm được những cuốn sách hay!";
                context.Clear();
                await AddMessageAsync(new Models.Dto.ChatbotDto
                {
                    SessionId = sessionId,
                    Role = "assistant",
                    Message = farewell ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                });
                var safeFarewell = farewell ?? "Tạm biệt!";
                return safeFarewell;
            }
            if (IsThanking(lowerMessage))
            {
                var thanks = "Không có gì! Rất vui được giúp bạn. Nếu cần thêm tư vấn về sách, cứ hỏi tôi nhé!";
                await AddMessageAsync(new Models.Dto.ChatbotDto
                {
                    SessionId = sessionId,
                    Role = "assistant",
                    Message = thanks ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                });
                var safeThanks = thanks ?? "Cảm ơn!";
                return safeThanks;
            }
            // First: local analysis + DB query to avoid fabrication
            var localAnalysis = LocalIntentAnalysis(message, context);
            var preDbResults = await QueryDatabase(localAnalysis);

            // If DB has any meaningful result, return a deterministic DB-backed reply
            bool hasData = (preDbResults.Books != null && preDbResults.Books.Any())
                || (preDbResults.TopRatedBooks != null && preDbResults.TopRatedBooks.Any())
                || (preDbResults.TrendingBooks != null && preDbResults.TrendingBooks.Any())
                || (preDbResults.AllCategories != null && preDbResults.AllCategories.Any());

                string? reply = null;
            if (hasData)
            {
                reply = await GenerateLocalResponse(message, context, preDbResults, localAnalysis);
            }
            else
            {
                // No DB results -> prefer AI to analyze/normalize into structured search params
                if (!string.IsNullOrEmpty(_geminiApiKey))
                {
                    try
                    {
                        // Ask AI to produce structured intent/search params
                        var aiAnalysis = await AnalyzeUserIntentWithAI(message, context);
                        var aiDbResults = await QueryDatabase(aiAnalysis);

                        var hasAiData = (aiDbResults.Books != null && aiDbResults.Books.Any())
                            || (aiDbResults.TopRatedBooks != null && aiDbResults.TopRatedBooks.Any())
                            || (aiDbResults.TrendingBooks != null && aiDbResults.TrendingBooks.Any());

                        if (hasAiData)
                        {
                            reply = await GenerateLocalResponse(message, context, aiDbResults, aiAnalysis);
                        }
                        else
                        {
                            // If still no results, ask AI to normalize (spell-correct / extract title/author/category)
                            try
                            {
                                var normalizePrompt =
                                    "Bạn là một trợ lý giúp chuẩn hóa cụm tìm kiếm sách để truy vấn database.\n\n" +
                                    "LỊCH SỬ HỘI THOẠI:\n" + context.GetFormattedHistory(5) + "\n\n" +
                                    "TIN NHẮN MỚI: \"" + message + "\"\n\n" +
                                    "NHIỆM VỤ: Trả về một JSON có các field 'title','author','category' (hoặc null nếu không có). Chỉ trả về JSON thuần, ví dụ: {\"title\":\"Harry Potter\",\"author\":null,\"category\":null}";

                                var normResp = await CallGeminiAI(normalizePrompt);
                                Console.WriteLine($"Normalization AI raw response: {normResp}");
                                var jsonStart = normResp.IndexOf('{');
                                var jsonEnd = normResp.LastIndexOf('}');
                                if (jsonStart >= 0 && jsonEnd > jsonStart)
                                {
                                    var json = normResp.Substring(jsonStart, jsonEnd - jsonStart + 1);
                                    try
                                    {
                                        var normalized = JsonSerializer.Deserialize<SearchParams>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                        if (normalized != null)
                                        {
                                            aiAnalysis.SearchParams = aiAnalysis.SearchParams ?? new SearchParams();
                                            if (!string.IsNullOrEmpty(normalized.Title)) aiAnalysis.SearchParams.Title = normalized.Title;
                                            if (!string.IsNullOrEmpty(normalized.Author)) aiAnalysis.SearchParams.Author = normalized.Author;
                                            if (!string.IsNullOrEmpty(normalized.Category)) aiAnalysis.SearchParams.Category = normalized.Category;

                                            var secondTry = await QueryDatabase(aiAnalysis);
                                            var hasSecond = (secondTry.Books != null && secondTry.Books.Any())
                                                || (secondTry.TopRatedBooks != null && secondTry.TopRatedBooks.Any())
                                                || (secondTry.TrendingBooks != null && secondTry.TrendingBooks.Any());

                                            if (hasSecond)
                                            {
                                                reply = await GenerateLocalResponse(message, context, secondTry, aiAnalysis);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Normalize parse error: {ex.Message}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Normalization AI error: {ex.Message}");
                            }
                        }

                        // If still empty after AI normalization, finally ask AI to generate a user-facing reply
                        if (string.IsNullOrEmpty(reply))
                        {
                            var aiFinal = await GenerateResponse(message, context, preDbResults);
                            reply = aiFinal;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"AI flow failed, fallback to local. Error: {ex.Message}");
                        reply = await HandleLocalConversation(message, context);
                    }
                }
                else
                {
                    reply = await HandleLocalConversation(message, context);
                }
            }

            var safeReply = reply ?? "Xin lỗi, mình không thể trả lời ngay bây giờ.";
            await AddMessageAsync(new Models.Dto.ChatbotDto
            {
                SessionId = sessionId,
                Role = "assistant",
                Message = safeReply,
                CreatedAt = DateTime.UtcNow
            });

            return safeReply;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chatbot Error: {ex.Message}");
            return "Xin lỗi, có lỗi xảy ra. Bạn có thể thử hỏi theo cách khác hoặc tìm kiếm trực tiếp trên trang nhé!";
        }
    }
    public async Task<List<Models.Dto.ChatbotDto>> GetHistoryAsync(string sessionId)
    {
        var messages = await _db.Chatbots
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        return messages.Select(m => new Models.Dto.ChatbotDto
        {
            ChatbotId = m.ChatbotId,
            SessionId = m.SessionId,
            Role = m.Role,
            Message = m.Message,
            CreatedAt = m.CreatedAt
        }).ToList();
    }

    public async Task AddMessageAsync(Models.Dto.ChatbotDto dto)
    {
        var entity = new Models.Entity.Chatbot
        {
            SessionId = dto.SessionId,
            Role = dto.Role,
            Message = dto.Message,
            CreatedAt = dto.CreatedAt
        };
        _db.Chatbots.Add(entity);
        await _db.SaveChangesAsync();
    }


    private async Task<string> HandleAIConversation(string userMessage, ConversationContext context)
    {
        // Bước 1: AI phân tích ý định và truy vấn database
        var analysisResult = await AnalyzeUserIntentWithAI(userMessage, context);
        
        // Bước 2: Thực hiện truy vấn database dựa trên phân tích
        var databaseResults = await QueryDatabase(analysisResult);
        
        // Nếu database không trả về dữ liệu phù hợp, KHÔNG gọi AI để tránh AI bịa đặt
        var hasAnyData = (databaseResults.Books.Any() || databaseResults.TopRatedBooks.Any() || databaseResults.TrendingBooks.Any() || databaseResults.AllCategories.Any());
        if (!hasAnyData)
        {
            // trả về thông điệp tôn trọng nguyên tắc: chỉ dùng dữ liệu từ database
            var fallback = "Xin lỗi, hiện tại mình không tìm thấy thông tin phù hợp trong cơ sở dữ liệu. Mình chỉ có thể trả lời dựa trên dữ liệu nội bộ. Bạn có thể thử nhập tên sách, tác giả hoặc thể loại khác.";
            context.AddMessage("assistant", fallback);
            return fallback;
        }

        // Bước 3: AI tạo câu trả lời tự nhiên dựa trên kết quả
        var response = await GenerateResponse(userMessage, context, databaseResults);

        // Nếu DB có dữ liệu, đảm bảo AI trả lời có nhắc tới ít nhất một tựa sách/tài liệu trong DB; nếu không, fallback
        try
        {
            var knownTitles = new List<string>();
            knownTitles.AddRange(databaseResults.Books.Select(b => b.Title ?? string.Empty));
            knownTitles.AddRange(databaseResults.TopRatedBooks.Select(b => b.Title ?? string.Empty));
            knownTitles.AddRange(databaseResults.TrendingBooks.Select(b => b.Title ?? string.Empty));
            var responseLower = (response ?? string.Empty).ToLowerInvariant();
            var matched = knownTitles.Any(t => !string.IsNullOrEmpty(t) && responseLower.Contains(t.ToLowerInvariant()));
            if (!matched)
            {
                Console.WriteLine("AI response did not reference any DB-known title; falling back to local deterministic response.");
                var local = await GenerateLocalResponse(userMessage, context, databaseResults, analysisResult);
                context.AddMessage("assistant", local);
                return local;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Validation error on AI response: {ex.Message}");
        }

    var safeResponse = response ?? "Xin lỗi, mình không có dữ liệu phù hợp.";
    context.AddMessage("assistant", safeResponse);
    return safeResponse;
    }

    // Local fallback: nếu không có AI hoặc AI lỗi thì dùng phân tích đơn giản + DB để trả lời
    private async Task<string> HandleLocalConversation(string userMessage, ConversationContext context)
    {
        var analysis = LocalIntentAnalysis(userMessage, context);
        var dbResults = await QueryDatabase(analysis);
    var reply = await GenerateLocalResponse(userMessage, context, dbResults, analysis);
    context.AddMessage("assistant", reply);
    return reply;
    }

    private IntentAnalysis LocalIntentAnalysis(string message, ConversationContext context)
    {
        var lower = Normalize(message);
        var analysis = new IntentAnalysis();

        // Quick follow-up detection: user may reply with a title from the assistant's last list
        try
        {
            var lastAssist = context.GetLastAssistantMessage();
            if (!string.IsNullOrEmpty(lastAssist))
            {
                // extract potential titles from assistant message: look for quoted titles or comma-separated list
                var candidates = new List<string>();
                var qstart = lastAssist.IndexOf('"');
                if (qstart >= 0)
                {
                    var qend = lastAssist.LastIndexOf('"');
                    if (qend > qstart)
                    {
                        var quoted = lastAssist.Substring(qstart + 1, qend - qstart - 1);
                        if (!string.IsNullOrWhiteSpace(quoted)) candidates.Add(quoted.Trim());
                    }
                }
                // also split by common separators
                var parts = lastAssist.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 2).ToList();
                candidates.AddRange(parts);

                // now if user message is short (likely a selection) try to match
                var words = message.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length <= 8 && candidates.Any())
                {
                    foreach (var cand in candidates)
                    {
                        if (string.IsNullOrEmpty(cand)) continue;
                        if (TokenMatches(cand, message) || TokenNearMatch(cand, message) || Similarity(Normalize(cand), Normalize(message)) > 0.7)
                        {
                            analysis.Intent = "ask_about_book";
                            analysis.SearchParams.Title = cand;
                            analysis.Explanation = "User selected a title from previous list";
                            return analysis;
                        }
                    }
                }
            }
        }
        catch { }

        // Count queries (books / authors / categories)
        var questionWords = new[] { "bao nhieu", "mấy", "may", "co may", "co bao nhieu", "có mấy", "có bao nhiêu", "tong", "tong so", "so luong" };
        var bookKeywords = new[] { "sach", "sách", "book" };
        var authorKeywords = new[] { "tac gia", "tác giả", "tacgia", "tac_gia", "author" };
        var categoryKeywords = new[] { "the loai", "thể loại", "theloai", "the_loai", "the-loai", "category" };

        bool IsQuestionAbout(string[] targets)
        {
            return targets.Any(t => lower.Contains(Normalize(t))) && questionWords.Any(q => lower.Contains(Normalize(q)) || TokenMatches(q, lower));
        }

        if (IsQuestionAbout(bookKeywords))
        {
            analysis.Intent = "count_books";
            analysis.QueryType = "count";
            analysis.Explanation = "User asks total book count";
            return analysis;
        }
        if (IsQuestionAbout(authorKeywords))
        {
            analysis.Intent = "count_authors";
            analysis.QueryType = "count";
            analysis.Explanation = "User asks total author count";
            return analysis;
        }
        if (IsQuestionAbout(categoryKeywords))
        {
            analysis.Intent = "count_categories";
            analysis.QueryType = "count";
            analysis.Explanation = "User asks total category count";
            return analysis;
        }

        // Top / recommendations
        if (lower.Contains("top") || lower.Contains("yeu thich") || lower.Contains("pho bien") || lower.Contains("duoc nhieu") || lower.Contains("iu thich") || lower.Contains("iu"))
        {
            analysis.Intent = "recommend";
            analysis.QueryType = "rating|favorite";
        }

        // Details
        if (lower.Contains("xem chi tiet") || lower.Contains("xem cuon") || lower.Contains("chi tiet") || TokenMatches("xem", lower))
        {
            analysis.Intent = "ask_about_book";
            var keywords = ExtractKeywordsFromMessage(message, context);
            if (!string.IsNullOrEmpty(keywords?.Title)) analysis.SearchParams.Title = keywords.Title;
            if (!string.IsNullOrEmpty(keywords?.Author)) analysis.SearchParams.Author = keywords.Author;
            if (!string.IsNullOrEmpty(keywords?.Category)) analysis.SearchParams.Category = keywords.Category;
        }
        else if (lower.Contains("tim") || lower.Contains("tim kiem") || lower.Contains("tìm") || TokenMatches("tim", lower))
        {
            analysis.Intent = "search_book";
            analysis.QueryType = "mixed";
        }
        else if (lower.Contains("goi y") || lower.Contains("goi y sach") || lower.Contains("recommend") || TokenMatches("goi y", lower))
        {
            analysis.Intent = "recommend";
            analysis.QueryType = "mixed";
        }

        // List / enumerate
        var listKeywords = new[] { "liet ke", "liệt kê", "liệtke", "lietke", "liệt kê ra", "liệt kê giúp", "liệt kê cho", "list", "liệt kê hộ", "liệt kê nhé" };
        if (listKeywords.Any(k => lower.Contains(Normalize(k))))
        {
            analysis.Intent = "list";

            // Decide whether user wants books specifically
            bool userMentionedBooks = lower.Contains("sach") || lower.Contains("sách") || TokenMatches("sach", lower);
            var lastAssist = context.GetLastAssistantMessage();
            bool lastMentionedBooksOrCount = false;
            if (!string.IsNullOrEmpty(lastAssist))
            {
                var nLast = Normalize(lastAssist);
                lastMentionedBooksOrCount = nLast.Contains("sach") || nLast.Contains("sách") || System.Text.RegularExpressions.Regex.IsMatch(lastAssist, "\\d{1,6}");
            }

            if (userMentionedBooks || lastMentionedBooksOrCount)
            {
                analysis.QueryType = "list_books";
            }
            else
            {
                analysis.QueryType = "list";
            }

            analysis.Explanation = "User asks to list previously suggested items or search results";

            // If list_books, try to infer category and counts
            if (analysis.QueryType == "list_books")
            {
                try
                {
                    if (!string.IsNullOrEmpty(lastAssist))
                    {
                        var cats = _categoryService.GetAllCategoriesAsync().GetAwaiter().GetResult();
                        if (cats != null && cats.Any())
                        {
                            foreach (var c in cats)
                            {
                                if (TokenMatches(c.Name ?? string.Empty, lastAssist) || TokenMatches(c.Name ?? string.Empty, lower))
                                {
                                    analysis.SearchParams.Category = c.Name;
                                    break;
                                }
                            }
                        }

                        // detect count in last assistant message
                        var cnt = ExtractNumberFromText(lastAssist);
                        if (cnt.HasValue)
                        {
                            analysis.ContextReference = $"count:{cnt.Value}";
                        }
                    }

                    // also check the user's message for an explicit count: "liệt kê 23 sách"
                    var userCnt = ExtractNumberFromText(message);
                    if (userCnt.HasValue)
                    {
                        analysis.ContextReference = $"count:{userCnt.Value}";
                    }
                }
                catch { }

            }

            return analysis;
        }

        // If none of the above matched, apply simple heuristics
        // check for 'của' to get author
        if (lower.Contains(" cua ") || lower.Contains(" cua:") || lower.Contains(" cua"))
        {
            var idx = lower.IndexOf(" cua ");
            if (idx >= 0)
            {
                var after = message.Substring(idx + 4).Trim();
                analysis.SearchParams.Author = after.Split(new[] { ',', '.' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            }
        }

        // category heuristics
        var knownCats = new[] { "fantasy", "science fiction", "literary fiction", "dystopian", "horror", "romance", "biography", "thriller", "mystery", "sci-fi", "science", "khoa hoc vien tuong", "vien tuong", "van hoc", "dystopia", "lang man" };
        foreach (var c in knownCats)
        {
            if (lower.Contains(c))
            {
                analysis.SearchParams.Category = CultureInfoInvariant(c);
                break;
            }
        }

        if (string.IsNullOrEmpty(analysis.SearchParams.Title) && string.IsNullOrEmpty(analysis.SearchParams.Author) && string.IsNullOrEmpty(analysis.SearchParams.Category))
        {
            analysis.SearchParams.Title = message;
        }

        analysis.Explanation = string.IsNullOrEmpty(analysis.Explanation) ? "Local heuristic analysis" : analysis.Explanation;
        return analysis;
    }

    private string ExtractQuoted(string s)
    {
        if (string.IsNullOrEmpty(s)) return null!;
        var start = s.IndexOf('"');
        var end = s.LastIndexOf('"');
        if (start >= 0 && end > start)
        {
            return s.Substring(start + 1, end - start - 1);
        }
        return null!;
    }

    private string CultureInfoInvariant(string s) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s);

    private int? ExtractNumberFromText(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        // look for numbers in text, including digits and spelled-out small numbers
        var m = System.Text.RegularExpressions.Regex.Match(text, "(\\d{1,6})");
        if (m.Success)
        {
            if (int.TryParse(m.Value, out var v)) return v;
        }
        // fallback: match Vietnamese words for small numbers (một, hai, ba, tư, năm, sáu...)
        var spelled = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            {"mot",1},{"một",1},{"hai",2},{"ba",3},{"bon",4},{"bốn",4},{"nam",5},{"năm",5},{"sau",6},{"sáu",6},{"bay",7},{"bảy",7},{"tam",8},{"tám",8},{"chin",9},{"chín",9},{"muoi",10},{"mười",10}
        };
        var norm = Normalize(text);
        foreach (var kv in spelled)
        {
            if (norm.Contains(kv.Key)) return kv.Value;
        }
        return null;
    }

    // Try to extract Title/Author/Category from arbitrary user message.
    // First use heuristics (quoted text, patterns like 'của', 'by', 'tác giả', 'thể loại'),
    // then fallback to AI (Gemini) to normalize and extract structured fields.
    private SearchParams ExtractKeywordsFromMessage(string message, ConversationContext context)
    {
        var result = new SearchParams();
        if (string.IsNullOrEmpty(message)) return result;

        // 1) Quoted titles
        var quoted = ExtractQuoted(message);
        if (!string.IsNullOrEmpty(quoted))
        {
            result.Title = quoted.Trim();
            return result;
        }

        var lower = Normalize(message);

        // 2) Patterns like "tác giả: ...", "author: ...", "by ...", "của ..."
        var authorPattern = System.Text.RegularExpressions.Regex.Match(message, "(tác giả|tac gia|author|by)[:\\s]+(?<a>[^,\\.\\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (authorPattern.Success)
        {
            result.Author = authorPattern.Groups["a"].Value.Trim();
        }

        var categoryPattern = System.Text.RegularExpressions.Regex.Match(message, "(thể loại|the loai|category)[:\\s]+(?<c>[^,\\.\\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (categoryPattern.Success)
        {
            result.Category = categoryPattern.Groups["c"].Value.Trim();
        }

        // 3) 'của X' heuristic: take text after ' của ' if present
        var n = lower;
        if (string.IsNullOrEmpty(result.Author) && n.Contains(" cua "))
        {
            var idx = n.IndexOf(" cua ");
            if (idx >= 0)
            {
                var after = message.Substring(idx + 4).Trim();
                // stop at punctuation
                after = after.Split(new[] { ',', '.', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? after;
                if (!string.IsNullOrEmpty(after)) result.Author = after;
            }
        }

        // 4) Titles introduced by words like 'tên', 'tựa', 'cuốn', 'cuon', 'book' etc.
        if (string.IsNullOrEmpty(result.Title))
        {
            var titlePatterns = new[] { "tên" , "tua", "tựa", "tua: ", "tua:", "ten" };
            foreach (var p in titlePatterns)
            {
                if (lower.Contains(p) && lower.IndexOf(p) + p.Length < lower.Length)
                {
                    var idx = lower.IndexOf(p);
                    var after = message.Substring(idx + p.Length).Trim();
                    after = after.Split(new[] { ',', '.', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? after;
                    if (!string.IsNullOrEmpty(after))
                    {
                        result.Title = after;
                        break;
                    }
                }
            }
        }

        // If heuristics found something meaningful, return it
        if (!string.IsNullOrEmpty(result.Title) || !string.IsNullOrEmpty(result.Author) || !string.IsNullOrEmpty(result.Category))
        {
            return result;
        }

        // 5) Long or ambiguous sentence -> ask AI to extract structured fields
        if (!string.IsNullOrEmpty(_geminiApiKey))
        {
            try
            {
                var prompt = BuildExtractionPrompt(message, context);
                var aiResp = CallGeminiAI(prompt).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(aiResp))
                {
                    var jsonStart = aiResp.IndexOf('{');
                    var jsonEnd = aiResp.LastIndexOf('}');
                    if (jsonStart >= 0 && jsonEnd > jsonStart)
                    {
                        var json = aiResp.Substring(jsonStart, jsonEnd - jsonStart + 1);
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<SearchParams>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (parsed != null) return parsed;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Keyword extract JSON parse error: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Keyword extraction AI error: {ex.Message}");
            }
        }

        return result;
    }

    private string BuildExtractionPrompt(string message, ConversationContext context)
    {
        var history = context.GetFormattedHistory(5);
    var prompt = $@"Bạn là một trợ lý chuyên trích xuất thông tin từ câu hỏi người dùng để truy vấn database sách.

LỊCH SỬ HỘI THOẠI:
{history}

TIN NHẮN MỚI: ""{message}""

NHIỆM VỤ: Trích xuất và trả về duy nhất một JSON có các trường: title, author, category.
- Nếu không tìm thấy trường nào, hãy để null.
- Chỉ trả về JSON thuần, ví dụ: {{""title"":""Harry Potter"",""author"":""J.K. Rowling"",""category"":null}}

Ghi chú: câu có thể rất dài và có từ thừa; hãy chỉ rút ra phần liên quan nhất cho tìm sách.";
        return prompt;
    }

    private async Task<string> GenerateLocalResponse(string userMessage, ConversationContext context, DatabaseResults dbResults, IntentAnalysis analysis)
    {
        // Deterministic short replies based on dbResults
        if (analysis.Intent == "count_books")
        {
            try
            {
                // Use book service to get counts if available
                int total = 0;
                try
                {
                    var all = await _bookService.GetAllBooksAsync();
                    total = all?.Count() ?? 0;
                }
                catch
                {
                    // fallback to direct DB if service fails
                    total = _db.Books.Count();
                }
                return $"Hiện tại hệ thống có khoảng {total} sách.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Count books error: {ex.Message}");
                return "Mình không thể lấy được tổng số sách ngay bây giờ, bạn thử lại sau nhé.";
            }
        }
        if (analysis.Intent == "count_authors")
        {
            try
            {
                int totalAuthors = 0;
                try
                {
                    var authors = await _authorService.GetAllAuthorsAsync();
                    totalAuthors = authors?.Count() ?? 0;
                }
                catch
                {
                    totalAuthors = _db.Authors.Count();
                }
                return $"Hiện tại hệ thống có khoảng {totalAuthors} tác giả.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Count authors error: {ex.Message}");
                return "Mình không thể lấy được tổng số tác giả ngay bây giờ, bạn thử lại sau nhé.";
            }
        }

        if (analysis.Intent == "count_categories")
        {
            try
            {
                int totalCats = 0;
                try
                {
                    var cats = await _categoryService.GetAllCategoriesAsync();
                    totalCats = cats?.Count() ?? 0;
                }
                catch
                {
                    totalCats = _db.Categories.Count();
                }
                return $"Hiện tại hệ thống có khoảng {totalCats} thể loại.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Count categories error: {ex.Message}");
                return "Mình không thể lấy được tổng số thể loại ngay bây giờ, bạn thử lại sau nhé.";
            }
        }
        // If user asked for recommendations/top, prefer TopRatedBooks or TrendingBooks
        if ((analysis.Intent == "recommend" || (analysis.QueryType != null && analysis.QueryType.Contains("rating")))
            && dbResults.TopRatedBooks != null && dbResults.TopRatedBooks.Any())
        {
            var top = dbResults.TopRatedBooks.Take(5).ToList();
            var list = string.Join("; ", top.Select(b => $"'{b.Title}' — {b.AuthorName ?? "?"}"));
            return $"Top sách được nhiều người yêu thích: {list}. Bạn muốn xem chi tiết hay không?";
        }

        if ((analysis.Intent == "recommend" || (analysis.QueryType != null && analysis.QueryType.Contains("favorite")))
            && dbResults.TrendingBooks != null && dbResults.TrendingBooks.Any())
        {
            var top = dbResults.TrendingBooks.Take(5).ToList();
            var list = string.Join("; ", top.Select(b => $"'{b.Title}' — {b.AuthorName ?? "?"}"));
            return $"Những sách được nhiều người thêm vào yêu thích: {list}. Bạn muốn xem chi tiết cuốn nào?";
        }

    if (dbResults.Books != null && dbResults.Books.Any())
        {
            var top = dbResults.Books.Take(3).ToList();
            if (analysis.Intent == "ask_about_book" || analysis.Intent == "follow_up")
            {
                var b = dbResults.FocusedBook ?? top.First();
                var sb = new System.Text.StringBuilder();
                sb.Append($"Mình tìm thấy \"{b.Title}\" bởi {b.AuthorName ?? "không rõ"}. ");
                if (!string.IsNullOrEmpty(b.CategoryName)) sb.Append($"Thể loại: {b.CategoryName}. ");
                if (b.AverageRating > 0) sb.Append($"Đánh giá trung bình: {b.AverageRating:F1}⭐ ({b.RatingCount} lượt). ");
                if (!string.IsNullOrEmpty(b.Abstract)) sb.Append($"Tóm tắt: {b.Abstract} ");
                sb.Append("Bạn muốn xem mô tả đầy đủ, đánh giá chi tiết hay các sách liên quan?");
                return sb.ToString();
            }
            if (analysis.Intent == "recommend" || (!string.IsNullOrEmpty(analysis.QueryType) && (analysis.QueryType.Contains("rating") || analysis.QueryType.Contains("favorite"))))
            {
                var list = string.Join("; ", top.Select(b => $"'{b.Title}' ({b.AuthorName})"));
                return $"Những sách được nhiều người yêu thích: {list}. Bạn muốn xem chi tiết cuốn nào?";
            }
            // default list result
            var shortList = string.Join(", ", top.Select(b => b.Title));
            return $"Mình tìm thấy một vài kết quả: {shortList}. Bạn muốn xem chi tiết cuốn nào?";
        }

        // If no books, but categories exist
        if (dbResults.AllCategories != null && dbResults.AllCategories.Any())
        {
            var catSample = string.Join(", ", dbResults.AllCategories.Take(5).Select(c => c.Name));
            return $"Hiện tại mình chưa tìm thấy sách phù hợp, nhưng có các thể loại: {catSample}. Bạn muốn khám phá thể loại nào?";
        }

        // If user explicitly asked to list results or categories
        if (analysis.Intent == "list")
        {
            if (analysis.QueryType == "list_books")
            {
                if (dbResults.Books != null && dbResults.Books.Any())
                {
                    Console.WriteLine("[DEBUG] Generating list response");

                    var count = dbResults.Books.Count;
                    var capped = Math.Min(count, 20);
                    var list = dbResults.Books.Take(capped).Select((b, index) =>
                        $"{index + 1}. '{b.Title}' — {b.AuthorName ?? "Không rõ tác giả"}");
                    var titles = string.Join("\n", list);

                    var response = $"📚 Danh sách {count} sách:\n\n{titles}";

                    if (count > 20)
                    {
                        response += $"\n\n... và {count - 20} sách khác. Bạn muốn xem chi tiết cuốn nào?";
                    }
                    else
                    {
                        response += "\n\nBạn muốn xem chi tiết cuốn nào?";
                    }

                    return response;
                }

                // fallback to top rated or trending
                if (dbResults.TopRatedBooks != null && dbResults.TopRatedBooks.Any())
                {
                    var list = string.Join(", ", dbResults.TopRatedBooks.Take(5).Select(b => b.Title));
                    return $"Hiện tại mình có thể liệt kê các sách được đánh giá cao: {list}. Bạn muốn xem chi tiết cuốn nào?";
                }
                if (dbResults.TrendingBooks != null && dbResults.TrendingBooks.Any())
                {
                    var list = string.Join(", ", dbResults.TrendingBooks.Take(5).Select(b => b.Title));
                    return $"Hiện tại mình có thể liệt kê các sách được nhiều người yêu thích: {list}. Bạn muốn xem chi tiết cuốn nào?";
                }

                // final fallback: sample some books directly from DB (non-persistent query)
                try
                {
                    var sample = _db.Books.Include(b => b.Author).Take(5).ToList();
                    if (sample.Any())
                    {
                        var sampleTitles = string.Join(", ", sample.Select(b => b.Title));
                        return $"Mình liệt kê một vài sách mẫu: {sampleTitles}. Bạn muốn xem chi tiết cuốn nào?";
                    }
                }
                catch { }

                return "Mình hiện chưa có sách để liệt kê. Bạn có thể chỉ định thể loại hoặc từ khóa cụ thể được không?";
            }

            // default list behavior: list categories first, then books
            if (dbResults.AllCategories != null && dbResults.AllCategories.Any())
            {
                var all = string.Join(", ", dbResults.AllCategories.Select(c => c.Name));
                return $"Danh sách thể loại có trong hệ thống: {all}. Bạn muốn xem sách thuộc thể loại nào?";
            }
            if (dbResults.Books != null && dbResults.Books.Any())
            {
                var titles = string.Join(", ", dbResults.Books.Select(b => b.Title));
                return $"Các kết quả tôi tìm thấy: {titles}. Bạn muốn xem chi tiết cuốn nào?";
            }
            return "Mình chưa có mục để liệt kê ngay bây giờ. Bạn có thể hỏi 'liệt kê các thể loại' hoặc tìm một từ khóa cụ thể để mình liệt kê kết quả.";
        }

        return "Xin lỗi, mình không tìm thấy thông tin phù hợp trong cơ sở dữ liệu. Bạn có thể thử nhập tên sách, tác giả hoặc thể loại cụ thể hơn.";
    }

    private async Task<IntentAnalysis> AnalyzeUserIntentWithAI(string message, ConversationContext context)
    {
        string conversationHistory = context.GetFormattedHistory(5);
        
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
            var aiResponse = await CallAI(analysisPrompt);
            
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

            // Special case: user asked to list books generally (e.g., 'liệt kê sách' after bot said there are N books)
            if ((!string.IsNullOrEmpty(analysis.QueryType) && analysis.QueryType.Contains("list_books"))
                && string.IsNullOrEmpty(bookTitle) && string.IsNullOrEmpty(author) && string.IsNullOrEmpty(category))
            {
                try
                {
                    Console.WriteLine("[DEBUG] Handling list_books request");

                    // Lấy số lượng từ context nếu có
                    int? requestedCount = null;
                    if (!string.IsNullOrEmpty(analysis.ContextReference) &&
                        analysis.ContextReference.StartsWith("count:"))
                    {
                        var parts = analysis.ContextReference.Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[1], out var v))
                        {
                            requestedCount = v;
                            Console.WriteLine($"[DEBUG] User wants to list {requestedCount} books");
                        }
                    }

                    // Nếu không có số cụ thể, mặc định lấy 10-20 sách
                    var pageSize = Math.Clamp(requestedCount ?? 20, 1, 100);

                    Console.WriteLine($"[DEBUG] Fetching {pageSize} books from database");

                    var (books, total) = await _bookService.SearchBooksWithStatsPagedAsync(
                        null, null, null, null, 1, pageSize, null);

                    results.Books = books.ToList();
                    results.TotalBooks = total;

                    Console.WriteLine($"[DEBUG] Retrieved {results.Books.Count} books out of {total} total");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] List-books query error: {ex.Message}");
                }
            }

            // Query books by provided params
            if (!string.IsNullOrEmpty(bookTitle) || !string.IsNullOrEmpty(author) || !string.IsNullOrEmpty(category))
            {
                var (books, total) = await _bookService.SearchBooksWithStatsPagedAsync(
                    bookTitle, author, category, null, 1, 10, null);
                
                results.Books = books.ToList();
                results.TotalBooks = total;
            }

            // Nếu hỏi về rating/trending
            if (!string.IsNullOrEmpty(analysis.QueryType) && analysis.QueryType.Contains("rating"))
            {
                var (topBooks, total) = await _ratingService.GetTopRatedBooksPagedAsync(1, 10);
                results.TopRatedBooks = topBooks.ToList();
            }

            if (!string.IsNullOrEmpty(analysis.QueryType) && analysis.QueryType.Contains("favorite"))
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

        // Previously we persisted fallback SearchHistory here. Per new behavior, do NOT write
        // any artificial request records. Instead, if the initial queries returned nothing,
        // perform a relaxed ad-hoc re-query (in-memory / non-persistent) to try to match fuzzy
        // or broader terms produced by AI. This builds and runs the query but does not save
        // any placeholder/search request in the database.

        // If no results so far, try a relaxed secondary search using existing book service
    var secSearchParams = analysis.SearchParams;
    var noResults = !(results.Books.Any() || results.TopRatedBooks.Any() || results.TrendingBooks.Any());
        if (noResults && secSearchParams != null && (!string.IsNullOrEmpty(secSearchParams.Title) || !string.IsNullOrEmpty(secSearchParams.Author) || !string.IsNullOrEmpty(secSearchParams.Category)))
        {
            try
            {
                // Use the book service to perform another search with the (possibly normalized) params.
                var (books2, total2) = await _bookService.SearchBooksWithStatsPagedAsync(
                    secSearchParams.Title, secSearchParams.Author, secSearchParams.Category, null, 1, 10, null);
                if (books2 != null && books2.Any())
                {
                    results.Books = books2.ToList();
                    results.TotalBooks = total2;
                }
                else
                {
                    // If the service didn't return results, try a direct EF search that also
                    // checks the book Abstract and author name for fuzzy/implicit matches.
                    var term = secSearchParams.Title?.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(term))
                    {
                        // Tokenize the term to build a broader candidate set, then score via Levenshtein-based similarity
                        var tokens = term.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => t.Length > 0).ToArray();
                        if (tokens.Length > 0)
                        {
                            var efQuery = _db.Books
                                .Include(b => b.Author)
                                .Include(b => b.Category)
                                .Where(b => tokens.Any(t => (b.Title != null && EF.Functions.Like(b.Title.ToLower(), $"%{t}%"))
                                                            || (b.Abstract != null && EF.Functions.Like(b.Abstract.ToLower(), $"%{t}%"))
                                                            || (b.Author != null && EF.Functions.Like(b.Author.Name.ToLower(), $"%{t}%"))))
                                .Take(100);

                            var efList = await efQuery.ToListAsync();

                            if (efList.Any())
                            {
                                // Score candidates by similarity to the full term
                                var scored = new List<(Book b, double score)>();
                                foreach (var b in efList)
                                {
                                    var title = (b.Title ?? string.Empty).ToLowerInvariant();
                                    var authorName = (b.Author?.Name ?? string.Empty).ToLowerInvariant();
                                    var abs = (b.Abstract ?? string.Empty).ToLowerInvariant();
                                    var sTitle = Similarity(term, title);
                                    var sAuthor = Similarity(term, authorName);
                                    var sAbs = Similarity(term, abs);
                                    var score = Math.Max(sTitle, Math.Max(sAuthor, sAbs));
                                    // boost if token-level matching detected
                                    if (TokenMatches(term, title) || TokenMatches(term, authorName) || TokenMatches(term, abs))
                                    {
                                        score = Math.Max(score, 0.8);
                                    }
                                    if (TokenNearMatch(term, title) || TokenNearMatch(term, authorName) || TokenNearMatch(term, abs))
                                    {
                                        score = 0.95; // strong match when near-equal tokens
                                    }
                                    scored.Add((b, score));
                                }

                                var matched = scored.Where(x => x.score >= 0.55).OrderByDescending(x => x.score).ToList();
                                if (matched.Any())
                                {
                                    results.Books = matched.Select(x => new BookListDto
                                    {
                                        BookId = x.b.BookId,
                                        Title = x.b.Title,
                                        AuthorName = x.b.Author?.Name ?? string.Empty,
                                        CategoryName = x.b.Category?.Name ?? string.Empty,
                                        Abstract = x.b.Abstract ?? string.Empty,
                                        PublicationDate = x.b.PublicationDate,
                                        ImageBase64 = x.b.ImageBase64 ?? string.Empty
                                    }).ToList();
                                    results.TotalBooks = results.Books.Count;
                                }
                            }
                            else
                            {
                                // Token query found no candidates; broaden to a larger candidate set and score all
                                var allCandidates = await _db.Books
                                    .Include(b => b.Author)
                                    .Include(b => b.Category)
                                    .Take(200)
                                    .ToListAsync();
                                var scoredAll = new List<(Book b, double score)>();
                                foreach (var b in allCandidates)
                                {
                                    var title = (b.Title ?? string.Empty).ToLowerInvariant();
                                    var authorName = (b.Author?.Name ?? string.Empty).ToLowerInvariant();
                                    var abs = (b.Abstract ?? string.Empty).ToLowerInvariant();
                                    var sTitle = Similarity(term, title);
                                    var sAuthor = Similarity(term, authorName);
                                    var sAbs = Similarity(term, abs);
                                    var score = Math.Max(sTitle, Math.Max(sAuthor, sAbs));
                                    if (TokenMatches(term, title) || TokenMatches(term, authorName) || TokenMatches(term, abs))
                                    {
                                        score = Math.Max(score, 0.8);
                                    }
                                    if (TokenNearMatch(term, title) || TokenNearMatch(term, authorName) || TokenNearMatch(term, abs))
                                    {
                                        score = 0.95;
                                    }
                                    scoredAll.Add((b, score));
                                }
                                var matchedAll = scoredAll.Where(x => x.score >= 0.45).OrderByDescending(x => x.score).ToList();
                                if (matchedAll.Any())
                                {
                                    results.Books = matchedAll.Select(x => new BookListDto
                                    {
                                        BookId = x.b.BookId,
                                        Title = x.b.Title,
                                        AuthorName = x.b.Author?.Name ?? string.Empty,
                                        CategoryName = x.b.Category?.Name ?? string.Empty,
                                        Abstract = x.b.Abstract ?? string.Empty,
                                        PublicationDate = x.b.PublicationDate,
                                        ImageBase64 = x.b.ImageBase64 ?? string.Empty
                                    }).ToList();
                                    results.TotalBooks = results.Books.Count;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ad-hoc search error: {ex.Message}");
            }
        }

        return results;
    }

    private async Task<string> GenerateResponse(string userMessage, ConversationContext context, DatabaseResults dbResults)
    {
        string conversationHistory = context.GetFormattedHistory(5);
        string dataContext = BuildDataContext(dbResults);
    string systemPrompt = @"Bạn là trợ lý sách của BookInfoFinder.

QUY TẮC QUAN TRỌNG:
❌ CHỈ ĐƯỢC GỢI Ý, TRẢ LỜI VỀ CÁC SÁCH, TÁC GIẢ, THỂ LOẠI, ĐÁNH GIÁ... CÓ TRONG DỮ LIỆU TỪ DATABASE BÊN DƯỚI. KHÔNG ĐƯỢC BỊA ĐẶT TÊN SÁCH, TÁC GIẢ, THỂ LOẠI HOẶC BẤT KỲ THÔNG TIN NÀO KHÔNG CÓ TRONG DỮ LIỆU NÀY.
❌ Nếu người dùng hỏi về sách mà không có trong danh sách dưới đây, hãy trả lời lịch sự rằng hiện tại chưa có thông tin về sách đó.
❌ Nếu không có dữ liệu phù hợp, hãy trả lời lịch sự rằng hiện tại chưa có thông tin hoặc gợi ý người dùng thử tìm sách khác.

TÍNH CÁCH:
- Thân thiện, nhiệt tình, gần gũi như người bạn
- Nhớ ngữ cảnh cuộc trò chuyện và tiếp tục tự nhiên
- Không lặp lại thông tin đã nói trước đó
- Giọng điệu tự nhiên, không máy móc

KỸ NĂNG:
- Nhớ sách vừa nhắc đến và trả lời câu hỏi tiếp theo về sách đó
- Hiểu câu hỏi mơ hồ như 'sách đó', 'tác giả ấy', 'thể loại gì'
- Gợi ý sách liên quan thông minh (cùng tác giả, cùng thể loại)
- Trả lời ngắn gọn, súc tích (2-4 câu)
- Luôn khuyến khích khám phá thêm

QUY TẮC PHỤ:
- KHÔNG lặp lại thông tin đã nói
- KHÔNG liệt kê dài dòng
- KHÔNG nói 'theo database' hay 'hệ thống'
- Nếu không có dữ liệu, gợi ý thay thế
- Luôn duy trì ngữ cảnh cuộc trò chuyện";

        string prompt = $@"{systemPrompt}

LỊCH SỬ HỘI THOẠI:
{conversationHistory}

DỮ LIỆU TỪ DATABASE:
{dataContext}

NGƯỜI DÙNG VỪA HỎI: ""{userMessage}""

Hãy trả lời tự nhiên, thân thiện và tiếp nối cuộc trò chuyện. Nếu người dùng hỏi về sách vừa nhắc, trả lời trực tiếp mà không cần nhắc lại tên sách.";

        try
        {
            var response = await CallAI(prompt);
            response = response.Trim();
            if (dbResults.Books.Any() && !response.Contains("🔍") && !response.Contains("tìm kiếm"))
            {
                if (new Random().Next(100) < 30)
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
        // Normalize message to be tolerant to accents and extra spaces
        var m = Normalize(message);
        string[] greetings = { "xin chao", "xin chào", "chao", "chào", "hello", "hi", "hey", "hế nhô", "hế lô", "chao ban", "chào bạn", "chao bot", "chào bot", "chao em", "chào em", "chao anh", "chào anh", "chao chi", "chào chị", "hé lô", "helo", "yo", "xin chào mọi người", "chào mọi người" };
        // check startswith or contains short greetings
        foreach (var g in greetings)
        {
            if (m == g) return true;
            if (m.StartsWith(g + " ") || m.StartsWith(g)) return true;
            if (m.Contains(" " + g + " ") || m.Contains(" " + g)) return true;
        }
        return false;
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
            "Hello! 📚 Tôi có thể giúp bạn khám phá thế giới sách. Bạn muốn tìm gì?",
            "Chào bạn, mình ở đây để giúp chọn sách — bạn thích thể loại gì?",
            "Hi! Nếu bạn muốn, cho mình biết một thể loại hoặc một cuốn bạn đã đọc, mình sẽ gợi ý nhé.",
            "Xin chào! Muốn mình gợi ý sách theo tâm trạng hay thể loại nào?"
        };
        return responses[new Random().Next(responses.Length)];
    }

    private bool IsFavoriteQuery(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        string[] favs = { "iu thích", "iu", "yêu thích", "yêu", "thích", "favorite", "top", "phổ biến", "được nhiều người yêu", "đc nhiều người iu" };
        return favs.Any(k => message.Contains(k));
    }

    #endregion

    // Removed Groq integration. Using Gemini generativeContent only.

    #region Gemini API

   public async Task<string> CallGeminiAI(string prompt)
{
    if (string.IsNullOrEmpty(_geminiApiKey))
    {
        Console.WriteLine("Gemini API: No API key configured.");
        return string.Empty;
    }

    try
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        // ✅ ĐÚNG: Payload format cho Gemini API
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
                topK = 40,
                topP = 0.95,
                maxOutputTokens = 2048
            }
        };

        // ✅ ĐÚNG: URL endpoint cho Gemini
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_geminiModel}:generateContent?key={_geminiApiKey}";
        
        var keyPreview = _geminiApiKey.Length > 6 ? _geminiApiKey[..4] + "..." + _geminiApiKey[^2..] : "(key hidden)";
        Console.WriteLine($"Gemini API: calling model={_geminiModel}, keyLoaded=true, keyPreview={keyPreview}");
        Console.WriteLine($"Gemini API: POST {url}");
        Console.WriteLine($"Gemini API: payload preview: {(prompt?.Length > 200 ? prompt.Substring(0, 200).Replace("\n", " ") + "..." : prompt)}");

        var response = await client.PostAsJsonAsync(url, payload);
        
        if (response.IsSuccessStatusCode)
        {
            var respText = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Gemini API Success: status={(int)response.StatusCode}, response length={respText?.Length ?? 0}");
            
            if (string.IsNullOrEmpty(respText)) return string.Empty;
            
            var parsed = JsonSerializer.Deserialize<JsonElement>(respText);
            
            // ✅ ĐÚNG: Parse response format của Gemini
            if (parsed.ValueKind == JsonValueKind.Object && 
                parsed.TryGetProperty("candidates", out var candidates) && 
                candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var part = parts[0];
                    if (part.TryGetProperty("text", out var textProp))
                    {
                        var text = textProp.GetString();
                        Console.WriteLine($"Gemini API: Successfully extracted text: {(text?.Length > 100 ? text.Substring(0, 100) + "..." : text)}");
                        return text ?? string.Empty;
                    }
                }
            }

            Console.WriteLine($"Gemini API: Failed to parse response structure. Raw: {respText.Substring(0, Math.Min(500, respText.Length))}");
            return string.Empty;
        }
        else
        {
            var err = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Gemini API Error: status={(int)response.StatusCode}, body: {(err?.Length > 1000 ? err.Substring(0, 1000) + "..." : err)}");
            return string.Empty;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Gemini API Exception: {ex.Message}\n{ex.StackTrace}");
        return string.Empty;
    }
}

    private async Task<string> CallAI(string prompt)
    {
        // Prefer Gemini if configured
        if (!string.IsNullOrEmpty(_geminiApiKey))
        {
            Console.WriteLine("CallAI: invoking Gemini");
            var g = await CallGeminiAI(prompt);
            if (!string.IsNullOrEmpty(g)) return g;
            Console.WriteLine("CallAI: Gemini returned empty response");
        }
        Console.WriteLine("CallAI: no AI provider produced a response");
        return string.Empty;
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

    public string? GetLastAssistantMessage()
    {
        var last = _messages.LastOrDefault(m => m.Role == "assistant");
        return last?.Content;
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