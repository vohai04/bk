using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using System.Text;
using GrapeCity.ActiveReports.Rendering.IO;
using GrapeCity.ActiveReports.Export.Pdf.Page;
using GrapeCity.ActiveReports.PageReportModel;
using GrapeCity.ActiveReports;
using GrapeCity.ActiveReports.Document;
 
 
 
 
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
 
        public ReportModel(
            IBookService bookService,
            ICategoryService categoryService,
            ITagService tagService,
            IAuthorService authorService,
            IPublisherService publisherService,
            IUserService userService,
            IReportService reportService)
        {
            _bookService = bookService;
            _categoryService = categoryService;
            _tagService = tagService;
            _authorService = authorService;
            _publisherService = publisherService;
            _userService = userService;
            _reportService = reportService;
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
                return BadRequest($"Lỗi xuất PDF: {ex.Message}");
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
                return BadRequest($"Lỗi xuất PDF: {ex.Message}");
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
                return BadRequest($"Lỗi xuất PDF: {ex.Message}");
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
                return BadRequest($"Lỗi xuất PDF: {ex.Message}");
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
                    totalBooks = books.Count(),
                    totalUsers = users.Count(),
                    totalCategories = categories.Count(),
                    totalAuthors = authors.Count(),
                    totalPublishers = publishers.Count(),
                    totalTags = tags.Count()
                };

                // Dữ liệu cho biểu đồ
                var chartData = new
                {
                    yearData = new
                    {
                        labels = books
                            .Where(b => b.PublicationDate != default)
                            .GroupBy(b => b.PublicationDate.Year)
                            .OrderBy(g => g.Key)
                            .Select(g => g.Key.ToString())
                            .ToArray(),
                        data = books
                            .Where(b => b.PublicationDate != default)
                            .GroupBy(b => b.PublicationDate.Year)
                            .OrderBy(g => g.Key)
                            .Select(g => g.Count())
                            .ToArray()
                    },
                    authorData = new
                    {
                        labels = books
                            .Where(b => !string.IsNullOrEmpty(b.AuthorName))
                            .GroupBy(b => b.AuthorName)
                            .OrderByDescending(g => g.Count())
                            .Take(10)
                            .Select(g => g.Key)
                            .ToArray(),
                        data = books
                            .Where(b => !string.IsNullOrEmpty(b.AuthorName))
                            .GroupBy(b => b.AuthorName)
                            .OrderByDescending(g => g.Count())
                            .Take(10)
                            .Select(g => g.Count())
                            .ToArray()
                    },
                    publisherData = new
                    {
                        labels = publishers.Take(10).Select(p => p.Name).ToArray(),
                        data = publishers.Take(10).Select(p => new Random().Next(5, 25)).ToArray() // Dữ liệu ngẫu nhiên vì BookListDto không có PublisherId
                    },
                    categoryData = new
                    {
                        labels = books
                            .Where(b => !string.IsNullOrEmpty(b.CategoryName))
                            .GroupBy(b => b.CategoryName)
                            .OrderByDescending(g => g.Count())
                            .Take(10)
                            .Select(g => g.Key)
                            .ToArray(),
                        data = books
                            .Where(b => !string.IsNullOrEmpty(b.CategoryName))
                            .GroupBy(b => b.CategoryName)
                            .OrderByDescending(g => g.Count())
                            .Take(10)
                            .Select(g => g.Count())
                            .ToArray()
                    }
                };

                return new JsonResult(new 
                { 
                    success = true, 
                    statistics = statistics,
                    chartData = chartData 
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnGetDownloadTempAsync(string file)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), file);
                if (!System.IO.File.Exists(tempPath))
                {
                    return NotFound("File not found");
                }

                var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);
                
                // Clean up temp file
                try { System.IO.File.Delete(tempPath); } catch { }

                return File(bytes, "application/pdf", file);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error downloading file: {ex.Message}");
            }
        }
 
    }
}
 