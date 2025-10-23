using BookInfoFinder.Data;
using BookInfoFinder.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text;
using BookInfoFinder.Services.Interface;
// using BookInfoFinder.Hubs;
using QuestPDF;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
// Cấu hình license cho QuestPDF
QuestPDF.Settings.License = LicenseType.Community;
 
builder.Services.AddRazorPages()
    .AddRazorRuntimeCompilation();
// Add services to the container
builder.Services.AddRazorPages(options =>
{
   options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
});
builder.Services.AddSession(options =>
{
   options.IdleTimeout = TimeSpan.FromHours(2);
   options.Cookie.HttpOnly = true;
   options.Cookie.IsEssential = true;
   options.Cookie.SameSite = SameSiteMode.Lax;
   options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
   options.Cookie.Name = "BookInfoFinder.Session";
});
builder.Services.AddDistributedMemoryCache();

// Add DbContext
builder.Services.AddDbContext<BookContext>(options =>
   options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSignalR();
// Add custom services
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IBookTagService, BookTagService>();
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ISearchHistoryService, SearchHistoryService>();
builder.Services.AddScoped<IAuthorService, AuthorService>();
builder.Services.AddScoped<IPublisherService, PublisherService>();
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddScoped<IBookCommentService, BookCommentService>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
// Page routes
builder.Services.AddRazorPages(options =>
{
   options.Conventions.AddPageRoute("/Account/Login", "Login");
   options.Conventions.AddPageRoute("/Account/Register", "Register");
   options.Conventions.AddPageRoute("/Account/Logout", "Logout");
   options.Conventions.AddPageRoute("/Admin/Dashboard", "Dashboard");
   options.Conventions.AddPageRoute("/Account/ForgotPassword", "ForgotPassword");
});
builder.Services.AddMemoryCache();
var app = builder.Build();
// Configure pipeline
if (!app.Environment.IsDevelopment())
{
   app.UseExceptionHandler("/Error");
   app.UseHsts();
}
else
{
   app.UseDeveloperExceptionPage();
}
 
//app.MapHub<CommentHub>("/commentHub");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapRazorPages();
app.Run();