using Microsoft.AspNetCore.Http;

namespace BookInfoFinder.Services;

public static class PasswordResetGrant
{
    public static void Issue(ISession session, string email)
    {
        session.SetString("PasswordResetEmail", email.Trim().ToLowerInvariant());
        session.SetString("PasswordResetExpires", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString());
    }
    public static bool IsValid(ISession session, string email) =>
        session.GetString("PasswordResetEmail") == email.Trim().ToLowerInvariant() &&
        long.TryParse(session.GetString("PasswordResetExpires"), out var expires) &&
        expires > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static void Clear(ISession session)
    {
        session.Remove("PasswordResetEmail");
        session.Remove("PasswordResetExpires");
    }
}
