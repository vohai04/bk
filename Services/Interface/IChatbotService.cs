using BookInfoFinder.Services.Interface;

namespace BookInfoFinder.Services.Interface;

public interface IChatbotService
{
    Task<string> GetChatbotReplyAsync(string message);
}