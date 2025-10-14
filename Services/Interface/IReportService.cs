using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface IReportService
    {
        // Dashboard statistics
        Task<DashboardReportDto> GetDashboardReportAsync();
        Task<DashboardReportDto> GetDashboardReportByDateRangeAsync(DateTime startDate, DateTime endDate);
        
        // QuestPDF export methods
        Task<byte[]> ExportTodayReportToPdfAsync();
        Task<byte[]> ExportWeekReportToPdfAsync();
        Task<byte[]> ExportMonthReportToPdfAsync();
        Task<byte[]> ExportYearReportToPdfAsync();
    }
}

namespace BookInfoFinder.Models.Dto
{
    public class DashboardReportDto
    {
        public int TotalBooks { get; set; }
        public int TotalCategories { get; set; }
        public int TotalTags { get; set; }
        public int TotalAuthors { get; set; }
        public int TotalPublishers { get; set; }
        public int TotalUsers { get; set; }
        public int TotalComments { get; set; }
        public int TotalFavorites { get; set; }
        public int TotalRatings { get; set; }
        public double AverageRating { get; set; }
        public DateTime ReportGeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public class AuthorStatisticsDto
    {
        public int AuthorId { get; set; }
        public string AuthorName { get; set; } = "";
        public int BookCount { get; set; }
        public double AverageRating { get; set; }
        public int TotalRatings { get; set; }
        public int TotalFavorites { get; set; }
    }

    public class CategoryStatisticsDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public int BookCount { get; set; }
        public double AverageRating { get; set; }
        public int TotalRatings { get; set; }
        public int TotalFavorites { get; set; }
    }

    public class TagStatisticsDto
    {
        public int TagId { get; set; }
        public string TagName { get; set; } = "";
        public int BookCount { get; set; }
        public double AverageRating { get; set; }
        public int UsageCount { get; set; }
    }

    public class UserActivityDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public int CommentsCount { get; set; }
        public int RatingsCount { get; set; }
        public int FavoritesCount { get; set; }
        public DateTime LastActivityDate { get; set; }
        public bool IsActive { get; set; }
    }
}