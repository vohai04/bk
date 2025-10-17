using BookInfoFinder.Services.Interface;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Data;
using BookInfoFinder.Models.Entity;
using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services;

public class ChatbotService : IChatbotService
{
    private readonly IBookService _bookService;
    private readonly IRatingService _ratingService;
    private readonly IFavoriteService _favoriteService;
    private readonly ICategoryService _categoryService;
    private readonly IAuthorService _authorService;
    private readonly ITagService _tagService;
    private readonly IConfiguration _config;
    private readonly BookContext _db;
    private readonly string? _geminiApiKey;
    private readonly string? _geminiModel;
    private readonly HttpClient _httpClient;

    public ChatbotService(IBookService bookService, IRatingService ratingService, 
        IFavoriteService favoriteService, ICategoryService categoryService, IAuthorService authorService, 
        ITagService tagService, IConfiguration config, BookContext db)
    {
        _bookService = bookService;
        _ratingService = ratingService;
        _favoriteService = favoriteService;
        _categoryService = categoryService;
        _authorService = authorService;
        _tagService = tagService;
        _config = config;
        _db = db;
        _geminiApiKey = _config["GEMINI:ApiKey"];
        _geminiModel = _config["GEMINI:Model"] ?? "gemini-2.0-flash-exp";
        _httpClient = new HttpClient();
        Console.WriteLine($"Gemini API Key: {(_geminiApiKey != null ? "✅ LOADED" : "❌ NULL")}, model={_geminiModel}");
    }

    public class SearchIntent
    {
        public string Type { get; set; } = ""; // "book", "author", "category", "favorite_books", "trending_books", "tags", "total_tags", "general"
        public string Keywords { get; set; } = "";
        public List<string> SearchTerms { get; set; } = new List<string>();
        public bool IsDetailRequest { get; set; } = false;
        public bool IsListRequest { get; set; } = false;
        public bool IsTotalRequest { get; set; } = false;
    }

    private SearchIntent ExtractSearchKeywords(string message)
    {
        var intent = new SearchIntent();
        var lowerMessage = message.ToLower().Trim();

        // Remove common Vietnamese filler words and normalize
        var fillerWords = new[] { "giúp", "tôi", "cho", "mình", "bạn", "có", "thể", "được", "không", "hãy", "làm", "ơn", "ạ", "và", "nhé", "nha" };
        var cleanedMessage = lowerMessage;
        foreach (var filler in fillerWords)
        {
            cleanedMessage = cleanedMessage.Replace($" {filler} ", " ").Replace($" {filler}", "").Replace($"{filler} ", "");
        }
        cleanedMessage = cleanedMessage.Trim();

        // Check for detail requests BEFORE normalization
        if (cleanedMessage.Contains("chi tiết") || cleanedMessage.Contains("xem chi tiết") ||
            cleanedMessage.Contains("thông tin") || cleanedMessage.Contains("view details"))
        {
            intent.IsDetailRequest = true;
        }

        // Check for list/total requests BEFORE normalization
        if (cleanedMessage.Contains("liệt kê") || cleanedMessage.Contains("nêu") ||
            cleanedMessage.Contains("cho biết") || cleanedMessage.Contains("list") ||
            cleanedMessage.Contains("show") || cleanedMessage.Contains("một số") ||
            cleanedMessage.Contains("1 số") || cleanedMessage.Contains("vài") ||
            cleanedMessage.Contains("mấy") || cleanedMessage.Contains("những"))
        {
            intent.IsListRequest = true;
        }

        if (cleanedMessage.Contains("tổng") || cleanedMessage.Contains("số lượng") ||
            cleanedMessage.Contains("bao nhiêu") || cleanedMessage.Contains("total") ||
            cleanedMessage.Contains("count"))
        {
            intent.IsTotalRequest = true;

            // Special case for total tags
            if (cleanedMessage.Contains("tag") || cleanedMessage.Contains("thẻ"))
            {
                intent.Type = "total_tags";
                intent.IsTotalRequest = true;
                return intent;
            }
        }

        // Apply normalization AFTER keyword detection for better pattern matching
        var normalizedMessage = NormalizeVietnamese(cleanedMessage);

        // Determine search type and extract keywords
        var vietnamesePatterns = new Dictionary<string, string[]>
        {
            ["book"] = new[] { "sách", "cuốn sách", "quyển sách", "tác phẩm", "truyện", "tiểu thuyết", "book" },
            ["author"] = new[] { "tác giả", "người viết", "viết bởi", "của", "author", "writer" },
            ["category"] = new[] { "thể loại", "loại sách", "danh mục", "category", "genre", "văn" },
            ["publisher"] = new[] { "nhà xuất bản", "nxb", "publisher" },
            ["favorite_books"] = new[] { "yêu thích", "favorite", "ưa thích", "thích nhất" },
            ["trending_books"] = new[] { "xu hướng", "trending", "hot", "phổ biến", "được tìm kiếm nhiều" },
            ["tags"] = new[] { "tags", "tag", "thẻ", "nhãn" }
        };

        // Check for explicit type indicators using cleaned message (not normalized)
        foreach (var pattern in vietnamesePatterns)
        {
            foreach (var keyword in pattern.Value)
            {
                if (cleanedMessage.Contains(keyword))
                {
                    intent.Type = pattern.Key;
                    
                    // Extract keywords after the type indicator
                    var parts = cleanedMessage.Split(new[] { keyword }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        var remaining = parts[1].Trim();
                        // Remove common connecting words
                        var connectors = new[] { "về", "là", "có", "tên", "gì", "nào", "bao" };
                        foreach (var connector in connectors)
                        {
                            remaining = remaining.Replace($" {connector} ", " ").Replace($" {connector}", "").Replace($"{connector} ", "");
                        }
                        intent.Keywords = remaining.Trim();
                        break;
                    }
                }
            }
            if (!string.IsNullOrEmpty(intent.Type)) break;
        }

        // If no explicit type found, try to infer from context
        if (string.IsNullOrEmpty(intent.Type))
        {
            // Check if it looks like a book title (contains multiple words, no common verbs)
            var words = normalizedMessage.Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            
            // If message is short and doesn't contain action words, might be a direct search
            var actionWords = new[] { "tìm", "kiếm", "search", "find", "xem", "liệt", "nêu", "cho", "biết", "tổng", "số" };
            var hasActionWord = actionWords.Any(word => cleanedMessage.Contains(word));
            
            if (!hasActionWord && words.Length >= 1 && words.Length <= 10)
            {
                // Could be a direct book/author/category name
                intent.Keywords = cleanedMessage;
                
                // Default to book search for direct queries, but also check for category indicators
                if (normalizedMessage.Contains("hài") || normalizedMessage.Contains("comedy") || 
                    normalizedMessage.Contains("kịch") || normalizedMessage.Contains("drama") ||
                    normalizedMessage.Contains("tình cảm") || normalizedMessage.Contains("romance") ||
                    normalizedMessage.Contains("fantasy") || normalizedMessage.Contains("fanta") ||
                    normalizedMessage.Contains("viễn tưởng") || normalizedMessage.Contains("scifi"))
                {
                    intent.Type = "category";
                }
                else
                {
                    // Default to book search for direct queries
                    intent.Type = "book";
                }
            }
            else if (!hasActionWord)
            {
                intent.Type = "general";
                intent.Keywords = cleanedMessage;
            }
        }

        // Split keywords into search terms for better matching
        if (!string.IsNullOrEmpty(intent.Keywords))
        {
            intent.SearchTerms = NormalizeVietnamese(intent.Keywords).Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(term => term.Length > 1) // Filter out very short terms
                .Distinct()
                .ToList();
        }

        return intent;
    }

