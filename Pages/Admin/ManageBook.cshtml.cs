using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using System.Text;
 
namespace BookInfoFinder.Pages.Admin
{
    public class ManageBookModel : PageModel
    {
        private readonly IBookService _bookService;
        private readonly ICategoryService _categoryService;
        private readonly IBookTagService _bookTagService;

        public List<CategoryDto> Categories { get; set; } = new();

        public ManageBookModel(
            IBookService bookService,
            ICategoryService categoryService,
            IBookTagService bookTagService)
        {
            _bookService = bookService;
            _categoryService = categoryService;
            _bookTagService = bookTagService;
        }

        public async Task OnGetAsync()
        {
            try
            {
                Categories = await _categoryService.GetAllCategoriesAsync();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi khi tải dữ liệu: {ex.Message}";
            }
        }

        public async Task<JsonResult> OnGetAjaxSearchAsync()
        {
            try
            {
                var query = Request.Query;
                string? search = query["search"].ToString();
                int.TryParse(query["category"], out int categoryId);
                int.TryParse(query["page"], out int page);
                int.TryParse(query["pageSize"], out int pageSize);

                page = page <= 0 ? 1 : page;
                pageSize = pageSize <= 0 ? 6 : pageSize;
                
                string? categoryName = null;
                if (categoryId > 0)
                {
                    var selectedCategory = await _categoryService.GetCategoryByIdAsync(categoryId);
                    categoryName = selectedCategory?.Name;
                }
                
                var (books, totalCount) = await _bookService.SearchBooksAdminPagedAsync(
                    search, null, categoryName, null, page, pageSize, null);

                var result = books.Select(b => new
                {
                    b.BookId,
                    b.Title,
                    b.ISBN,
                    b.Description,
                    b.Abstract,
                    ImageBase64 = b.ImageBase64,
                    PublicationDate = b.PublicationDate.ToString("yyyy-MM-dd"),
                    PublicationYear = b.PublicationDate.Year,
                    Author = new { Name = b.AuthorName },
                    Category = new { Name = b.CategoryName },
                    Publisher = new { Name = b.PublisherName },
                    Tags = b.Tags?.Select(t => t.Name).ToList() ?? new List<string>()
                }).ToList();

                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                return new JsonResult(new { books = result, totalPages, totalCount });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostExportCsvAsync()
        {
            try
            {
                string? search = Request.Form["Search"];
                string? categoryStr = Request.Form["Category"];
                int.TryParse(categoryStr, out int categoryId);
                
                string? categoryName = null;
                if (categoryId > 0)
                {
                    var selectedCategory = await _categoryService.GetCategoryByIdAsync(categoryId);
                    categoryName = selectedCategory?.Name;
                }
                
                var (books, _) = await _bookService.SearchBooksAdminPagedAsync(
                    search, null, categoryName, null, 1, int.MaxValue, null);

                var csv = new StringBuilder();
                csv.AppendLine("Tiêu đề,ISBN,Tác giả,Thể loại,NXB,Năm XB,Mô tả,Tóm tắt,Tag");
                
                foreach (var book in books)
                {
                    var tags = string.Join(";", book.Tags?.Select(t => t.Name) ?? new List<string>());
                    string CleanCsv(string input) => (input ?? "").Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", " ");
                    
                    csv.AppendLine(
                        $"\"{CleanCsv(book.Title)}\",\"{CleanCsv(book.ISBN)}\",\"{CleanCsv(book.AuthorName)}\",\"{CleanCsv(book.CategoryName)}\",\"{CleanCsv(book.PublisherName)}\",\"{book.PublicationDate.Year}\",\"{CleanCsv(book.Description)}\",\"{CleanCsv(book.Abstract)}\",\"{CleanCsv(tags)}\""
                    );
                }
                
                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                var fileName = $"DanhSachSach_{DateTime.Now:yyyyMMdd}.csv";
                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi khi xuất file: {ex.Message}";
                return RedirectToPage();
            }
        }
    }
}