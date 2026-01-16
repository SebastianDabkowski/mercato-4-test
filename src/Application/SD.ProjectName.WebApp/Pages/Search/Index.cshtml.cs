using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Search
{
    public class IndexModel : PageModel
    {
        private const int SuggestionLimit = 5;
        private const int SuggestionMinLength = 2;
        private readonly GetProducts _getProducts;
        private readonly ProductImageService _imageService;
        private readonly CategoryManagement _categoryManagement;

        public IndexModel(GetProducts getProducts, ProductImageService imageService, CategoryManagement categoryManagement)
        {
            _getProducts = getProducts;
            _imageService = imageService;
            _categoryManagement = categoryManagement;
        }

        [BindProperty(SupportsGet = true)]
        public string? Q { get; set; }

        [BindProperty(SupportsGet = true)]
        public ProductSort? Sort { get; set; }

        public List<ProductListItem> Results { get; private set; } = new();

        public bool HasQuery => !string.IsNullOrWhiteSpace(Q);

        public async Task OnGetAsync()
        {
            Sort ??= ProductSort.Relevance;
            if (!HasQuery)
            {
                Results.Clear();
                return;
            }

            var normalized = Q!.Trim();
            if (normalized.Length > 200)
            {
                normalized = normalized[..200];
            }

            Q = normalized;

            var products = await _getProducts.Search(normalized, Sort.Value);
            Results = products.Select(p =>
            {
                var main = _imageService.GetMainImage(p.ImageUrls);
                return new ProductListItem(
                    p,
                    main,
                    _imageService.GetVariant(main, ImageVariant.Thumbnail));
            }).ToList();
        }

        public async Task<IActionResult> OnGetSuggestionsAsync(string? term)
        {
            var normalized = NormalizeSuggestionTerm(term);
            if (normalized is null)
            {
                return new JsonResult(SuggestionResponse.Empty);
            }

            var products = await _getProducts.Search(normalized, ProductSort.Relevance);
            var querySuggestions = products
                .Select(p => p.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(SuggestionLimit)
                .ToList();

            var productSuggestions = new List<ProductSuggestionItem>();
            foreach (var product in products.Take(SuggestionLimit))
            {
                var url = Url.Page("/Products/Details", new { id = product.Id });
                if (!string.IsNullOrWhiteSpace(url))
                {
                    productSuggestions.Add(new ProductSuggestionItem(product.Name, product.Category, url));
                }
            }

            var categories = await _categoryManagement.GetTree(includeInactive: false);
            var categorySuggestions = new List<SuggestionItem>();
            foreach (var category in FlattenCategories(categories)
                         .Where(name => name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(name => name.StartsWith(normalized, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                         .ThenBy(name => name)
                         .Take(SuggestionLimit))
            {
                var url = Url.Page("/Products/List", new { category });
                if (!string.IsNullOrWhiteSpace(url))
                {
                    categorySuggestions.Add(new SuggestionItem(category, url));
                }
            }

            return new JsonResult(new SuggestionResponse(querySuggestions, categorySuggestions, productSuggestions));
        }

        public record ProductListItem(ProductModel Product, string? MainImageUrl, string? ThumbnailUrl);

        public record SuggestionItem(string Label, string Url);

        public record ProductSuggestionItem(string Label, string Category, string Url);

        public record SuggestionResponse(
            IReadOnlyList<string> Queries,
            IReadOnlyList<SuggestionItem> Categories,
            IReadOnlyList<ProductSuggestionItem> Products)
        {
            public static SuggestionResponse Empty { get; } =
                new(Array.Empty<string>(), Array.Empty<SuggestionItem>(), Array.Empty<ProductSuggestionItem>());
        }

        private static string? NormalizeSuggestionTerm(string? term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return null;
            }

            var trimmed = term.Trim();
            if (trimmed.Length < SuggestionMinLength)
            {
                return null;
            }

            return trimmed.Length > 200 ? trimmed[..200] : trimmed;
        }

        private static IEnumerable<string> FlattenCategories(IEnumerable<CategoryManagement.CategoryTreeItem> categories)
        {
            foreach (var category in categories)
            {
                yield return category.Name;
                foreach (var child in FlattenCategories(category.Children))
                {
                    yield return child;
                }
            }
        }
    }
}
