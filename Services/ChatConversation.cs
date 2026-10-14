using System.Security.Cryptography;

namespace BookInfoFinder.Services;

public static class ChatConversation
{
    // Account history is selected only from the authenticated server session.
    // Guest history uses an unguessable HttpOnly capability, never a client-supplied ID parameter.
    public static string Resolve(HttpContext http)
    {
        if (int.TryParse(http.Session.GetString("UserId"), out var userId) && userId > 0)
            return $"user:{userId}";
        var secure = !http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();
        var cookieName = secure ? "__Host-BookInfoFinder.Chat" : "BookInfoFinder.Chat";
        var token = http.Request.Cookies[cookieName];
        if (token is null || token.Length != 64 || !token.All(Uri.IsHexDigit))
        {
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            http.Response.Cookies.Append(cookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Path = "/",
                MaxAge = TimeSpan.FromDays(30)
            });
        }
        return "guest:" + token;
    }
}
