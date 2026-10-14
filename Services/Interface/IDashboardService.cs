using System.Collections.Generic;
using System.Threading.Tasks;
using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface IDashboardService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync();
        Task<IEnumerable<NotificationDto>> GetNotificationsAsync(int userId, bool unreadOnly = false, int limit = 50);
        Task<(IEnumerable<NotificationDto> Notifications, int TotalCount)> GetNotificationsPagedAsync(int userId, int page, int pageSize, bool unreadOnly = false);
        Task<bool> MarkNotificationAsReadAsync(int notificationId, int userId);
        Task<bool> MarkAllNotificationsAsReadAsync(int userId);
        Task<int> GetUnreadNotificationCountAsync(int userId);
        Task LogActivityAsync(string userName, string action, string description, string entityType, int? entityId, string ipAddress);
        Task CreateNotificationAsync(int userId, string title, string message, string type, int? relatedEntityId = null, string? relatedEntityType = null);
        Task CreateCommentReplyNotificationAsync(int commentId, int replierUserId);
    }
}
