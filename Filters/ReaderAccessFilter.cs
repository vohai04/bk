using BookInfoFinder.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BookInfoFinder.Filters;

// Revalidate an existing session so disabling an account takes effect immediately.
public sealed class ReaderAccessFilter : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var session = context.HttpContext.Session;
        if (int.TryParse(session.GetString("UserId"), out var id))
        {
            var users = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
            var user = await users.GetUserByIdAsync(id);
            if (user is null || user.Status != 1)
            {
                session.Clear();
                context.Result = new JsonResult(new { success = false, message = "Tài khoản không còn hoạt động. Vui lòng đăng nhập lại." }) { StatusCode = 403 };
                return;
            }
            session.SetString("Role", user.Role);
        }
        await next();
    }
}
