using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BookInfoFinder.Services
{
    public class ReportService : IReportService
    {
        private readonly BookContext _context;
        private readonly ILogger<ReportService> _logger;
        private readonly IConfiguration _configuration;

        public ReportService(BookContext context, ILogger<ReportService> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<DashboardReportDto> GetDashboardReportAsync()
        {
            try
            {
                var totalBooks = await _context.Books.CountAsync();
                var totalCategories = await _context.Categories.CountAsync();
                var totalTags = await _context.Tags.CountAsync();
                var totalAuthors = await _context.Authors.CountAsync();
                var totalPublishers = await _context.Publishers.CountAsync();
                var totalUsers = await _context.Users.CountAsync();
                var totalComments = await _context.BookComments.CountAsync();
                var totalFavorites = await _context.Favorites.CountAsync();
                var totalRatings = await _context.Ratings.CountAsync();
                
                var averageRating = totalRatings > 0 
                    ? Math.Round(await _context.Ratings.AverageAsync(r => r.Star), 2)
                    : 0;

                return new DashboardReportDto
                {
                    TotalBooks = totalBooks,
                    TotalCategories = totalCategories,
                    TotalTags = totalTags,
                    TotalAuthors = totalAuthors,
                    TotalPublishers = totalPublishers,
                    TotalUsers = totalUsers,
                    TotalComments = totalComments,
                    TotalFavorites = totalFavorites,
                    TotalRatings = totalRatings,
                    AverageRating = averageRating,
                    ReportGeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard report");
                throw;
            }
        }

        public async Task<DashboardReportDto> GetDashboardReportByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Books don't have CreatedAt, so use PublicationDate as filter
                var totalBooks = await _context.Books
                    .Where(b => b.PublicationDate >= startDate && b.PublicationDate <= endDate)
                    .CountAsync();
                
                var totalCategories = await _context.Categories
                    .Where(c => c.CreatedAt >= startDate && c.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalTags = await _context.Tags
                    .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalAuthors = await _context.Authors
                    .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalPublishers = await _context.Publishers
                    .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalUsers = await _context.Users
                    .Where(u => u.CreatedAt >= startDate && u.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalComments = await _context.BookComments
                    .Where(c => c.CreatedAt >= startDate && c.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalFavorites = await _context.Favorites
                    .Where(f => f.CreatedAt >= startDate && f.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalRatings = await _context.Ratings
                    .Where(r => r.CreatedAt >= startDate && r.CreatedAt <= endDate)
                    .CountAsync();
                
                var averageRating = totalRatings > 0 
                    ? Math.Round(await _context.Ratings
                        .Where(r => r.CreatedAt >= startDate && r.CreatedAt <= endDate)
                        .AverageAsync(r => r.Star), 2)
                    : 0;

                return new DashboardReportDto
                {
                    TotalBooks = totalBooks,
                    TotalCategories = totalCategories,
                    TotalTags = totalTags,
                    TotalAuthors = totalAuthors,
                    TotalPublishers = totalPublishers,
                    TotalUsers = totalUsers,
                    TotalComments = totalComments,
                    TotalFavorites = totalFavorites,
                    TotalRatings = totalRatings,
                    AverageRating = averageRating,
                    ReportGeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard report by date range");
                throw;
            }
        }

        public async Task<List<AuthorStatisticsDto>> GetAuthorStatisticsAsync()
        {
            try
            {
                var authors = await _context.Authors
                    .Include(a => a.Books)
                        .ThenInclude(b => b.Ratings)
                    .Include(a => a.Books)
                        .ThenInclude(b => b.Favorites)
                    .ToListAsync();

                return authors.Select(author =>
                {
                    var bookCount = author.Books.Count;
                    var allRatings = author.Books.SelectMany(b => b.Ratings).ToList();
                    var totalRatings = allRatings.Count;
                    var averageRating = totalRatings > 0 ? Math.Round(allRatings.Average(r => r.Star), 2) : 0;
                    var totalFavorites = author.Books.SelectMany(b => b.Favorites).Count();

                    return new AuthorStatisticsDto
                    {
                        AuthorId = author.AuthorId,
                        AuthorName = author.Name,
                        BookCount = bookCount,
                        AverageRating = averageRating,
                        TotalRatings = totalRatings,
                        TotalFavorites = totalFavorites
                    };
                }).OrderByDescending(a => a.BookCount).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting author statistics");
                return new List<AuthorStatisticsDto>();
            }
        }

        public async Task<byte[]> ExportTodayReportToPdfAsync()
        {
            try
            {
                var endDate = DateTime.UtcNow;
                var startDate = endDate.Date;
                var dashboardData = await GetDashboardReportByDateRangeAsync(startDate, endDate);

                return GenerateDashboardPdf(dashboardData, "Báo cáo thống kê hôm nay", startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting today report to PDF");
                throw;
            }
        }

        public async Task<byte[]> ExportWeekReportToPdfAsync()
        {
            try
            {
                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddDays(-7);
                var dashboardData = await GetDashboardReportByDateRangeAsync(startDate, endDate);

                return GenerateDashboardPdf(dashboardData, "Báo cáo thống kê tuần này", startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting week report to PDF");
                throw;
            }
        }

        public async Task<byte[]> ExportMonthReportToPdfAsync()
        {
            try
            {
                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddMonths(-1);
                var dashboardData = await GetDashboardReportByDateRangeAsync(startDate, endDate);

                return GenerateDashboardPdf(dashboardData, "Báo cáo thống kê tháng này", startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting month report to PDF");
                throw;
            }
        }

        public async Task<byte[]> ExportYearReportToPdfAsync()
        {
            try
            {
                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddYears(-1);
                var dashboardData = await GetDashboardReportByDateRangeAsync(startDate, endDate);

                return GenerateDashboardPdf(dashboardData, "Báo cáo thống kê năm nay", startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting year report to PDF");
                throw;
            }
        }

        private byte[] GenerateDashboardPdf(DashboardReportDto data, string title, DateTime startDate, DateTime endDate)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header()
                        .Text(title)
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium)
                        .AlignCenter();

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(20);

                            column.Item().Text($"Thời gian: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}")
                                .FontSize(12).FontColor(Colors.Grey.Medium);

                            column.Item().Text("TỔNG QUAN HỆ THỐNG")
                                .SemiBold().FontSize(16).FontColor(Colors.Black);

                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(Block).Text("Chỉ số").SemiBold();
                                    header.Cell().Element(Block).Text("Giá trị").SemiBold();
                                });

                                table.Cell().Element(Block).Text("Tổng số sách");
                                table.Cell().Element(Block).Text(data.TotalBooks.ToString());

                                table.Cell().Element(Block).Text("Tác giả");
                                table.Cell().Element(Block).Text(data.TotalAuthors.ToString());

                                table.Cell().Element(Block).Text("Thể loại");
                                table.Cell().Element(Block).Text(data.TotalCategories.ToString());

                                table.Cell().Element(Block).Text("Nhà xuất bản");
                                table.Cell().Element(Block).Text(data.TotalPublishers.ToString());

                                table.Cell().Element(Block).Text("Người dùng");
                                table.Cell().Element(Block).Text(data.TotalUsers.ToString());

                                table.Cell().Element(Block).Text("Tags");
                                table.Cell().Element(Block).Text(data.TotalTags.ToString());

                                table.Cell().Element(Block).Text("Bình luận");
                                table.Cell().Element(Block).Text(data.TotalComments.ToString());

                                table.Cell().Element(Block).Text("Yêu thích");
                                table.Cell().Element(Block).Text(data.TotalFavorites.ToString());

                                table.Cell().Element(Block).Text("Đánh giá");
                                table.Cell().Element(Block).Text(data.TotalRatings.ToString());

                                table.Cell().Element(Block).Text("Điểm trung bình");
                                table.Cell().Element(Block).Text($"{data.AverageRating:F2}/5");
                            });

                            column.Item().Text($"Tạo báo cáo: {data.ReportGeneratedAt:dd/MM/yyyy HH:mm:ss}")
                                .FontSize(10).FontColor(Colors.Grey.Medium)
                                .AlignRight();
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text("BookInfoFinder - Hệ thống quản lý sách")
                        .FontSize(10).FontColor(Colors.Grey.Medium);
                });
            });

            return document.GeneratePdf();
        }

        static IContainer Block(IContainer container)
        {
            return container
                .Border(1)
                .Background(Colors.Grey.Lighten3)
                .Padding(5);
        }
    }
}