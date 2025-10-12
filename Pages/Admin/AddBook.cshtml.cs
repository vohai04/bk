using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;

namespace BookInfoFinder.Pages.Admin
{
    public class AddBookModel : PageModel
    {
        private readonly IBookService _bookService;
        private readonly IAuthorService _authorService;
        private readonly ICategoryService _categoryService;
        private readonly IPublisherService _publisherService;
        private readonly ITagService _tagService;
        private readonly IBookTagService _bookTagService;
        private readonly IUserService _userService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AddBookModel(
            IBookService bookService,
            IAuthorService authorService,
            ICategoryService categoryService,
            IPublisherService publisherService,
            ITagService tagService,
            IBookTagService bookTagService,
            IUserService userService,
            IWebHostEnvironment webHostEnvironment)
        {
            _bookService = bookService;
            _authorService = authorService;
            _categoryService = categoryService;
            _publisherService = publisherService;
            _tagService = tagService;
            _bookTagService = bookTagService;
            _userService = userService;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty] public BookCreateDto Book { get; set; } = new();
        [BindProperty] public List<int> SelectedTagIds { get; set; } = new();
        [BindProperty] public IFormFile? ImageFile { get; set; }

        public List<AuthorDto> Authors { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<PublisherDto> Publishers { get; set; } = new();
        public List<TagDto> Tags { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await LoadDataAsync();
                    return Page();
                }

                // Get current user ID from session
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                {
                    TempData["ErrorMessage"] = "Bạn cần đăng nhập để thêm sách.";
                    return RedirectToPage("/Account/Login");
                }

                // Handle image upload and save to images folder
                if (ImageFile != null)
                {
                    var imagePath = SaveImageToFolder(ImageFile);
                    if (imagePath == null)
                    {
                        await LoadDataAsync();
                        ModelState.AddModelError("ImageFile", "Lỗi khi upload ảnh. Vui lòng thử lại.");
                        return Page();
                    }
                    Book.ImageBase64 = imagePath; // Save relative path instead of base64
                }

                // Set book properties
                Book.UserId = userId;
                Book.TagIds = SelectedTagIds;

                // Ensure publication date is UTC
                if (Book.PublicationDate != default && Book.PublicationDate.Kind != DateTimeKind.Utc)
                {
                    Book.PublicationDate = DateTime.SpecifyKind(Book.PublicationDate, DateTimeKind.Utc);
                }

                // Add book
                var createdBook = await _bookService.CreateBookAsync(Book);
                if (createdBook == null)
                {
                    await LoadDataAsync();
                    TempData["ErrorMessage"] = "Có lỗi xảy ra khi thêm sách.";
                    return Page();
                }

                // Add book tags
                if (SelectedTagIds.Any())
                {
                    foreach (var tagId in SelectedTagIds)
                    {
                        await _bookTagService.AddBookTagAsync(createdBook.BookId, tagId);
                    }
                }

                TempData["SuccessMessage"] = "Thêm sách thành công!";
                return RedirectToPage("/Admin/ManageBook");
            }
            catch (Exception ex)
            {
                await LoadDataAsync();
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
                return Page();
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                Authors = await _authorService.GetAllAuthorsAsync();
                Categories = await _categoryService.GetAllCategoriesAsync();
                Publishers = await _publisherService.GetAllPublishersAsync();
                Tags = await _tagService.GetAllTagsAsync();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi khi tải dữ liệu: {ex.Message}";
            }
        }

        private string? SaveImageToFolder(IFormFile imageFile)
        {
            try
            {
                // Validate file
                if (imageFile.Length == 0)
                    return null;

                // Check file size (max 5MB)
                if (imageFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("ImageFile", "Kích thước file không được vượt quá 5MB.");
                    return null;
                }

                // Check file extension
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("ImageFile", "Chỉ chấp nhận file ảnh (.jpg, .jpeg, .png, .gif, .webp).");
                    return null;
                }

                // Use ImageHelper to save file
                return Services.ImageHelper.SaveFileToImages(imageFile, _webHostEnvironment.WebRootPath);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xử lý ảnh: {ex.Message}";
                return null;
            }
        }

        private async Task<string?> ConvertImageToBase64Async(IFormFile imageFile)
        {
            try
            {
                // Validate file
                if (imageFile.Length == 0)
                    return null;

                // Check file size (max 5MB)
                if (imageFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("ImageFile", "Kích thước file không được vượt quá 5MB.");
                    return null;
                }

                // Check file extension
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("ImageFile", "Chỉ chấp nhận file ảnh (.jpg, .jpeg, .png, .gif, .webp).");
                    return null;
                }

                // Convert to base64
                using (var memoryStream = new MemoryStream())
                {
                    await imageFile.CopyToAsync(memoryStream);
                    var imageBytes = memoryStream.ToArray();
                    var base64String = Convert.ToBase64String(imageBytes);
                    
                    // Add data URL prefix based on content type
                    var mimeType = imageFile.ContentType;
                    return $"data:{mimeType};base64,{base64String}";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xử lý ảnh: {ex.Message}";
                return null;
            }
        }
    }
}