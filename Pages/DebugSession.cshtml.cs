using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookInfoFinder.Pages
{
    public class DebugSessionModel : PageModel
    {
        public void OnGet()
        {
        }

        public JsonResult OnGetSessionInfo()
        {
            var sessionUserId = HttpContext.Session.GetString("UserId");
            var sessionUserName = HttpContext.Session.GetString("UserName");
            var sessionRole = HttpContext.Session.GetString("Role");
            
            var cookieUserId = HttpContext.Request.Cookies["UserId"];
            var cookieUserName = HttpContext.Request.Cookies["UserName"];
            var cookieRole = HttpContext.Request.Cookies["Role"];
            
            var sessionId = HttpContext.Session.Id;
            var sessionKeys = new List<string>();
            
            // Try to get all session keys (if possible)
            try
            {
                HttpContext.Session.LoadAsync().Wait();
                foreach (var key in HttpContext.Session.Keys)
                {
                    sessionKeys.Add(key);
                }
            }
            catch (Exception ex)
            {
                sessionKeys.Add($"Error loading session: {ex.Message}");
            }

            return new JsonResult(new
            {
                session = new
                {
                    id = sessionId,
                    userId = sessionUserId,
                    userName = sessionUserName,
                    role = sessionRole,
                    keys = sessionKeys,
                    isAvailable = HttpContext.Session.IsAvailable
                },
                cookies = new
                {
                    userId = cookieUserId,
                    userName = cookieUserName,
                    role = cookieRole,
                    allCookies = HttpContext.Request.Cookies.ToDictionary(c => c.Key, c => c.Value)
                },
                request = new
                {
                    userAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                    host = HttpContext.Request.Host.ToString(),
                    scheme = HttpContext.Request.Scheme,
                    isHttps = HttpContext.Request.IsHttps,
                    headers = HttpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
                }
            });
        }
    }
}