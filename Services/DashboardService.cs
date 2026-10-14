using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookInfoFinder.Data;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services
{
    using Microsoft.Extensions.Logging;

    public class DashboardService : IDashboardService
    {
        private readonly BookContext _context;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<BookInfoFinder.Hubs.NotificationHub> _hubContext;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(BookContext context, Microsoft.AspNetCore.SignalR.IHubContext<BookInfoFinder.Hubs.NotificationHub> hubContext, ILogger<DashboardService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            var today = DateTime.UtcNow.AddHours(7).Date.AddHours(-7);
            var tomorrow = today.AddDays(1);
            var yesterday = today.AddDays(-1);

            var stats = new DashboardStatsDto
            {
                TotalBooks = await _context.Books.CountAsync(),
                TotalUsers = await _context.Users.CountAsync(),
                TotalCategories = await _context.Categories.CountAsync(),
                TotalComments = await _context.BookComments.CountAsync(),
                NewBooksToday = await _context.ActivityLogs.Where(a => a.EntityType == "Book" && a.Action == "Book Created" && a.CreatedAt >= today && a.CreatedAt < tomorrow).Select(a => a.EntityId).Distinct().CountAsync(),
                NewUsersToday = await _context.Users.CountAsync(u => u.CreatedAt >= today && u.CreatedAt < tomorrow),
                NewCommentsToday = await _context.BookComments.CountAsync(c => c.CreatedAt >= today && c.CreatedAt < tomorrow),
                NewCategoriesToday = await _context.Categories.CountAsync(c => c.CreatedAt >= today && c.CreatedAt < tomorrow),
                ActiveUsersToday = await _context.SearchHistories
                    .Where(s => s.SearchedAt >= today && s.SearchedAt < tomorrow)
                    .Select(s => s.UserId)
                    .Distinct()
                    .CountAsync()
            };

            return stats;
        }

        public async Task<IEnumerable<NotificationDto>> GetNotificationsAsync(int userId, bool unreadOnly = false, int limit = 50)
        {
            var query = _context.Notifications
                .Include(n => n.User)
                .Where(n => n.UserId == userId);

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return notifications.Select(n => n.ToDto());
        }

        public async Task<(IEnumerable<NotificationDto> Notifications, int TotalCount)> GetNotificationsPagedAsync(int userId, int page, int pageSize, bool unreadOnly = false)
        {
            var query = _context.Notifications
                .Include(n => n.User)
                .Where(n => n.UserId == userId);

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items.Select(n => n.ToDto()), total);
        }

        public async Task<bool> MarkNotificationAsReadAsync(int notificationId, int userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

            if (notification == null)
                return false;

            notification.IsRead = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAllNotificationsAsReadAsync(int userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetUnreadNotificationCountAsync(int userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task LogActivityAsync(string userName, string action, string description, string entityType, int? entityId, string ipAddress)
        {
            var activityLog = new ActivityLog
            {
                UserName = userName,
                Action = action,
                Description = description,
                EntityType = entityType,
                EntityId = entityId,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            };

            _context.ActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();
        }

        public async Task CreateNotificationAsync(int userId, string title, string message, string type, int? relatedEntityId = null, string? relatedEntityType = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                RelatedEntityId = relatedEntityId,
                RelatedEntityType = relatedEntityType,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Push real-time notification via SignalR to the specific user group
            try
            {
                var payload = new
                {
                    notificationId = notification.NotificationId,
                    title = notification.Title,
                    message = notification.Message,
                    type = notification.Type,
                    relatedEntityId = notification.RelatedEntityId,
                    relatedEntityType = notification.RelatedEntityType,
                    createdAt = notification.CreatedAt
                };

                var group = BookInfoFinder.Hubs.NotificationHub.GetUserGroup(userId);
                _logger?.LogInformation("Sending notification {NotificationId} to group {Group}", notification.NotificationId, group);
                await _hubContext.Clients.Group(group).SendCoreAsync("ReceiveNotification", new object[] { payload });
                _logger?.LogInformation("Sent notification {NotificationId} to group {Group}", notification.NotificationId, group);
            }
            catch (Exception ex)
            {
                // Log and swallow hub errors to avoid breaking the normal flow if SignalR is not available
                _logger?.LogWarning(ex, "Failed to send notification {NotificationId} to user {UserId}", notification.NotificationId, userId);
            }
        }

        public async Task CreateCommentReplyNotificationAsync(int replyId, int replierUserId)
        {
            var reply = await _context.BookComments.Include(c => c.Book).FirstOrDefaultAsync(c => c.BookCommentId == replyId);
            if (reply?.ParentCommentId is not int parentId) return;
            var parent = await _context.BookComments.FindAsync(parentId);
            if (parent is null || parent.UserId == replierUserId) return;
            var replier = await _context.Users.FindAsync(replierUserId);
            if (replier is null) return;
            await CreateNotificationAsync(parent.UserId, "Phản hồi mới",
                $"{replier.UserName} đã trả lời bình luận của bạn trong sách {reply.Book?.Title ?? "Không có tiêu đề"}",
                "comment_reply", reply.BookCommentId, "BookComment");
        }

    }
}
