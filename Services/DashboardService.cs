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
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);

            var stats = new DashboardStatsDto
            {
                TotalBooks = await _context.Books.CountAsync(),
                TotalUsers = await _context.Users.CountAsync(),
                TotalCategories = await _context.Categories.CountAsync(),
                TotalComments = await _context.BookComments.CountAsync(),
                NewBooksToday = await _context.Books.CountAsync(b => b.PublicationDate.Date >= today),
                NewUsersToday = await _context.Users.CountAsync(u => u.CreatedAt.Date >= today),
                NewCommentsToday = await _context.BookComments.CountAsync(c => c.CreatedAt.Date >= today),
                NewCategoriesToday = await _context.Categories.CountAsync(c => c.CreatedAt.Date >= today),
                ActiveUsersToday = await _context.SearchHistories
                    .Where(s => s.SearchedAt.Date >= today)
                    .Select(s => s.UserId)
                    .Distinct()
                    .CountAsync()
            };

            return stats;
        }

        public async Task<IEnumerable<ActivityLogDto>> GetRecentActivitiesAsync(int limit = 20)
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var activities = await _context.ActivityLogs
                .Where(a => a.CreatedAt >= today && a.CreatedAt < tomorrow)
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return activities.Select(a => a.ToDto());
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

        public async Task<IEnumerable<RecentActivityDto>> GetRecentActivitiesForDisplayAsync(int limit = 10)
        {
            var activities = new List<RecentActivityDto>();

            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            // Get recent books
            var recentBooks = await _context.Books
                .Include(b => b.User)
                .Where(b => b.PublicationDate >= today && b.PublicationDate < tomorrow)
                .OrderByDescending(b => b.PublicationDate)
                .Take(limit / 4)
                .ToListAsync();

            activities.AddRange(recentBooks.Select(b => b.ToRecentActivityDto()));

            // Get recent users
            var recentUsers = await _context.Users
                .Where(u => u.CreatedAt >= today && u.CreatedAt < tomorrow)
                .OrderByDescending(u => u.CreatedAt)
                .Take(limit / 4)
                .ToListAsync();

            activities.AddRange(recentUsers.Select(u => u.ToRecentActivityDto()));

            // Get recent categories
            var recentCategories = await _context.Categories
                .Where(c => c.CreatedAt >= today && c.CreatedAt < tomorrow)
                .OrderByDescending(c => c.CreatedAt)
                .Take(limit / 4)
                .ToListAsync();

            activities.AddRange(recentCategories.Select(c => c.ToRecentActivityDto()));

            // Get recent comments
            var recentComments = await _context.BookComments
                .Include(c => c.User)
                .Where(c => c.CreatedAt >= today && c.CreatedAt < tomorrow)
                .OrderByDescending(c => c.CreatedAt)
                .Take(limit / 4)
                .ToListAsync();

            activities.AddRange(recentComments.Select(c => c.ToRecentActivityDto()));

            // Sort by creation date and take top items
            return activities
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit);
        }

        public async Task<(IEnumerable<RecentActivityDto> Activities, int TotalCount)> GetRecentActivitiesPagedAsync(int page, int pageSize)
        {
            // Build queryable projections for each entity type so DB can do ordering/skip/take
            // Note: we project to an anonymous type first then map to RecentActivityDto after materialize

            var booksQ = _context.Books
                .Select(b => new
                {
                    Type = "book",
                    Title = b.Title,
                    Description = (string) ("Đã thêm sách"),
                    UserName = (string) (b.User != null ? b.User.UserName : "Hệ thống"),
                    CreatedAt = b.PublicationDate,
                    ActionUrl = (string) ("/BookDetail/" + b.BookId),
                    EntityId = (int?)b.BookId,
                    EntityType = "Book"
                });

            var usersQ = _context.Users
                .Select(u => new
                {
                    Type = "user",
                    Title = u.UserName,
                    Description = "Đã đăng ký tài khoản",
                    UserName = u.UserName,
                    CreatedAt = u.CreatedAt,
                    ActionUrl = "/Admin/Users",
                    EntityId = (int?)u.UserId,
                    EntityType = "User"
                });

            var categoriesQ = _context.Categories
                .Select(c => new
                {
                    Type = "category",
                    Title = c.Name,
                    Description = "Thể loại mới",
                    UserName = "Hệ thống",
                    CreatedAt = c.CreatedAt,
                    ActionUrl = "/Admin/Categories",
                    EntityId = (int?)c.CategoryId,
                    EntityType = "Category"
                });

            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            // Fetch each entity's today's activities separately and materialize to avoid EF set-operation translation issues
            var booksList = await _context.Books
                .Where(b => b.PublicationDate >= today && b.PublicationDate < tomorrow)
                .Select(b => new RecentActivityDto
                {
                    Type = "book",
                    Title = b.Title,
                    Description = "Đã thêm sách",
                    UserName = b.User != null ? b.User.UserName : "Hệ thống",
                    CreatedAt = b.PublicationDate,
                    ActionUrl = "/BookDetail/" + b.BookId,
                    EntityId = b.BookId,
                    EntityType = "Book"
                })
                .ToListAsync();

            var usersList = await _context.Users
                .Where(u => u.CreatedAt >= today && u.CreatedAt < tomorrow)
                .Select(u => new RecentActivityDto
                {
                    Type = "user",
                    Title = u.UserName,
                    Description = "Đã đăng ký tài khoản",
                    UserName = u.UserName,
                    CreatedAt = u.CreatedAt,
                    ActionUrl = "/Admin/Users",
                    EntityId = u.UserId,
                    EntityType = "User"
                })
                .ToListAsync();

            var categoriesList = await _context.Categories
                .Where(c => c.CreatedAt >= today && c.CreatedAt < tomorrow)
                .Select(c => new RecentActivityDto
                {
                    Type = "category",
                    Title = c.Name,
                    Description = "Thể loại mới",
                    UserName = "Hệ thống",
                    CreatedAt = c.CreatedAt,
                    ActionUrl = "/Admin/Categories",
                    EntityId = c.CategoryId,
                    EntityType = "Category"
                })
                .ToListAsync();

            var commentsList = await _context.BookComments
                .Where(c => c.CreatedAt >= today && c.CreatedAt < tomorrow)
                .Select(c => new RecentActivityDto
                {
                    Type = "comment",
                    Title = c.Comment.Substring(0, Math.Min(60, c.Comment.Length)),
                    Description = c.Comment,
                    UserName = c.User != null ? c.User.UserName : "Unknown",
                    CreatedAt = c.CreatedAt,
                    ActionUrl = "/BookDetail/" + c.BookId + "#comment-content-" + c.BookCommentId,
                    EntityId = c.BookCommentId,
                    EntityType = "BookComment"
                })
                .ToListAsync();

            // Combine lists in-memory and apply ordering + paging
            var all = booksList
                .Concat(usersList)
                .Concat(categoriesList)
                .Concat(commentsList)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            var total = all.Count;
            var pageItems = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return (pageItems, total);
        }

        public async Task<(IEnumerable<ActivityLogDto> ActivityLogs, int TotalCount)> GetActivityLogsPagedAsync(int page, int pageSize, DateTime? startDate = null, DateTime? endDate = null, string? entityType = null)
        {
            // Build query and apply filters at DB level so total count reflects filters
            var query = _context.ActivityLogs.AsQueryable();

            if (startDate.HasValue)
            {
                // Use date range based on the provided date values (avoid timezone shifts)
                // Ensure the DateTime has Kind=Utc so Npgsql maps to timestamptz correctly
                var start = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
                query = query.Where(a => a.CreatedAt >= start);
            }

            if (endDate.HasValue)
            {
                // include full day (exclusive upper bound)
                // Ensure the DateTime has Kind=Utc so Npgsql maps to timestamptz correctly
                var end = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
                query = query.Where(a => a.CreatedAt < end);
            }

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                var t = entityType.ToLower();
                query = query.Where(a => (a.EntityType ?? string.Empty).ToLower().Contains(t));
            }

            query = query.OrderByDescending(a => a.CreatedAt);

            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items.Select(a => a.ToDto()), total);
        }

        public async Task<bool> MarkNotificationAsReadAsync(int notificationId, int userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

            if (notification == null)
                return false;

            // Don't mark comment_reply notifications as read automatically
            // They should remain unread until user manually marks them or takes action
            if (notification.Type == "comment_reply")
                return true; // Return success but don't actually mark as read

            notification.IsRead = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAllNotificationsAsReadAsync(int userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead && n.Type != "comment_reply")
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
                .CountAsync(n => n.UserId == userId && !n.IsRead && n.Type != "comment_reply");
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

        public async Task CreateCommentReplyNotificationAsync(int commentId, int replierUserId)
        {
            var comment = await _context.BookComments
                .Include(c => c.User)
                .Include(c => c.Book)
                .FirstOrDefaultAsync(c => c.BookCommentId == commentId);

            if (comment == null || comment.UserId == replierUserId)
                return;

            var replier = await _context.Users.FindAsync(replierUserId);
            if (replier == null)
                return;

            var title = "Bình luận mới";
            var message = $"{replier.UserName} đã trả lời bình luận của bạn trong sách \"{comment.Book?.Title ?? "Unknown"}\"";

            await CreateNotificationAsync(
                comment.UserId,
                title,
                message,
                "comment_reply",
                    comment.BookCommentId,
                "book"
            );
        }

        public async Task<object?> GetReplyDetailsAsync(int notificationId)
        {
            var notification = await _context.Notifications
                .Include(n => n.User)
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId);

            if (notification == null || notification.Type != "comment_reply")
                return null;

            // Get the reply comment details
            var replyComment = await _context.BookComments
                .Include(c => c.User)
                .Include(c => c.Book)
                .FirstOrDefaultAsync(c => c.BookCommentId == notification.RelatedEntityId);

            if (replyComment == null)
                return null;

            // Get parent comment separately to avoid null reference
            BookComment? parentComment = null;
            if (replyComment.ParentCommentId.HasValue)
            {
                parentComment = await _context.BookComments
                    .Include(pc => pc.User)
                    .FirstOrDefaultAsync(pc => pc.BookCommentId == replyComment.ParentCommentId.Value);
            }

                return new
            {
                notificationId = notification.NotificationId,
                replyId = replyComment.BookCommentId,
                replyContent = replyComment.Comment,
                replyUserName = replyComment.User?.UserName ?? "Ẩn danh",
                replyUserRole = replyComment.User?.Role.ToString() ?? "user",
                replyCreatedAt = replyComment.CreatedAt.ToLocalTime(),
                parentCommentId = replyComment.ParentCommentId,
                parentCommentContent = parentComment?.Comment ?? "",
                parentCommentUserName = parentComment?.User?.UserName ?? "Ẩn danh",
                bookId = replyComment.BookId,
                bookTitle = replyComment.Book?.Title ?? "Unknown Book",
                bookUrl = $"/BookDetail/{replyComment.BookId}"
            };
        }
    }
}