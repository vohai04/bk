using System.Text.Json;
using BookInfoFinder.Data;
using BookInfoFinder.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BookInfoFinder.Filters;

// Book mutations and user creation record their own events. Other admin changes
// are committed together with their audit record; passwords are never recorded.
public sealed class AdminAuditFilter : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var http = context.HttpContext;
        var entity = context.RouteData.Values["page"]?.ToString() switch
        {
            "/Admin/Categories" => "Category", "/Admin/Tags" => "Tag",
            "/Admin/ManageAuthor" => "Author", "/Admin/ManageNXB" => "Publisher",
            "/Admin/Users" => "User", _ => null
        };
        var handler = context.HandlerMethod?.Name ?? "";
        var action = handler.Contains("Delete") ? "Deleted" : handler.Contains("Add") ? "Created" : handler.Contains("Status") || handler.Contains("Enable") || handler.Contains("Disable") ? "StatusChanged" : handler.Contains("Edit") ? "Updated" : null;
        if (!HttpMethods.IsPost(http.Request.Method) || entity is null || action is null || (entity == "User" && handler == "AjaxAdd"))
        {
            await next();
            return;
        }

        var db = http.RequestServices.GetRequiredService<BookContext>();
        var form = await http.Request.ReadFormAsync();
        var idKey = entity.ToLowerInvariant() + "Id";
        int? id = int.TryParse(form[idKey], out var submittedId) ? submittedId : null;
        var beforeName = "";
        if (id.HasValue)
        {
            var type = db.Model.GetEntityTypes().First(t => t.ClrType.Name == entity).ClrType;
            var existing = await db.FindAsync(type, id.Value);
            if (existing is not null) beforeName = db.Entry(existing).Property(entity == "User" ? "UserName" : "Name").CurrentValue?.ToString() ?? "";
        }
        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await next();
        if (result.Exception is not null || result.Result is not JsonResult json) return;
        var payload = JsonSerializer.SerializeToElement(json.Value);
        if (!payload.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True) return;

        id ??= FindId(payload, idKey);
        var name = action == "Deleted" || action == "StatusChanged" ? beforeName : form[entity == "User" ? "userName" : "name"].ToString().Trim();
        if (id is null && name.Length > 0)
        {
            var entry = db.ChangeTracker.Entries().FirstOrDefault(e => e.Metadata.ClrType.Name == entity && e.Metadata.FindProperty("Name") is not null && string.Equals(e.Property("Name").CurrentValue?.ToString(), name, StringComparison.Ordinal));
            if (entry is not null) id = (int?)entry.Property(entity + "Id").CurrentValue;
        }
        var description = name.Length > 0 ? name : $"{entity} #{id}";
        if (action == "StatusChanged") description += form["status"] == "0" || handler.Contains("Disable") ? " — Khóa tài khoản" : " — Kích hoạt tài khoản";
        await http.RequestServices.GetRequiredService<IDashboardService>().LogActivityAsync(
            http.Session.GetString("UserName") ?? "Admin", action,
            description.Length > 500 ? description[..500] : description, entity, id,
            http.Connection.RemoteIpAddress?.ToString() ?? "");
        await transaction.CommitAsync();
    }

    private static int? FindId(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var id)) return id;
            if (property.Value.ValueKind == JsonValueKind.Object && FindId(property.Value, name) is { } nested) return nested;
        }
        return null;
    }
}
