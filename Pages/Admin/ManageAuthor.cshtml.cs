using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using BookInfoFinder.Data;
 
namespace BookInfoFinder.Pages.Admin
{
    public class ManageAuthorModel : PageModel
    {
        private readonly IAuthorService _authorService;
        private readonly BookContext _context;

        public ManageAuthorModel(IAuthorService authorService, BookContext context)
        {
            _authorService = authorService;
            _context = context;
        }

        public List<AuthorDto> Authors { get; set; } = new();

        [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }

        public async Task OnGetAsync(int page = 1)
        {
            CurrentPage = page < 1 ? 1 : page;
            int pageSize = 10;
            
            var result = await _authorService.GetAuthorsPagedAsync(CurrentPage, pageSize);
            Authors = result.Authors;
            TotalCount = result.TotalCount;
            TotalPages = (int)Math.Ceiling((double)TotalCount / pageSize);
        }
        // AJAX
        public async Task<JsonResult> OnGetAjaxSearchAsync()
        {
            try
            {
                var query = Request.Query;
                string search = query["search"].ToString() ?? "";
                int.TryParse(query["page"], out int page);
                int.TryParse(query["pageSize"], out int pageSize);

                page = page <= 0 ? 1 : page;
                pageSize = pageSize <= 0 ? 10 : pageSize;

                var result = await _authorService.GetAuthorsPagedAsync(page, pageSize);
                var filteredAuthors = result.Authors;

                // Apply search filter if provided
                if (!string.IsNullOrWhiteSpace(search))
                {
                    filteredAuthors = (await _authorService.SearchAuthorsAsync(search))
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();
                }

                var authorResult = filteredAuthors.Select(a => new {
                    a.AuthorId,
                    a.Name,
                    a.Biography,
                    a.DateOfBirth,
                    DateOfBirthFormatted = a.DateOfBirth?.ToString("dd/MM/yyyy") ?? "",
                    a.Nationality,
                    a.BookCount,
                    a.CreatedAt,
                    CreatedAtFormatted = a.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                });

                var totalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize);
                return new JsonResult(new { authors = authorResult, totalPages, totalCount = result.TotalCount });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxAddAsync([FromForm] string name, [FromForm] string biography, [FromForm] string dateOfBirth, [FromForm] string nationality)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
                    return new JsonResult(new { success = false, message = "Tên tác giả không hợp lệ." });

                if (await _authorService.IsAuthorNameExistsAsync(name.Trim()))
                    return new JsonResult(new { success = false, message = "Tên tác giả đã tồn tại." });

                // Parse dateOfBirth if provided
                DateTime? parsedDateOfBirth = null;
                if (!string.IsNullOrWhiteSpace(dateOfBirth))
                {
                    if (DateTime.TryParseExact(dateOfBirth, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime dob))
                    {
                        parsedDateOfBirth = DateTime.SpecifyKind(dob, DateTimeKind.Utc);
                    }
                    else if (DateTime.TryParse(dateOfBirth, out dob))
                    {
                        parsedDateOfBirth = DateTime.SpecifyKind(dob, DateTimeKind.Utc);
                    }
                }

                var authorCreateDto = new AuthorCreateDto 
                { 
                    Name = name.Trim(), 
                    Biography = biography?.Trim() ?? string.Empty,
                    DateOfBirth = parsedDateOfBirth,
                    Nationality = nationality?.Trim() ?? string.Empty
                };
                var createdAuthor = await _authorService.CreateAuthorAsync(authorCreateDto);
                
                return new JsonResult(new { 
                    success = true, 
                    author = new { 
                        createdAuthor.AuthorId, 
                        createdAuthor.Name, 
                        createdAuthor.Biography,
                        createdAuthor.DateOfBirth,
                        DateOfBirthFormatted = createdAuthor.DateOfBirth?.ToString("dd/MM/yyyy") ?? "",
                        createdAuthor.Nationality,
                        createdAuthor.BookCount,
                        createdAuthor.CreatedAt,
                        CreatedAtFormatted = createdAuthor.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostAjaxEditAsync(int authorId, string name, string dateOfBirth, string nationality, string biography)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return new JsonResult(new { success = false, message = "Tên tác giả không được để trống" });
                }

                var author = await _context.Authors.FindAsync(authorId);
                if (author == null)
                {
                    return new JsonResult(new { success = false, message = "Không tìm thấy tác giả" });
                }

                // Parse dateOfBirth if provided
                DateTime? parsedDateOfBirth = null;
                if (!string.IsNullOrEmpty(dateOfBirth))
                {
                    if (DateTime.TryParseExact(dateOfBirth, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime dob))
                    {
                        parsedDateOfBirth = DateTime.SpecifyKind(dob, DateTimeKind.Utc);
                    }
                    else if (DateTime.TryParse(dateOfBirth, out dob))
                    {
                        parsedDateOfBirth = DateTime.SpecifyKind(dob, DateTimeKind.Utc);
                    }
                }

                author.Name = name;
                author.DateOfBirth = parsedDateOfBirth;
                author.Nationality = nationality ?? string.Empty;
                author.Biography = biography ?? string.Empty;
                author.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = "Đã xảy ra lỗi: " + ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxDeleteAsync([FromForm] int authorId)
        {
            try
            {
                var success = await _authorService.DeleteAuthorAsync(authorId);
                return new JsonResult(new { success });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }
}