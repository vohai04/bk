using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
 
namespace BookInfoFinder.Pages.Admin
{
    public class CategoriesModel : PageModel
    {
        private readonly ICategoryService _categoryService;

        public CategoriesModel(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public List<CategoryDto> Categories { get; set; } = new();

        [BindProperty(SupportsGet = true)] public int? EditCategoryId { get; set; }
        [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }

        public async Task OnGetAsync(int? edit, int page = 1)
        {
            CurrentPage = page < 1 ? 1 : page;
            int pageSize = 10;
            
            var result = await _categoryService.GetCategoriesPagedAsync(CurrentPage, pageSize);
            Categories = result.Categories;
            TotalCount = result.TotalCount;
            TotalPages = (int)Math.Ceiling((double)TotalCount / pageSize);
            
            EditCategoryId = edit;
        }

        public async Task<IActionResult> OnPostAddAsync(string Name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Name) || Name.Length > 50)
                {
                    TempData["ErrorMessage"] = "Tên thể loại không hợp lệ.";
                    return RedirectToPage();
                }

                if (await _categoryService.IsCategoryNameExistsAsync(Name.Trim()))
                {
                    TempData["ErrorMessage"] = "Tên thể loại đã tồn tại.";
                    return RedirectToPage();
                }

                var categoryCreateDto = new CategoryCreateDto { Name = Name.Trim() };
                await _categoryService.CreateCategoryAsync(categoryCreateDto);
                TempData["SuccessMessage"] = "Thêm thể loại thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
            }
            
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditAsync(int CategoryId, string Name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Name) || Name.Length > 50)
                {
                    TempData["ErrorMessage"] = "Tên thể loại không hợp lệ.";
                    return RedirectToPage();
                }

                var existingCategory = await _categoryService.GetCategoryByIdAsync(CategoryId);
                if (existingCategory == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thể loại.";
                    return RedirectToPage();
                }

                // Check if name exists for other categories
                var nameExists = await _categoryService.IsCategoryNameExistsAsync(Name.Trim());
                if (nameExists && !existingCategory.Name.Equals(Name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "Tên thể loại đã tồn tại.";
                    return RedirectToPage();
                }

                var categoryUpdateDto = new CategoryUpdateDto 
                { 
                    CategoryId = CategoryId,
                    Name = Name.Trim() 
                };
                await _categoryService.UpdateCategoryAsync(categoryUpdateDto);
                TempData["SuccessMessage"] = "Cập nhật thể loại thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
            }
            
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int CategoryId)
        {
            try
            {
                var success = await _categoryService.DeleteCategoryAsync(CategoryId);
                if (success)
                {
                    TempData["SuccessMessage"] = "Xóa thể loại thành công!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Không thể xóa thể loại này.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
            }
            
            return RedirectToPage();
        }

        public async Task<JsonResult> OnGetAjaxSearchAsync()
        {
            try
            {
                var query = Request.Query;
                string search = query["search"].ToString() ?? "";
                int.TryParse(query["page"], out int page);
                int.TryParse(query["pageSize"], out int pageSize);

                page = page <= 0 ? 1 : page;
                pageSize = pageSize <= 0 ? 10 : pageSize;

                var result = await _categoryService.GetCategoriesPagedAsync(page, pageSize);
                var filteredCategories = result.Categories;

                // Apply search filter if provided
                if (!string.IsNullOrWhiteSpace(search))
                {
                    filteredCategories = (await _categoryService.SearchCategoriesAsync(search))
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();
                }

                var categoryResult = filteredCategories.Select(cat => new {
                    cat.CategoryId,
                    cat.Name,
                    cat.Description,
                    cat.BookCount,
                    cat.CreatedAt,
                    CreatedAtFormatted = cat.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                });

                var totalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize);
                return new JsonResult(new { categories = categoryResult, totalPages, totalCount = result.TotalCount });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxAddAsync([FromForm] string name, [FromForm] string description)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name) || name.Length > 50)
                    return new JsonResult(new { success = false, message = "Tên thể loại không hợp lệ." });

                if (await _categoryService.IsCategoryNameExistsAsync(name.Trim()))
                    return new JsonResult(new { success = false, message = "Tên thể loại đã tồn tại." });

                var categoryCreateDto = new CategoryCreateDto { 
                    Name = name.Trim(),
                    Description = description?.Trim() ?? ""
                };
                var createdCategory = await _categoryService.CreateCategoryAsync(categoryCreateDto);
                
                return new JsonResult(new { 
                    success = true, 
                    category = new { 
                        createdCategory.CategoryId, 
                        createdCategory.Name,
                        createdCategory.Description,
                        createdCategory.BookCount,
                        createdCategory.CreatedAt,
                        CreatedAtFormatted = createdCategory.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxEditAsync([FromForm] int categoryId, [FromForm] string name, [FromForm] string description)
        {
            try
            {
                var existingCategory = await _categoryService.GetCategoryByIdAsync(categoryId);
                if (existingCategory == null) 
                    return new JsonResult(new { success = false, message = "Không tìm thấy thể loại." });
                
                if (string.IsNullOrWhiteSpace(name) || name.Length > 50)
                    return new JsonResult(new { success = false, message = "Tên thể loại không hợp lệ." });

                // Check if name exists for other categories
                var nameExists = await _categoryService.IsCategoryNameExistsAsync(name.Trim());
                if (nameExists && !existingCategory.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                    return new JsonResult(new { success = false, message = "Tên thể loại đã tồn tại." });

                var categoryUpdateDto = new CategoryUpdateDto 
                { 
                    CategoryId = categoryId,
                    Name = name.Trim(),
                    Description = description?.Trim() ?? ""
                };
                var updatedCategory = await _categoryService.UpdateCategoryAsync(categoryUpdateDto);
                
                return new JsonResult(new { 
                    success = true,
                    category = new { 
                        updatedCategory.CategoryId, 
                        updatedCategory.Name,
                        updatedCategory.Description,
                        updatedCategory.BookCount,
                        updatedCategory.CreatedAt,
                        CreatedAtFormatted = updatedCategory.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxDeleteAsync([FromForm] int categoryId)
        {
            try
            {
                var success = await _categoryService.DeleteCategoryAsync(categoryId);
                return new JsonResult(new { success });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }
}