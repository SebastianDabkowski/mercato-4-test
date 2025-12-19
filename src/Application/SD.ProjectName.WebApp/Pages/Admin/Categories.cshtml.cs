using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Admin
{
    [Authorize(Roles = IdentityRoles.Admin)]
    public class CategoriesModel : PageModel
    {
        private readonly CategoryManagement _categoryManagement;

        public CategoriesModel(CategoryManagement categoryManagement)
        {
            _categoryManagement = categoryManagement;
        }

        [BindProperty]
        public CreateCategoryInput CreateInput { get; set; } = new();

        public List<CategoryManagement.CategoryTreeItem> Tree { get; private set; } = new();

        public List<SelectListItem> ParentOptions { get; private set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGet()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadAsync();
                return Page();
            }

            try
            {
                await _categoryManagement.Create(new CreateCategoryRequest
                {
                    Name = CreateInput.Name,
                    ParentId = CreateInput.ParentId
                });
                StatusMessage = "Category created.";
                return RedirectToPage();
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await LoadAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostRenameAsync(int id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError(string.Empty, "Name is required.");
                await LoadAsync();
                return Page();
            }

            try
            {
                await _categoryManagement.Rename(id, name);
                StatusMessage = "Category renamed.";
                return RedirectToPage();
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
                await LoadAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostMoveAsync(int id, string direction)
        {
            var moveUp = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase);
            await _categoryManagement.Move(id, moveUp);
            StatusMessage = "Category order updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleAsync(int id, bool isActive)
        {
            try
            {
                await _categoryManagement.ToggleActive(id, isActive);
                StatusMessage = isActive ? "Category activated." : "Category deactivated.";
                return RedirectToPage();
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
                await LoadAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                await _categoryManagement.Delete(id);
                StatusMessage = "Category deleted.";
                return RedirectToPage();
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
                await LoadAsync();
                return Page();
            }
        }

        private async Task LoadAsync()
        {
            Tree = (await _categoryManagement.GetTree(includeInactive: true)).ToList();
            var options = await _categoryManagement.GetTree(includeInactive: true);
            ParentOptions = BuildParentOptions(options, 0);
        }

        private static List<SelectListItem> BuildParentOptions(IEnumerable<CategoryManagement.CategoryTreeItem> items, int depth)
        {
            var results = new List<SelectListItem>();
            foreach (var item in items)
            {
                var prefix = new string(' ', depth * 2);
                results.Add(new SelectListItem($"{prefix}{item.Name}", item.Id.ToString()));
                results.AddRange(BuildParentOptions(item.Children, depth + 1));
            }

            return results;
        }

        public class CreateCategoryInput
        {
            [Required]
            [StringLength(200, MinimumLength = 2)]
            public string Name { get; set; } = string.Empty;

            [Display(Name = "Parent category")]
            public int? ParentId { get; set; }
        }
    }
}
