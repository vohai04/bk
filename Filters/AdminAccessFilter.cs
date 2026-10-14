using BookInfoFinder.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BookInfoFinder.Filters;

// A folder-wide filter protects page views and every GET/POST handler.
public sealed class AdminAccessFilter : IAsyncAuthorizationFilter, IOrderedFilter
{
    public int Order => -1100;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        if (!int.TryParse(http.Session.GetString("UserId"), out var userId))
        {
            context.Result = http.Request.Query.ContainsKey("handler") || !HttpMethods.IsGet(http.Request.Method)
                ? new UnauthorizedResult()
                : new RedirectToPageResult("/Account/Login", new { returnUrl = http.Request.Path + http.Request.QueryString });
            return;
        }

        var users = http.RequestServices.GetRequiredService<IUserService>();
        var user = await users.GetUserByIdAsync(userId);
        if (user is null || user.Status != 1 || !string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
    }
}
