using System;

namespace BookInfoFinder.Models.Dto
{
    public class ChatbotDto
    {
        public int ChatbotId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // "user" hoặc "assistant"
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
