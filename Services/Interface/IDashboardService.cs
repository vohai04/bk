using System.Collections.Generic;
using System.Threading.Tasks;
using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface IDashboardService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync();
        Task<IEnumerable<ActivityLogDto>> GetRecentActivitiesAsync(int limit = 20);
        Task<IEnumerable<NotificationDto>> GetNotificationsAsync(int userId, bool unreadOnly = false, int limit = 50);
        Task<(IEnumerable<NotificationDto> Notifications, int TotalCount)> GetNotificationsPagedAsync(int userId, int page, int pageSize, bool unreadOnly = false);
    Task<IEnumerable<RecentActivityDto>> GetRecentActivitiesForDisplayAsync(int limit = 10);
    Task<(IEnumerable<RecentActivityDto> Activities, int TotalCount)> GetRecentActivitiesPagedAsync(int page, int pageSize);
    Task<(IEnumerable<ActivityLogDto> ActivityLogs, int TotalCount)> GetActivityLogsPagedAsync(int page, int pageSize, DateTime? startDate = null, DateTime? endDate = null, string? entityType = null);
        Task<bool> MarkNotificationAsReadAsync(int notificationId, int userId);
        Task<bool> MarkAllNotificationsAsReadAsync(int userId);
        Task<int> GetUnreadNotificationCountAsync(int userId);
        Task LogActivityAsync(string userName, string action, string description, string entityType, int? entityId, string ipAddress);
        Task CreateNotificationAsync(int userId, string title, string message, string type, int? relatedEntityId = null, string? relatedEntityType = null);
        Task CreateCommentReplyNotificationAsync(int commentId, int replierUserId);
        Task<object?> GetReplyDetailsAsync(int notificationId);
    }
}