    public async Task<string> GetChatbotReplyAsync(string message, string? sessionId)
    {
        if (string.IsNullOrEmpty(_geminiApiKey))
        {
            return "API key not configured.";
        }

        // Store user message
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

        var lowerMessage = message.ToLower();
        string response = "";

        // Extract search intent from the message
        var searchIntent = ExtractSearchKeywords(message);

        // Check for contextual queries first
        var contextualResponse = await HandleContextualQuery(lowerMessage, sessionId);
        if (!string.IsNullOrEmpty(contextualResponse))
        {
            response = contextualResponse;
        }
        // Handle based on search intent
        else if (!string.IsNullOrEmpty(searchIntent.Type))
        {
            switch (searchIntent.Type)
            {
                case "book":
                    if (searchIntent.IsTotalRequest)
                    {
                        response = await GetTotalBooksAsync();
                    }
                    else if (searchIntent.IsDetailRequest)
                    {
                        response = await HandleBookDetailRequest(lowerMessage);
                    }
                    else if (searchIntent.IsListRequest || string.IsNullOrEmpty(searchIntent.Keywords))
                    {
                        var books = await _bookService.GetAllBooksAsync();
                        var topBooks = books.Take(5).Select(b => b.Title).ToList();
                        response = $"Một số sách trong hệ thống: {string.Join(", ", topBooks)}";
                    }
                    else
                    {
                        // Search for specific book
                        response = await SearchBooksByKeywords(searchIntent);
                    }
                    break;

                case "author":
                    if (searchIntent.IsTotalRequest)
                    {
                        response = await GetTotalAuthorsAsync();
                    }
                    else if (searchIntent.IsDetailRequest)
                    {
                        response = await HandleAuthorDetailRequest(lowerMessage);
                    }
                    else if (searchIntent.IsListRequest || string.IsNullOrEmpty(searchIntent.Keywords))
                    {
                        var authors = await _authorService.GetAllAuthorsAsync();
                        var topAuthors = authors.Take(5).Select(a => a.Name).ToList();
                        response = $"Một số tác giả trong hệ thống: {string.Join(", ", topAuthors)}";
                    }
                    else
                    {
                        // Search for specific author
                        response = await SearchAuthorsByKeywords(searchIntent);
                    }
                    break;

                case "category":
                    if (searchIntent.IsTotalRequest)
                    {
                        response = await GetTotalCategoriesAsync();
                    }
                    else if (searchIntent.IsDetailRequest)
                    {
                        response = await HandleCategoryDetailRequest(lowerMessage);
                    }
                    else if (searchIntent.IsListRequest || string.IsNullOrEmpty(searchIntent.Keywords))
                    {
                        var categories = await _categoryService.GetAllCategoriesAsync();
                        var topCategories = categories.Take(5).Select(c => c.Name).ToList();
                        response = $"Một số thể loại sách: {string.Join(", ", topCategories)}";
                    }
                    else
                    {
                        // Search for specific category
                        response = await SearchCategoriesByKeywords(searchIntent);
                    }
                    break;

                case "favorite_books":
                    response = await GetFavoriteBooksAsync();
                    break;

                case "trending_books":
                    response = await GetTrendingBooksAsync();
                    break;

                case "tags":
                    if (searchIntent.IsTotalRequest)
                    {
                        response = await GetTotalTagsAsync();
                    }
                    else
                    {
                        response = await GetAllTagsAsync();
                    }
                    break;

                case "total_tags":
                    response = await GetTotalTagsAsync();
                    break;

                case "general":
                    // Handle general queries like totals
                    if (searchIntent.IsTotalRequest)
                    {
                        if (lowerMessage.Contains("sách") || lowerMessage.Contains("book"))
                        {
                            response = await GetTotalBooksAsync();
                        }
                        else if (lowerMessage.Contains("thể loại") || lowerMessage.Contains("category"))
                        {
                            response = await GetTotalCategoriesAsync();
                        }
                        else if (lowerMessage.Contains("tác giả") || lowerMessage.Contains("author"))
                        {
                            response = await GetTotalAuthorsAsync();
                        }
                        else
                        {
                            response = "Bạn muốn biết tổng số gì? Ví dụ: tổng số sách, tổng số tác giả, tổng số thể loại...";
                        }
                    }
                    else
                    {
                        // Try to search across all types
                        response = await SearchAllByKeywords(searchIntent);
                    }
                    break;
            }
        }
        // Fallback to Gemini API for other queries
        else
        {
            // Fall back to Gemini API for other queries
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_geminiModel}:generateContent?key={_geminiApiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = message }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var responseMsg = await _httpClient.PostAsync(url, content);
                responseMsg.EnsureSuccessStatusCode();

                var responseJson = await responseMsg.Content.ReadAsStringAsync();
                var responseObject = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (responseObject.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var contentProp) &&
                    contentProp.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var text))
                {
                    response = text.GetString() ?? "No response generated.";
                }
                else
                {
                    response = "Failed to parse response.";
                }
            }
            catch (Exception ex)
            {
                response = $"Error: {ex.Message}";
            }
        }

        // Store bot response
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

    private async Task<string> SearchBooksByKeywords(SearchIntent intent)
    {
        if (string.IsNullOrEmpty(intent.Keywords))
        {
            var books = await _bookService.GetAllBooksAsync();
            var topBooks = books.Take(5).Select(b => b.Title).ToList();
            return $"Một số sách trong hệ thống: {string.Join(", ", topBooks)}";
        }

        var allBooks = await _bookService.GetAllBooksAsync();
        var matchedBooks = new List<BookListDto>();

        // Normalize search terms (remove diacritics and common typos)
        var normalizedSearchTerms = intent.SearchTerms.Select(term => NormalizeVietnamese(term)).ToList();

        // Search by title with enhanced fuzzy matching
        foreach (var term in normalizedSearchTerms)
        {
            var originalTerm = intent.SearchTerms[normalizedSearchTerms.IndexOf(term)];
            
            // Direct matches (with and without diacritics)
            var directMatches = allBooks.Where(b =>
                b.Title.ToLower().Contains(originalTerm.ToLower()) ||
                NormalizeVietnamese(b.Title.ToLower()).Contains(term) ||
                term.Contains(NormalizeVietnamese(b.Title.ToLower()))
            ).ToList();

            // Fuzzy matches for typos
            var fuzzyMatches = allBooks.Where(b =>
                CalculateSimilarity(NormalizeVietnamese(b.Title.ToLower()), term) > 0.7 ||
                CalculateSimilarity(b.Title.ToLower(), originalTerm.ToLower()) > 0.7
            ).ToList();

            matchedBooks.AddRange(directMatches);
            matchedBooks.AddRange(fuzzyMatches);
        }

        // Also search by author name if no direct book matches
        if (!matchedBooks.Any())
        {
            var allAuthors = await _authorService.GetAllAuthorsAsync();
            foreach (var term in normalizedSearchTerms)
            {
                var originalTerm = intent.SearchTerms[normalizedSearchTerms.IndexOf(term)];
                
                var authorMatches = allAuthors.Where(a =>
                    NormalizeVietnamese(a.Name.ToLower()).Contains(term) ||
                    a.Name.ToLower().Contains(originalTerm.ToLower()) ||
                    CalculateSimilarity(NormalizeVietnamese(a.Name.ToLower()), term) > 0.7
                ).ToList();

                foreach (var author in authorMatches)
                {
                    var authorBooks = await _authorService.GetBooksByAuthorAsync(author.AuthorId);
                    matchedBooks.AddRange(authorBooks);
                }
            }
        }

        var uniqueBooks = matchedBooks.DistinctBy(b => b.BookId).ToList();

        if (uniqueBooks.Any())
        {
            if (uniqueBooks.Count == 1)
            {
                // Return detailed info for single match
                var book = uniqueBooks.First();
                var bookDetail = await _bookService.GetBookDetailWithStatsAndCommentsAsync(book.BookId, 1, 3);
                if (bookDetail != null)
                {
                    return FormatBookDetail(bookDetail);
                }
            }
            else
            {
                // Return list of matches
                var bookTitles = uniqueBooks.Take(5).Select(b => b.Title).ToList();
                return $"📚 Tìm thấy {uniqueBooks.Count} sách phù hợp:\n{string.Join("\n• ", bookTitles)}\n\nBạn có muốn xem chi tiết sách nào không?";
            }
        }

        return $"Không tìm thấy sách nào phù hợp với từ khóa '{intent.Keywords}'. Vui lòng thử từ khóa khác.";
    }

    private async Task<string> SearchAuthorsByKeywords(SearchIntent intent)
    {
        if (string.IsNullOrEmpty(intent.Keywords))
        {
            var authors = await _authorService.GetAllAuthorsAsync();
            var topAuthors = authors.Take(5).Select(a => a.Name).ToList();
            return $"Một số tác giả trong hệ thống: {string.Join(", ", topAuthors)}";
        }

        var allAuthors = await _authorService.GetAllAuthorsAsync();
        var matchedAuthors = new List<AuthorDto>();

        // Normalize search terms
        var normalizedSearchTerms = intent.SearchTerms.Select(term => NormalizeVietnamese(term)).ToList();

        foreach (var term in normalizedSearchTerms)
        {
            var originalTerm = intent.SearchTerms[normalizedSearchTerms.IndexOf(term)];
            
            // Direct matches (with and without diacritics)
            var directMatches = allAuthors.Where(a =>
                a.Name.ToLower().Contains(originalTerm.ToLower()) ||
                NormalizeVietnamese(a.Name.ToLower()).Contains(term) ||
                term.Contains(NormalizeVietnamese(a.Name.ToLower()))
            ).ToList();

            // Fuzzy matches for typos
            var fuzzyMatches = allAuthors.Where(a =>
                CalculateSimilarity(NormalizeVietnamese(a.Name.ToLower()), term) > 0.7 ||
                CalculateSimilarity(a.Name.ToLower(), originalTerm.ToLower()) > 0.7
            ).ToList();

            matchedAuthors.AddRange(directMatches);
            matchedAuthors.AddRange(fuzzyMatches);
        }

        var uniqueAuthors = matchedAuthors.DistinctBy(a => a.AuthorId).ToList();

        if (uniqueAuthors.Any())
        {
            if (uniqueAuthors.Count == 1)
            {
                // Return detailed info for single match
                var author = uniqueAuthors.First();
                var authorBooks = await _authorService.GetBooksByAuthorAsync(author.AuthorId);
                return FormatAuthorDetail(author, authorBooks);
            }
            else
            {
                // Return list of matches
                var authorNames = uniqueAuthors.Take(5).Select(a => a.Name).ToList();
                return $"👤 Tìm thấy {uniqueAuthors.Count} tác giả phù hợp:\n{string.Join("\n• ", authorNames)}\n\nBạn có muốn xem chi tiết tác giả nào không?";
            }
        }

        return $"Không tìm thấy tác giả nào phù hợp với từ khóa '{intent.Keywords}'. Vui lòng thử từ khóa khác.";
    }

    private async Task<string> SearchCategoriesByKeywords(SearchIntent intent)
    {
        if (string.IsNullOrEmpty(intent.Keywords))
        {
            var categories = await _categoryService.GetAllCategoriesAsync();
            var topCategories = categories.Take(5).Select(c => c.Name).ToList();
            return $"Một số thể loại sách: {string.Join(", ", topCategories)}";
        }

        var allCategories = await _categoryService.GetAllCategoriesAsync();
        var matchedCategories = new List<CategoryDto>();

        // Normalize search terms
        var normalizedSearchTerms = intent.SearchTerms.Select(term => NormalizeVietnamese(term)).ToList();

        foreach (var term in normalizedSearchTerms)
        {
            var originalTerm = intent.SearchTerms[normalizedSearchTerms.IndexOf(term)];
            
            // Direct matches (with and without diacritics)
            var directMatches = allCategories.Where(c =>
                c.Name.ToLower().Contains(originalTerm.ToLower()) ||
                NormalizeVietnamese(c.Name.ToLower()).Contains(term) ||
                term.Contains(NormalizeVietnamese(c.Name.ToLower()))
            ).ToList();

            // Fuzzy matches for typos and variations
            var fuzzyMatches = allCategories.Where(c =>
                CalculateSimilarity(NormalizeVietnamese(c.Name.ToLower()), term) > 0.7 ||
                CalculateSimilarity(c.Name.ToLower(), originalTerm.ToLower()) > 0.7
            ).ToList();

            matchedCategories.AddRange(directMatches);
            matchedCategories.AddRange(fuzzyMatches);
        }

        var uniqueCategories = matchedCategories.DistinctBy(c => c.CategoryId).ToList();

        if (uniqueCategories.Any())
        {
            if (uniqueCategories.Count == 1)
            {
                // Return detailed info for single match
                var category = uniqueCategories.First();
                var categoryBooks = await _categoryService.GetBooksByCategoryAsync(category.CategoryId);
                return FormatCategoryDetail(category, categoryBooks);
            }
            else
            {
                // Return list of matches
                var categoryNames = uniqueCategories.Take(5).Select(c => c.Name).ToList();
                return $"📚 Tìm thấy {uniqueCategories.Count} thể loại phù hợp:\n{string.Join("\n• ", categoryNames)}\n\nBạn có muốn xem chi tiết thể loại nào không?";
            }
        }

        return $"Không tìm thấy thể loại nào phù hợp với từ khóa '{intent.Keywords}'. Vui lòng thử từ khóa khác.";
    }

    private async Task<string> SearchAllByKeywords(SearchIntent intent)
    {
        if (string.IsNullOrEmpty(intent.Keywords))
        {
            return "Bạn muốn tìm gì? Ví dụ: sách, tác giả, thể loại...";
        }

        var results = new List<string>();

        // Search books
        var bookIntent = new SearchIntent { Type = "book", Keywords = intent.Keywords, SearchTerms = intent.SearchTerms };
        var bookResults = await SearchBooksByKeywords(bookIntent);
        if (!bookResults.Contains("Không tìm thấy"))
        {
            results.Add($"📚 Sách: {bookResults.Split('\n').First()}");
        }

        // Search authors
        var authorIntent = new SearchIntent { Type = "author", Keywords = intent.Keywords, SearchTerms = intent.SearchTerms };
        var authorResults = await SearchAuthorsByKeywords(authorIntent);
        if (!authorResults.Contains("Không tìm thấy"))
        {
            results.Add($"👤 Tác giả: {authorResults.Split('\n').First()}");
        }

        // Search categories
        var categoryIntent = new SearchIntent { Type = "category", Keywords = intent.Keywords, SearchTerms = intent.SearchTerms };
        var categoryResults = await SearchCategoriesByKeywords(categoryIntent);
        if (!categoryResults.Contains("Không tìm thấy"))
        {
            results.Add($"📖 Thể loại: {categoryResults.Split('\n').First()}");
        }

        if (results.Any())
        {
            return $"🔍 Kết quả tìm kiếm cho '{intent.Keywords}':\n\n{string.Join("\n\n", results)}";
        }

        return $"Không tìm thấy kết quả nào cho từ khóa '{intent.Keywords}'. Vui lòng thử từ khóa khác hoặc tìm kiếm cụ thể hơn.";
    }

    private string NormalizeVietnamese(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Common Vietnamese diacritic mappings
        var diacritics = new Dictionary<char, char>
        {
            ['à'] = 'a', ['á'] = 'a', ['ả'] = 'a', ['ã'] = 'a', ['ạ'] = 'a',
            ['â'] = 'a', ['ầ'] = 'a', ['ấ'] = 'a', ['ẩ'] = 'a', ['ẫ'] = 'a', ['ậ'] = 'a',
            ['ă'] = 'a', ['ằ'] = 'a', ['ắ'] = 'a', ['ẳ'] = 'a', ['ẵ'] = 'a', ['ặ'] = 'a',
            ['è'] = 'e', ['é'] = 'e', ['ẻ'] = 'e', ['ẽ'] = 'e', ['ẹ'] = 'e',
            ['ê'] = 'e', ['ề'] = 'e', ['ế'] = 'e', ['ể'] = 'e', ['ễ'] = 'e', ['ệ'] = 'e',
            ['ì'] = 'i', ['í'] = 'i', ['ỉ'] = 'i', ['ĩ'] = 'i', ['ị'] = 'i',
            ['ò'] = 'o', ['ó'] = 'o', ['ỏ'] = 'o', ['õ'] = 'o', ['ọ'] = 'o',
            ['ô'] = 'o', ['ồ'] = 'o', ['ố'] = 'o', ['ổ'] = 'o', ['ỗ'] = 'o', ['ộ'] = 'o',
            ['ơ'] = 'o', ['ờ'] = 'o', ['ớ'] = 'o', ['ở'] = 'o', ['ỡ'] = 'o', ['ợ'] = 'o',
            ['ù'] = 'u', ['ú'] = 'u', ['ủ'] = 'u', ['ũ'] = 'u', ['ụ'] = 'u',
            ['ư'] = 'u', ['ừ'] = 'u', ['ứ'] = 'u', ['ử'] = 'u', ['ữ'] = 'u', ['ự'] = 'u',
            ['ỳ'] = 'y', ['ý'] = 'y', ['ỷ'] = 'y', ['ỹ'] = 'y', ['ỵ'] = 'y',
            ['đ'] = 'd',
            ['À'] = 'A', ['Á'] = 'A', ['Ả'] = 'A', ['Ã'] = 'A', ['Ạ'] = 'A',
            ['Â'] = 'A', ['Ầ'] = 'A', ['Ấ'] = 'A', ['Ẩ'] = 'A', ['Ẫ'] = 'A', ['Ậ'] = 'A',
            ['Ă'] = 'A', ['Ằ'] = 'A', ['Ắ'] = 'A', ['Ẳ'] = 'A', ['Ẵ'] = 'A', ['Ặ'] = 'A',
            ['È'] = 'E', ['É'] = 'E', ['Ẻ'] = 'E', ['Ẽ'] = 'E', ['Ẹ'] = 'E',
            ['Ê'] = 'E', ['Ề'] = 'E', ['Ế'] = 'E', ['Ể'] = 'E', ['Ễ'] = 'E', ['Ệ'] = 'E',
            ['Ì'] = 'I', ['Í'] = 'I', ['Ỉ'] = 'I', ['Ĩ'] = 'I', ['Ị'] = 'I',
            ['Ò'] = 'O', ['Ó'] = 'O', ['Ỏ'] = 'O', ['Õ'] = 'O', ['Ọ'] = 'O',
            ['Ô'] = 'O', ['Ồ'] = 'O', ['Ố'] = 'O', ['Ổ'] = 'O', ['Ỗ'] = 'O', ['Ộ'] = 'O',
            ['Ơ'] = 'O', ['Ờ'] = 'O', ['Ớ'] = 'O', ['Ở'] = 'O', ['Ỡ'] = 'O', ['Ợ'] = 'O',
            ['Ù'] = 'U', ['Ú'] = 'U', ['Ủ'] = 'U', ['Ũ'] = 'U', ['Ụ'] = 'U',
            ['Ư'] = 'U', ['Ừ'] = 'U', ['Ứ'] = 'U', ['Ử'] = 'U', ['Ữ'] = 'U', ['Ự'] = 'U',
            ['Ỳ'] = 'Y', ['Ý'] = 'Y', ['Ỷ'] = 'Y', ['Ỹ'] = 'Y', ['Ỵ'] = 'Y',
            ['Đ'] = 'D'
        };

        var normalized = new string(text.Select(c => diacritics.ContainsKey(c) ? diacritics[c] : c).ToArray());
        
        // Apply common typo corrections
        normalized = ApplyTypoCorrections(normalized);
        
        return normalized.ToLower();
    }

    private string ApplyTypoCorrections(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Common typo corrections for book/genre names
        var corrections = new Dictionary<string, string>
        {
            // Genre corrections
            ["fantasssy"] = "fantasy",
            ["fanta"] = "fantasy", 
            ["fantsy"] = "fantasy",
            ["fantasi"] = "fantasy",
            ["scifi"] = "sci-fi",
            ["sc fi"] = "sci-fi",
            ["science fiction"] = "sci-fi",
            ["vien tuong"] = "vien tuong",
            ["vientuong"] = "vien tuong",
            ["horror"] = "kinh di",
            ["kinhdi"] = "kinh di",
            ["romance"] = "tinh cam",
            ["tinhcam"] = "tinh cam",
            ["comedy"] = "hai",
            ["drama"] = "kich",
            ["mystery"] = "trinh tham",
            ["trinhtham"] = "trinh tham",
            ["thriller"] = "gay can",
            ["gaycan"] = "gay can",
            
            // Common name corrections
            ["vawn"] = "van",
            ["haii"] = "hai",
            ["nguyeen"] = "nguyen",
            ["nguyenx"] = "nguyen",
            ["tranx"] = "tran",
            ["lev"] = "le",
            ["phamx"] = "pham",
            ["hoangx"] = "hoang",
            ["vux"] = "vu",
            ["dox"] = "do",
            ["maix"] = "mai",
            ["trann"] = "tran",
            ["iu"] = "yeu"
        };

        var result = text;
        foreach (var correction in corrections)
        {
            result = result.Replace(correction.Key, correction.Value);
        }

        return result;
    }

    private double CalculateSimilarity(string str1, string str2)
    {
        // Simple Levenshtein distance-based similarity
        var distance = LevenshteinDistance(str1, str2);
        var maxLength = Math.Max(str1.Length, str2.Length);
        return maxLength == 0 ? 1.0 : 1.0 - (double)distance / maxLength;
    }

    private int LevenshteinDistance(string str1, string str2)
    {
        var matrix = new int[str1.Length + 1, str2.Length + 1];

        for (int i = 0; i <= str1.Length; i++)
            matrix[i, 0] = i;
        for (int j = 0; j <= str2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= str1.Length; i++)
        {
            for (int j = 1; j <= str2.Length; j++)
            {
                var cost = str1[i - 1] == str2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[str1.Length, str2.Length];
    }

    private string FormatBookDetail(BookDetailDto bookDetail)
    {
        var response = $"📖 **{bookDetail.Title}**\n\n";
        response += $"👤 **Tác giả:** {bookDetail.AuthorName}\n";
        response += $"📚 **Thể loại:** {bookDetail.CategoryName}\n";
        response += $"🏢 **Nhà xuất bản:** {bookDetail.PublisherName}\n";
        response += $"📅 **Ngày xuất bản:** {bookDetail.PublicationDate:dd/MM/yyyy}\n";
        response += $"⭐ **Đánh giá:** {bookDetail.AverageRating:F1}/5 ({bookDetail.RatingCount} đánh giá)\n";
        
        if (!string.IsNullOrEmpty(bookDetail.ISBN))
        {
            response += $"📋 **ISBN:** {bookDetail.ISBN}\n";
        }
        
        if (bookDetail.Tags.Any())
        {
            response += $"🏷️ **Tags:** {string.Join(", ", bookDetail.Tags)}\n";
        }
        
        if (!string.IsNullOrEmpty(bookDetail.Abstract))
        {
            response += $"\n📝 **Tóm tắt:** {bookDetail.Abstract}\n";
        }
        else if (!string.IsNullOrEmpty(bookDetail.Description))
        {
            response += $"\n📝 **Mô tả:** {bookDetail.Description}\n";
        }

        return response;
    }

    private string FormatAuthorDetail(AuthorDto author, List<BookListDto> books)
    {
        var response = $"👤 **{author.Name}**\n\n";
        
        if (!string.IsNullOrEmpty(author.Nationality))
        {
            response += $"🌍 **Quốc tịch:** {author.Nationality}\n";
        }
        
        if (author.DateOfBirth.HasValue)
        {
            response += $"📅 **Ngày sinh:** {author.DateOfBirth.Value:dd/MM/yyyy}\n";
        }
        
        response += $"📚 **Số lượng sách:** {author.BookCount}\n";
        
        if (books.Any())
        {
            response += $"📖 **Một số sách nổi bật:**\n";
            var topBooks = books.Take(3).Select(b => b.Title).ToList();
            for (int i = 0; i < topBooks.Count; i++)
            {
                response += $"{i + 1}. {topBooks[i]}\n";
            }
        }
        
        if (!string.IsNullOrEmpty(author.Biography))
        {
            response += $"\n📝 **Tiểu sử:** {author.Biography}\n";
        }

        return response;
    }

    private string FormatCategoryDetail(CategoryDto category, List<BookListDto> books)
    {
        var response = $"📚 **Thể loại: {category.Name}**\n\n";
        
        if (!string.IsNullOrEmpty(category.Description))
        {
            response += $"📝 **Mô tả:** {category.Description}\n\n";
        }
        
        response += $"📊 **Số lượng sách:** {category.BookCount}\n";
        
        if (books.Any())
        {
            response += $"📖 **Một số sách nổi bật:**\n";
            var topBooks = books.Take(3).Select(b => b.Title).ToList();
            for (int i = 0; i < topBooks.Count; i++)
            {
                response += $"{i + 1}. {topBooks[i]}\n";
            }
        }

        return response;
    }

    private async Task<string> HandleContextualQuery(string lowerMessage, string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return "";

        // Get recent conversation history (last 10 messages to get better context)
        var history = await GetHistoryAsync(sessionId);
        var recentMessages = history.OrderByDescending(h => h.CreatedAt).Take(10).ToList();

        // Check if this is a follow-up question about listing items
        if (lowerMessage.Contains("liệt kê") || lowerMessage.Contains("nêu") || 
            lowerMessage.Contains("cho biết") || lowerMessage.Contains("list") ||
            lowerMessage.Contains("show") || lowerMessage.Contains("tell me"))
        {
            // Look for previous queries about totals or specific topics
            foreach (var msg in recentMessages.Where(m => m.Role == "assistant"))
            {
                var botMessage = msg.Message.ToLower();
                
                // If any previous bot message mentioned authors
                if (botMessage.Contains("tác giả") || botMessage.Contains("author"))
                {
                    var authors = await _authorService.GetAllAuthorsAsync();
                    var topAuthors = authors.Take(5).Select(a => a.Name).ToList();
                    return $"Danh sách một số tác giả: {string.Join(", ", topAuthors)}";
                }
                
                // If any previous bot message mentioned books
                if (botMessage.Contains("sách") || botMessage.Contains("book"))
                {
                    var books = await _bookService.GetAllBooksAsync();
                    var topBooks = books.Take(5).Select(b => b.Title).ToList();
                    return $"Danh sách một số sách: {string.Join(", ", topBooks)}";
                }
                
                // If any previous bot message mentioned categories
                if (botMessage.Contains("thể loại") || botMessage.Contains("category"))
                {
                    var categories = await _categoryService.GetAllCategoriesAsync();
                    var topCategories = categories.Take(5).Select(c => c.Name).ToList();
                    return $"Danh sách một số thể loại: {string.Join(", ", topCategories)}";
                }
            }
        }

        // Check for book detail requests in context
        if (lowerMessage.Contains("xem chi tiết") || lowerMessage.Contains("chi tiết sách") || 
            lowerMessage.Contains("view details") || lowerMessage.Contains("book details"))
        {
            return await HandleBookDetailRequest(lowerMessage);
        }

        // Check for author detail requests in context
        if (lowerMessage.Contains("xem chi tiết tác giả") || lowerMessage.Contains("chi tiết tác giả") ||
            lowerMessage.Contains("view author details") || lowerMessage.Contains("author details"))
        {
            return await HandleAuthorDetailRequest(lowerMessage);
        }

        // Check for category detail requests in context
        if (lowerMessage.Contains("xem chi tiết thể loại") || lowerMessage.Contains("chi tiết thể loại") ||
            lowerMessage.Contains("view category details") || lowerMessage.Contains("category details"))
        {
            return await HandleCategoryDetailRequest(lowerMessage);
        }

        return "";
    }

    private async Task<string> HandleBookDetailRequest(string lowerMessage)
    {
        // Extract book title from the message
        // Look for patterns like "xem chi tiết sách [title]" or "chi tiết sách [title]"
        var titlePatterns = new[] { "xem chi tiết sách", "chi tiết sách", "view details of", "book details" };
        
        string bookTitle = "";
        foreach (var pattern in titlePatterns)
        {
            if (lowerMessage.Contains(pattern.ToLower()))
            {
                bookTitle = lowerMessage.Replace(pattern.ToLower(), "").Trim();
                break;
            }
        }

        if (string.IsNullOrEmpty(bookTitle))
        {
            return "Vui lòng cho biết tên sách bạn muốn xem chi tiết. Ví dụ: 'xem chi tiết sách Harry Potter'";
        }

        // Search for the book using fuzzy matching
        var allBooks = await _bookService.GetAllBooksAsync();
        var matchedBook = allBooks.FirstOrDefault(b => 
            b.Title.Equals(bookTitle, StringComparison.OrdinalIgnoreCase)) ??
            allBooks.FirstOrDefault(b => 
                b.Title.ToLower().Contains(bookTitle.ToLower())) ??
            allBooks.FirstOrDefault(b => 
                bookTitle.ToLower().Contains(b.Title.ToLower()));

        if (matchedBook == null)
        {
            return $"Không tìm thấy sách với tên '{bookTitle}'. Vui lòng kiểm tra lại tên sách.";
        }

        // Get detailed information
        var bookDetail = await _bookService.GetBookDetailWithStatsAndCommentsAsync(matchedBook.BookId, 1, 5);

        if (bookDetail == null)
        {
            return $"Không thể tải chi tiết sách '{matchedBook.Title}'.";
        }

        // Format the response
        var response = $"📖 **{bookDetail.Title}**\n\n";
        response += $"👤 **Tác giả:** {bookDetail.AuthorName}\n";
        response += $"📚 **Thể loại:** {bookDetail.CategoryName}\n";
        response += $"🏢 **Nhà xuất bản:** {bookDetail.PublisherName}\n";
        response += $"📅 **Ngày xuất bản:** {bookDetail.PublicationDate:dd/MM/yyyy}\n";
        response += $"⭐ **Đánh giá:** {bookDetail.AverageRating:F1}/5 ({bookDetail.RatingCount} đánh giá)\n";
        
        if (!string.IsNullOrEmpty(bookDetail.ISBN))
        {
            response += $"📋 **ISBN:** {bookDetail.ISBN}\n";
        }
        
        if (bookDetail.Tags.Any())
        {
            response += $"🏷️ **Tags:** {string.Join(", ", bookDetail.Tags)}\n";
        }
        
        if (!string.IsNullOrEmpty(bookDetail.Abstract))
        {
            response += $"\n📝 **Tóm tắt:** {bookDetail.Abstract}\n";
        }
        else if (!string.IsNullOrEmpty(bookDetail.Description))
        {
            response += $"\n📝 **Mô tả:** {bookDetail.Description}\n";
        }

        return response;
    }

    private async Task<string> HandleAuthorDetailRequest(string lowerMessage)
    {
        // Extract author name from the message
        var namePatterns = new[] { "xem chi tiết tác giả", "chi tiết tác giả", "chi tiết" };
        
        string authorName = "";
        foreach (var pattern in namePatterns)
        {
            if (lowerMessage.Contains(pattern.ToLower()))
            {
                authorName = lowerMessage.Replace(pattern.ToLower(), "").Trim();
                // Remove extra words if present
                authorName = authorName.Replace("tác giả", "").Replace("author", "").Trim();
                break;
            }
        }

        if (string.IsNullOrEmpty(authorName))
        {
            return "Vui lòng cho biết tên tác giả bạn muốn xem chi tiết. Ví dụ: 'chi tiết Nguyễn Du'";
        }

        // Search for the author using fuzzy matching
        var allAuthors = await _authorService.GetAllAuthorsAsync();
        var matchedAuthor = allAuthors.FirstOrDefault(a => 
            a.Name.Equals(authorName, StringComparison.OrdinalIgnoreCase)) ??
            allAuthors.FirstOrDefault(a => 
                a.Name.ToLower().Contains(authorName.ToLower())) ??
            allAuthors.FirstOrDefault(a => 
                authorName.ToLower().Contains(a.Name.ToLower()));

        if (matchedAuthor == null)
        {
            return $"Không tìm thấy tác giả với tên '{authorName}'. Vui lòng kiểm tra lại tên tác giả.";
        }

        // Get books by this author
        var booksByAuthor = await _authorService.GetBooksByAuthorAsync(matchedAuthor.AuthorId);

        // Format the response
        var response = $"👤 **{matchedAuthor.Name}**\n\n";
        
        if (!string.IsNullOrEmpty(matchedAuthor.Nationality))
        {
            response += $"🌍 **Quốc tịch:** {matchedAuthor.Nationality}\n";
        }
        
        if (matchedAuthor.DateOfBirth.HasValue)
        {
            response += $"📅 **Ngày sinh:** {matchedAuthor.DateOfBirth.Value:dd/MM/yyyy}\n";
        }
        
        response += $"📚 **Số lượng sách:** {matchedAuthor.BookCount}\n";
        
        if (booksByAuthor.Any())
        {
            response += $"📖 **Một số sách nổi bật:**\n";
            var topBooks = booksByAuthor.Take(3).Select(b => b.Title).ToList();
            for (int i = 0; i < topBooks.Count; i++)
            {
                response += $"{i + 1}. {topBooks[i]}\n";
            }
        }
        
        if (!string.IsNullOrEmpty(matchedAuthor.Biography))
        {
            response += $"\n📝 **Tiểu sử:** {matchedAuthor.Biography}\n";
        }

        return response;
    }

    private async Task<string> TryGetCategoryDetail(string lowerMessage)
    {
        var categoryName = lowerMessage.Replace("chi tiết", "").Trim();
        if (string.IsNullOrEmpty(categoryName)) return "";

        var allCategories = await _categoryService.GetAllCategoriesAsync();
        var matchedCategory = allCategories.FirstOrDefault(c => 
            c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase)) ??
            allCategories.FirstOrDefault(c => 
                c.Name.ToLower().Contains(categoryName.ToLower())) ??
            allCategories.FirstOrDefault(c => 
                categoryName.ToLower().Contains(c.Name.ToLower()));

        if (matchedCategory != null)
        {
            var booksInCategory = await _categoryService.GetBooksByCategoryAsync(matchedCategory.CategoryId);
            var response = $"📚 **Thể loại: {matchedCategory.Name}**\n\n";
            
            if (!string.IsNullOrEmpty(matchedCategory.Description))
            {
                response += $"📝 **Mô tả:** {matchedCategory.Description}\n\n";
            }
            
            response += $"📊 **Số lượng sách:** {matchedCategory.BookCount}\n";
            
            if (booksInCategory.Any())
            {
                response += $"📖 **Một số sách nổi bật:**\n";
                var topBooks = booksInCategory.Take(3).Select(b => b.Title).ToList();
                for (int i = 0; i < topBooks.Count; i++)
                {
                    response += $"{i + 1}. {topBooks[i]}\n";
                }
            }

            return response;
        }

        return "";
    }

    private async Task<string> TryGetAuthorDetail(string lowerMessage)
    {
        var authorName = lowerMessage.Replace("chi tiết", "").Trim();
        if (string.IsNullOrEmpty(authorName)) return "";

        var allAuthors = await _authorService.GetAllAuthorsAsync();
        var matchedAuthor = allAuthors.FirstOrDefault(a => 
            a.Name.Equals(authorName, StringComparison.OrdinalIgnoreCase)) ??
            allAuthors.FirstOrDefault(a => 
                a.Name.ToLower().Contains(authorName.ToLower())) ??
            allAuthors.FirstOrDefault(a => 
                authorName.ToLower().Contains(a.Name.ToLower()));

        if (matchedAuthor != null)
        {
            var booksByAuthor = await _authorService.GetBooksByAuthorAsync(matchedAuthor.AuthorId);
            var response = $"👤 **{matchedAuthor.Name}**\n\n";
            
            if (!string.IsNullOrEmpty(matchedAuthor.Nationality))
            {
                response += $"🌍 **Quốc tịch:** {matchedAuthor.Nationality}\n";
            }
            
            if (matchedAuthor.DateOfBirth.HasValue)
            {
                response += $"📅 **Ngày sinh:** {matchedAuthor.DateOfBirth.Value:dd/MM/yyyy}\n";
            }
            
            response += $"📚 **Số lượng sách:** {matchedAuthor.BookCount}\n";
            
            if (booksByAuthor.Any())
            {
                response += $"📖 **Một số sách nổi bật:**\n";
                var topBooks = booksByAuthor.Take(3).Select(b => b.Title).ToList();
                for (int i = 0; i < topBooks.Count; i++)
                {
                    response += $"{i + 1}. {topBooks[i]}\n";
                }
            }
            
            if (!string.IsNullOrEmpty(matchedAuthor.Biography))
            {
                response += $"\n📝 **Tiểu sử:** {matchedAuthor.Biography}\n";
            }

            return response;
        }

        return "";
    }

    private async Task<string> HandleCategoryDetailRequest(string lowerMessage)
    {
        // Extract category name from the message
        // Look for patterns like "chi tiết [category]" or "chi tiết thể loại [category]"
        var namePatterns = new[] { "chi tiết thể loại", "chi tiết category", "chi tiết" };
        
        string categoryName = "";
        foreach (var pattern in namePatterns)
        {
            if (lowerMessage.Contains(pattern.ToLower()))
            {
                categoryName = lowerMessage.Replace(pattern.ToLower(), "").Trim();
                // Remove extra words if present
                categoryName = categoryName.Replace("thể loại", "").Replace("category", "").Trim();
                break;
            }
        }

        if (string.IsNullOrEmpty(categoryName))
        {
            return "Vui lòng cho biết tên thể loại bạn muốn xem chi tiết. Ví dụ: 'chi tiết Fantasy'";
        }

        // Search for the category using fuzzy matching
        var allCategories = await _categoryService.GetAllCategoriesAsync();
        var matchedCategory = allCategories.FirstOrDefault(c => 
            c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase)) ??
            allCategories.FirstOrDefault(c => 
                c.Name.ToLower().Contains(categoryName.ToLower())) ??
            allCategories.FirstOrDefault(c => 
                categoryName.ToLower().Contains(c.Name.ToLower()));

        if (matchedCategory == null)
        {
            return $"Không tìm thấy thể loại với tên '{categoryName}'. Vui lòng kiểm tra lại tên thể loại.";
        }

        // Get books in this category for more details
        var booksInCategory = await _categoryService.GetBooksByCategoryAsync(matchedCategory.CategoryId);

        // Format the response
        var response = $"📚 **Thể loại: {matchedCategory.Name}**\n\n";
        
        if (!string.IsNullOrEmpty(matchedCategory.Description))
        {
            response += $"� **Mô tả:** {matchedCategory.Description}\n\n";
        }
        
        response += $"📊 **Số lượng sách:** {matchedCategory.BookCount}\n";
        
        if (booksInCategory.Any())
        {
            response += $"� **Một số sách nổi bật:**\n";
            var topBooks = booksInCategory.Take(3).Select(b => b.Title).ToList();
            for (int i = 0; i < topBooks.Count; i++)
            {
                response += $"{i + 1}. {topBooks[i]}\n";
            }
        }

        return response;
    }

    public async Task<List<ChatbotDto>> GetHistoryAsync(string sessionId)
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

    private async Task<string> GetFavoriteBooksAsync()
    {
        var topBooks = await _favoriteService.GetTopFavoritedBooksAsync(5);
        
        if (topBooks.Any())
        {
            var bookTitles = topBooks.Select(b => b.Title).ToList();
            return $"📚 **Sách được yêu thích nhất:**\n{string.Join("\n• ", bookTitles)}";
        }
        
        return "Hiện tại chưa có sách nào được đánh giá để xác định sách yêu thích.";
    }

    private async Task<string> GetTrendingBooksAsync()
    {
        // Get books that appear most in search history
        var searchHistory = await _db.SearchHistories
            .Where(sh => sh.BookId.HasValue) // Only include records with non-null BookId
            .GroupBy(sh => sh.BookId)
            .Select(g => new { BookId = g.Key.Value, SearchCount = g.Count() }) // Use .Value since we filtered nulls
            .OrderByDescending(g => g.SearchCount)
            .Take(5)
            .ToListAsync();
        
        if (searchHistory.Any())
        {
            var trendingBooks = new List<string>();
            foreach (var item in searchHistory)
            {
                var book = await _bookService.GetBookByIdAsync(item.BookId);
                if (book != null)
                {
                    trendingBooks.Add($"{book.Title} ({item.SearchCount} lượt tìm)");
                }
            }
            
            return $"📈 **Sách được tìm kiếm nhiều nhất:**\n{string.Join("\n• ", trendingBooks)}";
        }
        
        return "Hiện tại chưa có dữ liệu tìm kiếm để xác định sách xu hướng.";
    }

    private async Task<string> GetAllTagsAsync()
    {
        var tags = await _tagService.GetAllTagsAsync();
        
        if (tags.Any())
        {
            var tagNames = tags.Select(t => t.Name).ToList();
            return $"🏷️ **Các tags hiện có trong hệ thống:**\n{string.Join(", ", tagNames)}";
        }
        
        return "Hiện tại chưa có tags nào trong hệ thống.";
    }

    private async Task<string> GetTotalTagsAsync()
    {
        var tags = await _tagService.GetAllTagsAsync();
        return $"Tổng số tags trong hệ thống là: {tags.Count}";
    }

    private async Task<string> GetTotalBooksAsync()
    {
        var books = await _bookService.GetAllBooksAsync();
        return $"Tổng số sách trong hệ thống là: {books.Count()}";
    }

    private async Task<string> GetTotalCategoriesAsync()
    {
        var categories = await _categoryService.GetAllCategoriesAsync();
        return $"Tổng số thể loại sách là: {categories.Count}";
    }

    private async Task<string> GetTotalAuthorsAsync()
    {
        var authors = await _authorService.GetAllAuthorsAsync();
        return $"Tổng số tác giả là: {authors.Count}";
    }

    public async Task AddMessageAsync(ChatbotDto message)
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
}

   