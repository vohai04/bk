using System;

namespace BookInfoFinder.Models.Dto
{
    public class ActivityLogDto
    {
        public int ActivityId { get; set; }
        public string? UserName { get; set; }
        public string? Action { get; set; }
        public string? Description { get; set; }
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? IpAddress { get; set; }
    }

    public class DashboardStatsDto
    {
        public int TotalBooks { get; set; }
        public int TotalUsers { get; set; }
        public int TotalCategories { get; set; }
        public int TotalComments { get; set; }
        public int NewBooksToday { get; set; }
        public int NewUsersToday { get; set; }
        public int NewCommentsToday { get; set; }
        public int NewCategoriesToday { get; set; }
        public int ActiveUsersToday { get; set; }
    }

    public class NotificationDto
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public string? Type { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; }
    }

    public class RecentActivityDto
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? UserName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ActionUrl { get; set; }
        public int? EntityId { get; set; }
        public string? EntityType { get; set; }
    }
}