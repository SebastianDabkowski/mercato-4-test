using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller.Products
{
    [Authorize(Roles = IdentityRoles.Seller)]
    public class CreateModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CreateProduct _createProduct;

        public CreateModel(UserManager<ApplicationUser> userManager, CreateProduct createProduct)
        {
            _userManager = userManager;
            _createProduct = createProduct;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            await _createProduct.CreateAsync(new CreateProduct.Request
            {
                Title = Input.Title,
                Category = Input.Category,
                Description = Input.Description,
                Price = Input.Price,
                Stock = Input.Stock
            }, user.Id);

            StatusMessage = "Product saved as draft.";
            return RedirectToPage("./Index");
        }

        public class InputModel
        {
            [Required]
            [StringLength(200, MinimumLength = 3)]
            [Display(Name = "Title")]
            public string Title { get; set; } = string.Empty;

            [StringLength(2000)]
            [Display(Name = "Description")]
            public string? Description { get; set; }

            [Required]
            [Range(0.01, 1_000_000)]
            [Display(Name = "Price")]
            public decimal Price { get; set; }

            [Required]
            [Range(0, 1_000_000)]
            [Display(Name = "Stock")]
            public int Stock { get; set; }

            [Required]
            [StringLength(100, MinimumLength = 2)]
            [Display(Name = "Category")]
            public string Category { get; set; } = string.Empty;
        }
    }
}
