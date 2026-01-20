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
        private const int PageSize = 12;
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

        [BindProperty(SupportsGet = true)]
        public ProductSort? Sort { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal? MinPrice { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal? MaxPrice { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Condition { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SellerId { get; set; }

        [BindProperty(SupportsGet = true, Name = "page")]
        public int PageNumber { get; set; } = 1;

        public IReadOnlyList<CategoryTreeItem> RootCategories { get; private set; } = Array.Empty<CategoryTreeItem>();

        public IReadOnlyList<CategoryTreeItem> ChildCategories { get; private set; } = Array.Empty<CategoryTreeItem>();

        public CategoryTreeItem? SelectedCategory { get; private set; }

        public IReadOnlyList<string> SuggestedCategories { get; private set; } = Array.Empty<string>();

        public bool AggregatedFromSubcategories { get; private set; }

        public IReadOnlyList<string> ConditionOptions { get; private set; } = Array.Empty<string>();

        public IReadOnlyList<string> SellerOptions { get; private set; } = Array.Empty<string>();

        public IReadOnlyList<ActiveFilter> ActiveFilters { get; private set; } = Array.Empty<ActiveFilter>();

        public IReadOnlyList<int> PageNumbers { get; private set; } = Array.Empty<int>();

        public int TotalResults { get; private set; }

        public int TotalPages { get; private set; }

        public bool HasActiveFilters => ActiveFilters.Any();

        public bool HasNextPage => PageNumber < TotalPages;

        public bool HasPreviousPage => PageNumber > 1;

        public int StartResult => TotalResults == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;

        public int EndResult => TotalResults == 0 ? 0 : Math.Min(PageNumber * PageSize, TotalResults);

        public async Task OnGetAsync()
        {
            Sort ??= ProductSort.Newest;
            PageNumber = NormalizePage(PageNumber);
            RootCategories = await _categoryManagement.GetTree(includeInactive: false);
            SelectedCategory = FindCategory(RootCategories, Category);

            var categoryNamesForQuery = BuildCategoryQueryList(SelectedCategory);
            AggregatedFromSubcategories = SelectedCategory?.Children?.Any() == true;

            var baseProducts = await LoadProductsAsync(categoryNamesForQuery, Sort.Value, applySorting: false);
            ConditionOptions = BuildConditionOptions(baseProducts);
            SellerOptions = BuildSellerOptions(baseProducts);
            ActiveFilters = BuildActiveFilters();

            var filtered = ProductFiltering.Apply(baseProducts, BuildFilters());
            var sorted = ProductSorting.Apply(filtered, Sort.Value).ToList();
            TotalResults = sorted.Count;
            TotalPages = (int)Math.Ceiling(TotalResults / (double)PageSize);
            if (TotalPages > 0)
            {
                PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
            }

            PageNumbers = TotalPages > 0
                ? Enumerable.Range(1, TotalPages).ToList()
                : Array.Empty<int>();
            Products = sorted.Skip((PageNumber - 1) * PageSize).Take(PageSize).Select(p =>
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

        public record ActiveFilter(string Label, string Value);

        private async Task<List<ProductModel>> LoadProductsAsync(IReadOnlyList<string> categoriesForQuery, ProductSort sort, bool applySorting)
        {
            if (categoriesForQuery.Any())
            {
                var aggregated = new List<ProductModel>();
                foreach (var category in categoriesForQuery)
                {
                    var items = await _getProducts.GetList(category, sort, applySorting: false);
                    aggregated.AddRange(items);
                }

                // A product might appear in multiple descendant categories; de-duplicate before returning.
                var deduplicated = aggregated
                    .GroupBy(p => p.Id)
                    .Select(g => g.First())
                    .ToList();

                return applySorting ? ProductSorting.Apply(deduplicated, sort).ToList() : deduplicated;
            }

            return await _getProducts.GetList(Category, sort, applySorting);
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

        private ProductFilterOptions BuildFilters() =>
            new(null, MinPrice, MaxPrice, Condition, SellerId);

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

        private static int NormalizePage(int page) => page < 1 ? 1 : page;
    }
}
