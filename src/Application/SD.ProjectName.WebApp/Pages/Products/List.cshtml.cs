using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.WebApp.Services;
using static SD.ProjectName.Modules.Products.Application.CategoryManagement;

namespace SD.ProjectName.WebApp.Pages.Products
{
    public class ListModel : PageModel
    {
        private readonly ILogger<ListModel> _logger;
        private readonly GetProducts _getProducts;
        private readonly ProductImageService _imageService;
        private readonly CategoryManagement _categoryManagement;

        public ListModel(ILogger<ListModel> logger, GetProducts getProducts, ProductImageService imageService, CategoryManagement categoryManagement)
        {
            _logger = logger;
            _getProducts = getProducts;
            _imageService = imageService;
            _categoryManagement = categoryManagement;
        }

        public List<ProductListItem> Products { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Category { get; set; }

        public IReadOnlyList<CategoryTreeItem> RootCategories { get; private set; } = Array.Empty<CategoryTreeItem>();

        public IReadOnlyList<CategoryTreeItem> ChildCategories { get; private set; } = Array.Empty<CategoryTreeItem>();

        public CategoryTreeItem? SelectedCategory { get; private set; }

        public IReadOnlyList<string> SuggestedCategories { get; private set; } = Array.Empty<string>();

        public bool AggregatedFromSubcategories { get; private set; }

        public async Task OnGetAsync()
        {
            RootCategories = await _categoryManagement.GetTree(includeInactive: false);
            SelectedCategory = FindCategory(RootCategories, Category);

            var categoryNamesForQuery = BuildCategoryQueryList(SelectedCategory);
            AggregatedFromSubcategories = SelectedCategory?.Children?.Any() == true;

            var products = await LoadProductsAsync(categoryNamesForQuery);
            Products = products.Select(p =>
            {
                var main = _imageService.GetMainImage(p.ImageUrls);
                return new ProductListItem(
                    p,
                    main,
                    _imageService.GetVariant(main, ImageVariant.Thumbnail));
            }).ToList();

            ChildCategories = SelectedCategory is null
                ? Array.Empty<CategoryTreeItem>()
                : SelectedCategory.Children
                    .OrderBy(c => c.DisplayOrder)
                    .ThenBy(c => c.Name)
                    .ToList();

            if (!Products.Any())
            {
                SuggestedCategories = RootCategories
                    .SelectMany(c => Flatten(new[] { c }))
                    .Where(name => !string.Equals(name, SelectedCategory?.Name, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToList();
            }
        }

        public bool IsSelected(string? categoryName) =>
            !string.IsNullOrWhiteSpace(categoryName) &&
            string.Equals(categoryName, SelectedCategory?.Name ?? Category, StringComparison.OrdinalIgnoreCase);

        public record ProductListItem(ProductModel Product, string? MainImageUrl, string? ThumbnailUrl);

        private async Task<List<ProductModel>> LoadProductsAsync(IReadOnlyList<string> categoriesForQuery)
        {
            if (categoriesForQuery.Any())
            {
                var aggregated = new List<ProductModel>();
                foreach (var category in categoriesForQuery)
                {
                    var items = await _getProducts.GetList(category);
                    aggregated.AddRange(items);
                }

                // A product might appear in multiple descendant categories; de-duplicate before returning.
                return aggregated
                    .GroupBy(p => p.Id)
                    .Select(g => g.First())
                    .ToList();
            }

            return await _getProducts.GetList(Category);
        }

        private static IReadOnlyList<string> BuildCategoryQueryList(CategoryTreeItem? selectedCategory)
        {
            if (selectedCategory is null)
            {
                return Array.Empty<string>();
            }

            var categories = new List<string> { selectedCategory.Name };
            categories.AddRange(GetDescendantNames(selectedCategory));
            return categories;
        }

        private static IEnumerable<string> GetDescendantNames(CategoryTreeItem category)
        {
            foreach (var child in category.Children ?? Enumerable.Empty<CategoryTreeItem>())
            {
                yield return child.Name;
                foreach (var name in GetDescendantNames(child))
                {
                    yield return name;
                }
            }
        }

        private static CategoryTreeItem? FindCategory(IEnumerable<CategoryTreeItem> categories, string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            foreach (var category in categories)
            {
                if (string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return category;
                }

                var match = FindCategory(category.Children, name);
                if (match is not null)
                {
                    return match;
                }
            }

            return null;
        }

        private static IEnumerable<string> Flatten(IEnumerable<CategoryTreeItem> categories)
        {
            foreach (var category in categories)
            {
                yield return category.Name;
                foreach (var child in Flatten(category.Children))
                {
                    yield return child;
                }
            }
        }
    }
}
