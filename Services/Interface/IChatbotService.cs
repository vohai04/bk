using BookInfoFinder.Services.Interface;
using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface;

public interface IChatbotService
{
    Task<string> GetChatbotReplyAsync(string message);
    Task<string> GetChatbotReplyAsync(string message, string? sessionId);
    Task<List<ChatbotDto>> GetHistoryAsync(string sessionId);
    Task AddMessageAsync(ChatbotDto message);
}