using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
 
namespace BookInfoFinder.Pages.Admin
{
    public class UpdateBookModel : PageModel
    {
        private readonly IBookService _bookService;
        private readonly IAuthorService _authorService;
        private readonly ICategoryService _categoryService;
        private readonly IPublisherService _publisherService;
        private readonly IUserService _userService;
        private readonly ITagService _tagService;
        private readonly IBookTagService _bookTagService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public UpdateBookModel(
            IBookService bookService,
            IAuthorService authorService,
            ICategoryService categoryService,
            IPublisherService publisherService,
            IUserService userService,
            ITagService tagService,
            IBookTagService bookTagService,
            IWebHostEnvironment webHostEnvironment)
        {
            _bookService = bookService;
            _authorService = authorService;
            _categoryService = categoryService;
            _publisherService = publisherService;
            _userService = userService;
            _tagService = tagService;
            _bookTagService = bookTagService;
            _webHostEnvironment = webHostEnvironment;
        }

        public List<AuthorDto> Authors { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<PublisherDto> Publishers { get; set; } = new();
        public List<TagDto> Tags { get; set; } = new();

        [BindProperty] public BookUpdateDto Book { get; set; } = new();
        [BindProperty] public List<int> SelectedTagIds { get; set; } = new();
        [BindProperty] public IFormFile? ImageFile { get; set; }

        [BindProperty(SupportsGet = true)] public string? Search { get; set; }
        [BindProperty(SupportsGet = true)] public int? Category { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                await LoadDataAsync();

                var bookDetail = await _bookService.GetBookByIdAsync(id);
                if (bookDetail == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy sách!";
                    return RedirectToPage("/Admin/ManageBook");
                }

                // Map BookDetailDto to BookUpdateDto
                Book = new BookUpdateDto
                {
                    BookId = bookDetail.BookId,
                    Title = bookDetail.Title,
                    ISBN = bookDetail.ISBN,
                    Description = bookDetail.Description,
                    Abstract = bookDetail.Abstract,
                    ImageBase64 = bookDetail.ImageBase64,
                    PublicationDate = bookDetail.PublicationDate,
                    AuthorId = bookDetail.AuthorId,
                    CategoryId = bookDetail.CategoryId,
                    PublisherId = bookDetail.PublisherId,
                    UserId = bookDetail.UserId
                };

                // Get selected tags
                var tags = await _bookTagService.GetTagsByBookIdAsync(id);
                SelectedTagIds = tags.Select(t => t.TagId).ToList();
                Book.TagIds = SelectedTagIds;

                return Page();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi khi tải dữ liệu: {ex.Message}";
                return RedirectToPage("/Admin/ManageBook");
            }
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
                    TempData["ErrorMessage"] = "Bạn cần đăng nhập để cập nhật sách.";
                    return RedirectToPage("/Account/Login");
                }

                // Handle image upload
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

                // Set update properties
                Book.UserId = userId;
                Book.TagIds = SelectedTagIds;

                // Ensure publication date is UTC
                if (Book.PublicationDate != default && Book.PublicationDate.Kind != DateTimeKind.Utc)
                {
                    Book.PublicationDate = DateTime.SpecifyKind(Book.PublicationDate, DateTimeKind.Utc);
                }

                // Update book
                var updatedBook = await _bookService.UpdateBookAsync(Book);
                if (updatedBook == null)
                {
                    await LoadDataAsync();
                    TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật sách.";
                    return Page();
                }

                TempData["SuccessMessage"] = "Cập nhật sách thành công!";
                return RedirectToPage("/Admin/ManageBook", new { search = Search, category = Category });
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