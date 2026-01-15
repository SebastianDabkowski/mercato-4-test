using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Search
{
    public class IndexModel : PageModel
    {
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

        [BindProperty(SupportsGet = true)]
        public string? Category { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal? MinPrice { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal? MaxPrice { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Condition { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SellerId { get; set; }

        public List<ProductListItem> Results { get; private set; } = new();

        public IReadOnlyList<CategoryManagement.CategoryOption> CategoryOptions { get; private set; } = Array.Empty<CategoryManagement.CategoryOption>();

        public IReadOnlyList<string> ConditionOptions { get; private set; } = Array.Empty<string>();

        public IReadOnlyList<string> SellerOptions { get; private set; } = Array.Empty<string>();

        public IReadOnlyList<ActiveFilter> ActiveFilters { get; private set; } = Array.Empty<ActiveFilter>();

        public bool HasQuery => !string.IsNullOrWhiteSpace(Q);

        public bool HasActiveFilters => ActiveFilters.Any();

        public async Task OnGetAsync()
        {
            Sort ??= ProductSort.Relevance;
            CategoryOptions = await _categoryManagement.GetActiveOptions();
            ActiveFilters = BuildActiveFilters();
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
            ConditionOptions = BuildConditionOptions(products);
            SellerOptions = BuildSellerOptions(products);
            var filtered = ProductFiltering.Apply(products, BuildFilters());
            Results = filtered.Select(p =>
            {
                var main = _imageService.GetMainImage(p.ImageUrls);
                return new ProductListItem(
                    p,
                    main,
                    _imageService.GetVariant(main, ImageVariant.Thumbnail));
            }).ToList();
        }

        public record ProductListItem(ProductModel Product, string? MainImageUrl, string? ThumbnailUrl);

        public record ActiveFilter(string Label, string Value);

        private ProductFilterOptions BuildFilters() =>
            new(Category, MinPrice, MaxPrice, Condition, SellerId);

        private static IReadOnlyList<string> BuildConditionOptions(IEnumerable<ProductModel> products)
        {
            var available = new HashSet<string>(
                products.Select(p => p.Condition).Where(c => !string.IsNullOrWhiteSpace(c)),
                StringComparer.OrdinalIgnoreCase);

            return ProductConditions.All.Where(available.Contains).ToList();
        }

        private static IReadOnlyList<string> BuildSellerOptions(IEnumerable<ProductModel> products) =>
            products.Select(p => p.SellerId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id)
                .ToList();

        private IReadOnlyList<ActiveFilter> BuildActiveFilters()
        {
            var filters = new List<ActiveFilter>();

            if (!string.IsNullOrWhiteSpace(Category))
            {
                filters.Add(new ActiveFilter("Category", Category.Trim()));
            }

            if (MinPrice.HasValue || MaxPrice.HasValue)
            {
                var min = MinPrice?.ToString("C");
                var max = MaxPrice?.ToString("C");
                var value = MinPrice.HasValue && MaxPrice.HasValue
                    ? $"{min} - {max}"
                    : MinPrice.HasValue
                        ? $"From {min}"
                        : $"Up to {max}";
                filters.Add(new ActiveFilter("Price", value));
            }

            if (!string.IsNullOrWhiteSpace(Condition))
            {
                filters.Add(new ActiveFilter("Condition", Condition.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(SellerId))
            {
                filters.Add(new ActiveFilter("Seller", SellerId.Trim()));
            }

            return filters;
        }
    }
}
