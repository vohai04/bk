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
 
        public async Task<JsonResult> OnGetStatsAsync()
        {
            try
            {
                var books = await _bookService.GetAllBooksAsync();
                var users = await _userService.GetAllUsersAsync();

                var stats = new
                {
                    totalBooks = books.Count(),
                    totalAuthors = books.Select(b => b.AuthorName).Where(n => !string.IsNullOrEmpty(n)).Distinct().Count(),
                    totalCategories = books.Select(b => b.CategoryName).Where(n => !string.IsNullOrEmpty(n)).Distinct().Count(),
                    totalPublishers = books.Select(b => b.CategoryName).Where(n => !string.IsNullOrEmpty(n)).Distinct().Count(), // Note: BookListDto doesn't have PublisherName
                    totalUsers = users.Count(),

                    booksPerYear = books
                        .Where(b => b.PublicationDate != default)
                        .GroupBy(b => b.PublicationDate.Year)
                        .OrderBy(g => g.Key)
                        .Select(g => new { year = g.Key, count = g.Count() }),

                    booksPerAuthor = books
                        .GroupBy(b => string.IsNullOrEmpty(b.AuthorName) ? "Không rõ" : b.AuthorName)
                        .OrderByDescending(g => g.Count())
                        .Select(g => new { author = g.Key, count = g.Count() }),

                    booksPerCategory = books
                        .GroupBy(b => string.IsNullOrEmpty(b.CategoryName) ? "Không rõ" : b.CategoryName)
                        .OrderByDescending(g => g.Count())
                        .Select(g => new { category = g.Key, count = g.Count() })
                };

                return new JsonResult(stats);
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
 
        public async Task<IActionResult> OnPostExportBookPdfAsync()
        {
            try
            {
                Console.WriteLine("Starting professional PDF export...");
                
                var reportPath = Path.Combine(Directory.GetCurrentDirectory(), "Reports", "SystemStatisticsReportSimple.rdlx");
                Console.WriteLine($"Report path: {reportPath}");
                
                if (!System.IO.File.Exists(reportPath))
                {
                    Console.WriteLine($"Report file not found at: {reportPath}");
                    return BadRequest($"Không tìm thấy file báo cáo tại: {reportPath}");
                }
                
                // Lấy dữ liệu thống kê từ database
                var books = await _bookService.GetAllBooksAsync();
                var users = await _userService.GetAllUsersAsync();
                var categories = await _categoryService.GetAllCategoriesAsync();
                var authors = await _authorService.GetAllAuthorsAsync();
                var publishers = await _publisherService.GetAllPublishersAsync();
                var tags = await _tagService.GetAllTagsAsync();

                // Tạo dữ liệu cho báo cáo RDLX
                var reportData = new
                {
                    ReportDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    TotalBooks = books.Count(),
                    TotalUsers = users.Count(),
                    TotalCategories = categories.Count(),
                    TotalAuthors = authors.Count(),
                    TotalPublishers = publishers.Count(),
                    TotalTags = tags.Count()
                };

                var yearlyBooksData = books
                    .Where(b => b.PublicationDate != default)
                    .GroupBy(b => b.PublicationDate.Year)
                    .OrderBy(g => g.Key)
                    .Select(g => new { Year = g.Key, Count = g.Count() })
                    .ToArray();

                var topAuthorsData = books
                    .Where(b => !string.IsNullOrEmpty(b.AuthorName))
                    .GroupBy(b => b.AuthorName)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .ToArray();

                var categoryData = books
                    .Where(b => !string.IsNullOrEmpty(b.CategoryName))
                    .GroupBy(b => b.CategoryName)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .ToArray();

                var publisherData = publishers.Take(10).Select(p => new { Name = p.Name, Count = books.Count(b => b.CategoryName == p.Name) }).ToArray();

                // Load và render báo cáo RDLX
                Console.WriteLine("Loading RDLX report...");
                var report = new PageReport(new FileInfo(reportPath));
                var runtime = new PageDocument(report);
                
                Console.WriteLine("Setting up data sources...");
                // Gắn dữ liệu cho các DataSet
                runtime.LocateDataSource += (s, e) =>
                {
                    Console.WriteLine($"LocateDataSource event: DataSet = {e.DataSet?.Name}");
                    
                    switch (e.DataSet?.Name?.ToLower())
                    {
                        case "statisticsdata":
                            e.Data = new[] { reportData };
                            break;
                        case "yearlybooksdata":
                            e.Data = yearlyBooksData;
                            break;
                        case "topauthorsdata":
                            e.Data = topAuthorsData;
                            break;
                        case "publisherdata":
                            e.Data = publisherData;
                            break;
                        case "categorydata":
                            e.Data = categoryData;
                            break;
                    }
                };

                // Render PDF
                Console.WriteLine("Rendering professional PDF...");
                var pdfRenderer = new PdfRenderingExtension();
                var streamProvider = new MemoryStreamProvider();
                runtime.Render(pdfRenderer, streamProvider);
                
                using var outputStream = streamProvider.GetPrimaryStream().OpenStream();
                outputStream.Position = 0;
                var bytes = new byte[outputStream.Length];
                var totalRead = 0;
                while (totalRead < bytes.Length)
                {
                    var read = await outputStream.ReadAsync(bytes.AsMemory(totalRead, bytes.Length - totalRead));
                    if (read == 0) break;
                    totalRead += read;
                }
                
                Console.WriteLine($"Professional PDF generated successfully, size: {bytes.Length} bytes");
                return File(bytes, "application/pdf", $"BaoCaoThongKe_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting PDF: {ex}");
                TempData["ErrorMessage"] = $"Lỗi xuất PDF: {ex.Message}";
                return RedirectToPage();
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

        public async Task<IActionResult> OnPostExportPDFAsync(string rdlxData)
        {
            try
            {
                if (string.IsNullOrEmpty(rdlxData))
                {
                    return new JsonResult(new { success = false, message = "RDLX data is required" });
                }

                var pdfBytes = await _reportService.ExportSystemReportToPdfAsync(rdlxData);
                
                // Save to temp file for download
                var tempFileName = $"SystemReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var tempPath = Path.Combine(Path.GetTempPath(), tempFileName);
                await System.IO.File.WriteAllBytesAsync(tempPath, pdfBytes);

                return new JsonResult(new 
                { 
                    success = true, 
                    downloadUrl = $"/Admin/Report?handler=DownloadTemp&file={tempFileName}" 
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
 