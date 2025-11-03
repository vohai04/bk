using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
 
 
 
 
namespace BookInfoFinder.Pages.Admin
{
    public class ReportModel : PageModel
    {
    private readonly IBookService _bookService;
    private readonly ICategoryService _categoryService;
    private readonly ITagService _tagService;
    private readonly IAuthorService _authorService;
    private readonly IPublisherService _publisherService;
    private readonly IUserService _userService;
    private readonly IReportService _reportService;
    private readonly ILogger<ReportModel> _logger;
 
        public ReportModel(
            IBookService bookService,
            ICategoryService categoryService,
            ITagService tagService,
            IAuthorService authorService,
            IPublisherService publisherService,
            IUserService userService,
            IReportService reportService,
            ILogger<ReportModel> logger)
        {
            _bookService = bookService;
            _categoryService = categoryService;
            _tagService = tagService;
            _authorService = authorService;
            _publisherService = publisherService;
            _userService = userService;
            _reportService = reportService;
            _logger = logger;
        }
 
    public void OnGet() { }

        public async Task<IActionResult> OnPostExportTodayPdfAsync()
        {
            try
            {
                var pdfBytes = await _reportService.ExportTodayReportToPdfAsync();
                return File(pdfBytes, "application/pdf", $"BaoCao_HomNay_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ExportTodayPdf failed");
                return BadRequest(new { success = false, message = "Lỗi xuất PDF: " + ex.Message });
            }
        }

        public async Task<IActionResult> OnPostExportWeekPdfAsync()
        {
            try
            {
                var pdfBytes = await _reportService.ExportWeekReportToPdfAsync();
                return File(pdfBytes, "application/pdf", $"BaoCao_TuanNay_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ExportWeekPdf failed");
                return BadRequest(new { success = false, message = "Lỗi xuất PDF: " + ex.Message });
            }
        }

        public async Task<IActionResult> OnPostExportMonthPdfAsync()
        {
            try
            {
                var pdfBytes = await _reportService.ExportMonthReportToPdfAsync();
                return File(pdfBytes, "application/pdf", $"BaoCao_ThangNay_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ExportMonthPdf failed");
                return BadRequest(new { success = false, message = "Lỗi xuất PDF: " + ex.Message });
            }
        }

        public async Task<IActionResult> OnPostExportYearPdfAsync()
        {
            try
            {
                var pdfBytes = await _reportService.ExportYearReportToPdfAsync();
                return File(pdfBytes, "application/pdf", $"BaoCao_NamNay_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ExportYearPdf failed");
                return BadRequest(new { success = false, message = "Lỗi xuất PDF: " + ex.Message });
            }
        }

        // RDLX Report handlers
        public async Task<JsonResult> OnGetGetStatisticsAsync()
        {
            try
            {
                var books = await _bookService.GetAllBooksAsync();
                var users = await _userService.GetAllUsersAsync();
                var categories = await _categoryService.GetAllCategoriesAsync();
                var authors = await _authorService.GetAllAuthorsAsync();
                var publishers = await _publisherService.GetAllPublishersAsync();
                var tags = await _tagService.GetAllTagsAsync();

                var statistics = new
                {
                    totalBooks = books?.Count() ?? 0,
                    totalUsers = users?.Count() ?? 0,
                    totalCategories = categories?.Count() ?? 0,
                    totalAuthors = authors?.Count() ?? 0,
                    totalPublishers = publishers?.Count() ?? 0,
                    totalTags = tags?.Count() ?? 0
                };

                // Basic chart data safe construction
                var yearGroups = (books ?? Enumerable.Empty<BookListDto>())
                    .Where(b => b.PublicationDate != default(DateTime))
                    .GroupBy(b => b.PublicationDate.Year)
                    .OrderBy(g => g.Key)
                    .Select(g => new { Label = g.Key.ToString(), Count = g.Count() })
                    .ToList();

                var chartData = new
                {
                    yearData = new
                    {
                        labels = yearGroups.Select(g => g.Label).ToArray(),
                        data = yearGroups.Select(g => g.Count).ToArray()
                    },
                    authorData = new
                    {
                        labels = (books ?? Enumerable.Empty<BookListDto>())
                            .Where(b => !string.IsNullOrEmpty(b.AuthorName))
                            .GroupBy(b => b.AuthorName)
                            .OrderByDescending(g => g.Count())
                            .Take(10)
                            .Select(g => g.Key)
                            .ToArray(),
                        data = (books ?? Enumerable.Empty<BookListDto>())
                            .Where(b => !string.IsNullOrEmpty(b.AuthorName))
                            .GroupBy(b => b.AuthorName)
                            .OrderByDescending(g => g.Count())
                            .Take(10)
                            .Select(g => g.Count())
                            .ToArray()
                    },
                    publisherData = new
                    {
                        labels = (publishers ?? Enumerable.Empty<dynamic>()).Take(10).Select(p => p.Name).ToArray(),
                        data = (publishers ?? Enumerable.Empty<dynamic>()).Take(10).Select(p => 1).ToArray()
                    },
                    categoryData = new
                    {
                        labels = (books ?? Enumerable.Empty<BookListDto>())
                            .Where(b => !string.IsNullOrEmpty(b.CategoryName))
                            .GroupBy(b => b.CategoryName)
                            .OrderByDescending(g => g.Count())
                            .Take(10)
                            .Select(g => g.Key)
                            .ToArray(),
                        data = (books ?? Enumerable.Empty<BookListDto>())
                            .Where(b => !string.IsNullOrEmpty(b.CategoryName))
                            .GroupBy(b => b.CategoryName)
                            .OrderByDescending(g => g.Count())
                            .Take(10)
                            .Select(g => g.Count())
                            .ToArray()
                    }
                };

                return new JsonResult(new { success = true, statistics, chartData });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetStatistics failed");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // Note: Temporary file download helper removed - exports stream PDF directly to client via POST handlers
 
    }
}
 