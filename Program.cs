using BookInfoFinder.Data;
using BookInfoFinder.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text;
using BookInfoFinder.Services.Interface;
using BookInfoFinder.Hubs;
using QuestPDF;
using QuestPDF.Infrastructure;
using DotNetEnv; // Thêm để đọc file .env

// Đọc file .env nếu tồn tại
if (File.Exists(".env"))
{
    Env.Load();
}

var builder = WebApplication.CreateBuilder(args);

// Cấu hình để thay thế ${VARIABLE} trong appsettings bằng environment variables
builder.Configuration.AddEnvironmentVariables();

// Đảm bảo environment variables từ .env được áp dụng
foreach (System.Collections.DictionaryEntry envVar in Environment.GetEnvironmentVariables())
{
    var key = envVar.Key?.ToString();
    var value = envVar.Value?.ToString();
    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
    {
        builder.Configuration[key] = value;
    }
}

// Override appsettings với environment variables nếu có
var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var dbUser = Environment.GetEnvironmentVariable("DB_USER");
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

if (!string.IsNullOrEmpty(dbHost) && !string.IsNullOrEmpty(dbName) && 
    !string.IsNullOrEmpty(dbUser) && !string.IsNullOrEmpty(dbPassword))
{
    builder.Configuration["ConnectionStrings:DefaultConnection"] = 
        $"Host={dbHost};Database={dbName};Username={dbUser};Password={dbPassword}";
}

var emailAddress = Environment.GetEnvironmentVariable("EMAIL_ADDRESS");
var emailPassword = Environment.GetEnvironmentVariable("EMAIL_PASSWORD");
if (!string.IsNullOrEmpty(emailAddress))
    builder.Configuration["EmailSettings:Email"] = emailAddress;
if (!string.IsNullOrEmpty(emailPassword))
    builder.Configuration["EmailSettings:Password"] = emailPassword;

var geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
if (!string.IsNullOrEmpty(geminiApiKey))
    builder.Configuration["GEMINI:ApiKey"] = geminiApiKey;
QuestPDF.Settings.License = LicenseType.Community;
 
builder.Services.AddRazorPages()
    .AddRazorRuntimeCompilation();
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
builder.Services.AddDbContext<BookContext>(options =>
   options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSignalR();
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
 
app.MapHub<NotificationHub>("/notificationHub");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapRazorPages();
app.Run();