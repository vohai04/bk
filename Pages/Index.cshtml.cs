    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using BookInfoFinder.Models.Dto;
    using BookInfoFinder.Models.Entity;
    using BookInfoFinder.Services.Interface;

    namespace BookInfoFinder.Pages
    {
        public class IndexModel : PageModel
        {
            // Services
            private readonly IBookService _bookService;
            private readonly ICategoryService _categoryService;
            private readonly ITagService _tagService;
            private readonly IFavoriteService _favoriteService;
            private readonly ISearchHistoryService _searchHistoryService;
            private readonly IAuthorService _authorService;
            private readonly IRatingService _ratingService;   // FIX: Thêm dòng này
            private readonly IBookTagService _bookTagService;
            private readonly IConfiguration _config;
            private readonly IChatbotService _chatbotService;
    
            // Data for view (chỉ dùng Tags trên Index)
            public List<TagDto> Tags { get; set; } = new();
            public string? UserName { get; set; }

        // FIX: Thêm IRatingService vào constructor
        public IndexModel(
            IBookService bookService,
            ICategoryService categoryService,
            ITagService tagService,
            IFavoriteService favoriteService,
            ISearchHistoryService searchHistoryService,
            IAuthorService authorService,
            IRatingService ratingService,
            IBookTagService bookTagService,
            IConfiguration config,
            IChatbotService chatbotService      // Thêm
        )
        {
            _bookService = bookService;
            _categoryService = categoryService;
            _tagService = tagService;
            _favoriteService = favoriteService;
            _searchHistoryService = searchHistoryService;
            _authorService = authorService;
            _ratingService = ratingService;  // FIX: Thêm dòng này
            _bookTagService = bookTagService;
            _config = config;  // Thêm
            _chatbotService = chatbotService;  // Thêm
        }
            
            
    
            public async Task OnGetAsync()
            {
                Tags = await _tagService.GetAllTagsAsync();
                UserName = HttpContext.Session.GetString("UserName");
                
                // Handle filter parameters for direct navigation
                var filter = Request.Query["filter"].ToString();
                if (!string.IsNullOrEmpty(filter))
                {
                    ViewData["ActiveFilter"] = filter;
                    ViewData["FilterTitle"] = GetFilterTitle(filter);
                }
            }
            
            private string GetFilterTitle(string filter)
            {
                return filter switch
                {
                    "top-rated" => "Sách đánh giá cao",
                    "trending" => "Xu hướng yêu thích",
                    "most-searched" => "Tìm kiếm nhiều",
                    _ => "Tất cả sách"
                };
            }
    
            // --- Handler lấy top 10 sách yêu thích nhất ---
            public async Task<JsonResult> OnGetMostFavoritedAsync()
            {
                var (books, totalCount) = await _favoriteService.GetMostFavoritedBooksPagedAsync(1, 10);
                var result = books.Select(book => new
                {
                    book.BookId,
                    book.Title,
                    book.ImageBase64,
                    book.PublicationDate,
                    AuthorName = book.AuthorName,
                    CategoryName = book.CategoryName,
                    Tags = book.Tags,
                    AverageRating = book.AverageRating,
                    RatingCount = book.RatingCount
                }).ToList();
                var totalPages = (int)Math.Ceiling(totalCount / 10.0);
                return new JsonResult(new { books = result, totalPages });
            }
            // --- Handler lấy top 10 sách đánh giá cao nhất ---
            public async Task<JsonResult> OnGetTopRatedAsync()
            {
                var (books, totalCount) = await _ratingService.GetTopRatedBooksPagedAsync(1, 10);
                var result = books.Select(book => new
                {
                    book.BookId,
                    book.Title,
                    book.ImageBase64,
                    book.PublicationDate,
                    AuthorName = book.AuthorName,
                    CategoryName = book.CategoryName,
                    Tags = book.Tags,
                    AverageRating = book.AverageRating,
                    RatingCount = book.RatingCount
                }).ToList();
                var totalPages = (int)Math.Ceiling(totalCount / 10.0);
                return new JsonResult(new { books = result, totalPages });
            }
    
            public async Task<JsonResult> OnGetSearchByTagAsync(string tag, int page = 1, int pageSize = 6)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    return new JsonResult(new { books = new List<object>(), total = 0, totalPages = 0, tag });

                // Tìm tagId theo tên
                var matched = await _tagService.SearchTagsAsync(tag);
                var tagId = matched.FirstOrDefault(t => t.Name.Equals(tag, StringComparison.OrdinalIgnoreCase))?.TagId
                            ?? matched.FirstOrDefault()?.TagId;
                if (tagId == null)
                    return new JsonResult(new { books = new List<object>(), total = 0, totalPages = 0, tag });

                var (books, total) = await _bookTagService.GetBooksByTagPagedAsync(tagId.Value, page, pageSize);
                int totalPages = (int)Math.Ceiling(total / (double)pageSize);

                var result = books.Select(book => new
                {
                    BookId = book.BookId,
                    Title = book.Title,
                    ImageBase64 = string.IsNullOrEmpty(book.ImageBase64) ? "/images/default-book.jpg" : book.ImageBase64,
                    AuthorName = book.AuthorName ?? "Không rõ",
                    CategoryName = book.CategoryName ?? "Không rõ",
                    PublicationYear = book.PublicationDate,
                    Tags = book.Tags ?? new List<string>()
                }).ToList();

                return new JsonResult(new { books = result, total, totalPages, tag });
            }
            // --- Suggest title ---
            public async Task<JsonResult> OnGetTitleSuggestAsync(string keyword)
            {
                var result = await _bookService.SuggestBookTitlesAsync(keyword);
                return new JsonResult(result);
            }
    
            // --- Suggest author ---
            public async Task<JsonResult> OnGetAuthorSuggestAsync(string keyword)
            {
                var authors = await _authorService.SearchAuthorsAsync(keyword);
                var result = authors.Select(a => a.Name).ToList();
                return new JsonResult(result);
            }
    
            // --- Suggest category ---
            public async Task<JsonResult> OnGetCategorySuggestAsync(string keyword)
            {
                var categories = await _categoryService.SearchCategoriesAsync(keyword);
                var result = categories.Select(c => c.Name).ToList();
                return new JsonResult(result);
            }
    
            // --- Handler search ajax ---
            public async Task<JsonResult> OnGetAjaxSearchAsync()
            {
                var query = Request.Query;
                string? title = query["title"];
                string? author = query["author"];
                string? category = query["category"];
                int.TryParse(query["year"], out int year);
                int.TryParse(query["page"], out int page);
                int.TryParse(query["pageSize"], out int pageSize);
    
                page = page <= 0 ? 1 : page;
                pageSize = pageSize <= 0 ? 8 : pageSize; // 8 cho đẹp với giao diện
    
                DateTime? publicationDate = year > 0 ? new DateTime(year, 1, 1) : (DateTime?)null;
    
                // Chỉ lưu lịch sử khi có ít nhất 1 tiêu chí search
                bool hasCriteria = !string.IsNullOrWhiteSpace(title)
                    || !string.IsNullOrWhiteSpace(author)
                    || !string.IsNullOrWhiteSpace(category)
                    || year > 0;
    
                if (hasCriteria)
                {
                    try
                    {
                        var userIdStr = HttpContext.Session.GetString("UserId");
                        if (int.TryParse(userIdStr, out int userId))
                        {
                            int? categoryId = null;
                            if (!string.IsNullOrWhiteSpace(category))
                            {
                                var cat = await _categoryService.GetCategoryByNameAsync(category);
                                categoryId = cat?.CategoryId;
                            }
    
                            var historyDto = new SearchHistoryCreateDto
                            {
                                Title = string.IsNullOrWhiteSpace(title) ? null : title,
                                Author = string.IsNullOrWhiteSpace(author) ? null : author,
                                CategoryId = categoryId,
                                UserId = userId
                            };
    
                            await _searchHistoryService.AddHistoryAsync(historyDto);
                        }
                    }
                    catch
                    {
                        // Ignore log lỗi
                    }
                }
    
                // KHÔNG lấy tag dropdown ở đây, chỉ search theo input
                var (books, totalCount) = await _bookService.SearchBooksWithStatsPagedAsync(
                    title, author, category, publicationDate, page, pageSize, null // Tag luôn null
                );
    
                var result = books.Select((book, index) => new
                {
                    BookId = book.BookId,
                    Title = book.Title,
                    ImageBase64 = string.IsNullOrEmpty(book.ImageBase64) ? "/images/default-book.jpg" : book.ImageBase64,
                    PublicationDate = book.PublicationDate,
                    AuthorName = book.AuthorName ?? "Không rõ",
                    CategoryName = book.CategoryName ?? "Không rõ",
                    Tags = book.Tags ?? new List<string>(),
                    AverageRating = book.AverageRating,
                    RatingCount = book.RatingCount
                }).ToList();
    
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                return new JsonResult(new
                {
                    books = result,
                    totalPages = totalPages,
                    total = totalCount
                });
            }

            // Test endpoint to check database connection
            public async Task<JsonResult> OnGetTestAsync()
            {
                try
                {
                    var (books, bookCount) = await _bookService.SearchBooksAdminPagedAsync(null, null, null, null, 1, 5, null);
                    var authors = await _authorService.GetAllAuthorsAsync();
                    var categories = await _categoryService.GetAllCategoriesAsync();
                    
                    return new JsonResult(new 
                    { 
                        success = true,
                        message = "Database connection successful",
                        data = new 
                        {
                            books = bookCount,
                            authors = authors.Count(),
                            categories = categories.Count(),
                            
                        }
                    });
                }
                catch (Exception ex)
                {
                    return new JsonResult(new 
                    { 
                        success = false,
                        message = ex.Message,
                        error = ex.ToString()
                    });
                }
            }

            // New unified search handler for Index AJAX
            public async Task<JsonResult> OnGetSearchAsync()
            {
                var q = Request.Query;
                string? title = q["title"];
                string? author = q["author"];
                string? category = q["category"];
                string? tags = q["tag"]; // Support multiple tags comma-separated
                string? sort = q["sort"];
                string? filter = q["filter"]; // Support filter parameter
                int.TryParse(q["year"], out int year);
                int.TryParse(q["page"], out int page);
                int.TryParse(q["pageSize"], out int pageSize);

                // Debug logging - xóa sau khi fix
                Console.WriteLine($"OnGetSearchAsync called: title={title}, author={author}, category={category}, page={page}");

                page = page <= 0 ? 1 : page;
                pageSize = pageSize <= 0 ? 12 : pageSize;
                DateTime? publicationDate = year > 0 ? new DateTime(year, 1, 1) : (DateTime?)null;

                // Handle filter parameter first
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    switch (filter)
                    {
                        case "top-rated":
                            sort = "rating";
                            break;
                        case "trending":
                            sort = "favorites";
                            break;
                        case "most-searched":
                            sort = "searched";
                            break;
                    }
                }

                long totalCount = 0;

                // Handle tag filtering first
                if (!string.IsNullOrWhiteSpace(tags))
                {
                    var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(t => t.Trim())
                                    .Where(t => !string.IsNullOrEmpty(t))
                                    .ToList();

                    if (tagList.Any())
                    {
                        // For simplicity, use the first tag only for now
                        var firstTag = tagList.First();
                        var matchedTags = await _tagService.SearchTagsAsync(firstTag);
                        var tag = matchedTags.FirstOrDefault(t => t.Name.Equals(firstTag, StringComparison.OrdinalIgnoreCase));
                        
                        if (tag != null)
                        {
                            var (tagBooks, tagTotalCount) = await _bookTagService.GetBooksByTagPagedAsync(tag.TagId, page, pageSize);
                            totalCount = tagTotalCount;
                            
                            // Lưu lịch sử tìm kiếm tag (chỉ page 1)
                            if (page == 1)
                            {
                                await SaveSearchHistory(firstTag, null, null, 0, totalCount, 0);
                            }
                            
                            var items = tagBooks.Select(b => new
                            {
                                id = b.BookId,
                                title = b.Title,
                                author = b.AuthorName ?? "Không rõ",
                                coverUrl = string.IsNullOrEmpty(b.ImageBase64) ? "/images/default-book.jpg" : b.ImageBase64,
                                rating = 0.0, // Tag search doesn't include rating info
                                favorites = 0,
                                description = ""
                            }).ToList();

                            var totalPages = (int)Math.Ceiling(tagTotalCount / (double)pageSize);
                            return new JsonResult(new { items, totalPages, total = tagTotalCount });
                        }
                        else
                        {
                            return new JsonResult(new { items = new List<object>(), totalPages = 0, total = 0 });
                        }
                    }
                }

                // Regular search without tags
                var (books, bookCount) = await _bookService.SearchBooksAdminPagedAsync(
                    title, author, category, publicationDate, page, pageSize, null
                );
                totalCount = bookCount;

                // Lưu lịch sử tìm kiếm (chỉ page 1, có tiêu chí search, không phải chỉ filter hoặc paging)
                bool hasSearch = !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(author) || !string.IsNullOrWhiteSpace(category) || year > 0;
                if (page == 1 && hasSearch)
                {
                    await SaveSearchHistory(title, author, category, year, totalCount, null);
                }

                // Server-side sorting options
                IEnumerable<BookInfoFinder.Models.Dto.BookDto> ordered = books;
                if (!string.IsNullOrWhiteSpace(sort))
                {
                    switch (sort)
                    {
                        case "rating":
                            ordered = books.OrderByDescending(b => b.AverageRating).ThenByDescending(b => b.RatingCount);
                            break;
                        case "favorites":
                            ordered = books.OrderByDescending(b => b.TotalFavorites).ThenByDescending(b => b.AverageRating);
                            break;
                        case "searched":
                            // Fallback: approximate by rating count if no search metric
                            ordered = books.OrderByDescending(b => b.RatingCount).ThenByDescending(b => b.AverageRating);
                            break;
                        case "title":
                            ordered = books.OrderBy(b => b.Title);
                            break;
                        case "year":
                            ordered = books.OrderByDescending(b => b.PublicationDate);
                            break;
                        default:
                            break;
                    }
                }

                var resultItems = ordered.Select(b => new
                {
                    id = b.BookId,
                    title = b.Title,
                    author = b.AuthorName ?? "Không rõ",
                    coverUrl = string.IsNullOrEmpty(b.ImageBase64) ? "/images/default-book.jpg" : b.ImageBase64,
                    rating = Math.Round(b.AverageRating, 1),
                    favorites = b.TotalFavorites,
                    description = !string.IsNullOrEmpty(b.Description) && b.Description.Length > 100 
                        ? b.Description.Substring(0, 100) + "..." 
                        : b.Description ?? ""
                }).ToList();

                var finalTotalPages = (int)Math.Ceiling(bookCount / (double)pageSize);
                return new JsonResult(new { items = resultItems, totalPages = finalTotalPages, total = bookCount });
            }

            // Helper method để lưu lịch sử tìm kiếm
            private async Task SaveSearchHistory(string? title, string? author, string? category, int year, long resultCount, int? categoryId)
            {
                bool hasCriteria = !string.IsNullOrWhiteSpace(title)
                    || !string.IsNullOrWhiteSpace(author)
                    || !string.IsNullOrWhiteSpace(category)
                    || year > 0;

                // Debug logging - xóa sau khi fix
                Console.WriteLine($"SaveSearchHistory called: hasCriteria={hasCriteria}, title={title}, author={author}, category={category}");

                if (!hasCriteria) return;

                try
                {
                    var userIdStr = HttpContext.Session.GetString("UserId");
                    Console.WriteLine($"UserId from session: {userIdStr}");
                    
                    if (!int.TryParse(userIdStr, out int userId)) return;

                    // Tạo search query dựa trên các tiêu chí tìm kiếm
                    var searchParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(title)) searchParts.Add(title);
                    if (!string.IsNullOrWhiteSpace(author)) searchParts.Add($"tác giả: {author}");
                    if (!string.IsNullOrWhiteSpace(category)) searchParts.Add($"thể loại: {category}");
                    if (year > 0) searchParts.Add($"năm: {year}");

                    var searchQuery = string.Join(", ", searchParts);

                    // Lấy categoryId nếu chưa có
                    if (categoryId == null && !string.IsNullOrWhiteSpace(category))
                    {
                        var cat = await _categoryService.GetCategoryByNameAsync(category);
                        categoryId = cat?.CategoryId;
                    }

                    // Luôn tìm BookId nếu có title
                    int? bookId = null;
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        var (books, totalCount) = await _bookService.SearchBooksAdminPagedAsync(
                            title, null, null, null, 1, 1, null
                        );
                        var exactMatch = books.FirstOrDefault(b => b.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
                        if (exactMatch != null)
                        {
                            bookId = exactMatch.BookId;
                            Console.WriteLine($"Found BookId: {bookId} for title: {title}");
                        }
                    }

                    var historyDto = new SearchHistoryCreateDto
                    {
                        SearchQuery = searchQuery,
                        Title = string.IsNullOrWhiteSpace(title) ? null : title,
                        Author = string.IsNullOrWhiteSpace(author) ? null : author,
                        CategoryId = categoryId,
                        UserId = userId,
                        ResultCount = (int)resultCount,
                        BookId = bookId
                    };

                    await _searchHistoryService.AddHistoryAsync(historyDto);
                }
                catch
                {
                    // Ignore lỗi lưu lịch sử
                }
            }

            // Endpoint riêng để lưu lịch sử tìm kiếm
            public async Task<JsonResult> OnPostSaveSearchHistoryAsync()
            {
                try
                {
                    var query = Request.Form["query"].ToString();
                    var type = Request.Form["type"].ToString();
                    var resultCount = int.Parse(Request.Form["resultCount"].ToString());

                    var userIdStr = HttpContext.Session.GetString("UserId");
                    if (!int.TryParse(userIdStr, out int userId))
                    {
                        return new JsonResult(new { success = false, message = "User not logged in" });
                    }

                    var historyDto = new SearchHistoryCreateDto
                    {
                        SearchQuery = query,
                        UserId = userId,
                        ResultCount = resultCount
                    };

                    // Set specific fields based on type
                    switch (type)
                    {
                        case "title":
                            historyDto.Title = query;
                            break;
                        case "author":
                            historyDto.Author = query;
                            break;
                        case "category":
                            var cat = await _categoryService.GetCategoryByNameAsync(query);
                            historyDto.CategoryId = cat?.CategoryId;
                            break;
                    }

                    await _searchHistoryService.AddHistoryAsync(historyDto);
                    return new JsonResult(new { success = true });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving search history: {ex.Message}");
                    return new JsonResult(new { success = false, message = ex.Message });
                }
            }

            // Get tag counts for display
            public async Task<JsonResult> OnGetTagCountsAsync()
            {
                try
                {
                    var tags = await _tagService.GetAllTagsAsync();
                    var tagCounts = new List<object>();

                    foreach (var tag in tags)
                    {
                        var (books, total) = await _bookTagService.GetBooksByTagPagedAsync(tag.TagId, 1, 1);
                        tagCounts.Add(new { name = tag.Name, count = total });
                    }

                    return new JsonResult(tagCounts);
                }
                catch (Exception ex)
                {
                    return new JsonResult(new { error = ex.Message });
                }
            }

            // Chatbot handler
            public async Task<JsonResult> OnPostChatAsync(string message)
            {
                // Use user session or create a persistent one for the conversation
                var sessionId = HttpContext.Session.GetString("ChatSessionId");
                if (string.IsNullOrEmpty(sessionId))
                {
                    sessionId = Guid.NewGuid().ToString();
                    HttpContext.Session.SetString("ChatSessionId", sessionId);
                }

                var reply = await _chatbotService.GetChatbotReplyAsync(message, sessionId);
                return new JsonResult(new { reply });
            }
        }
    }