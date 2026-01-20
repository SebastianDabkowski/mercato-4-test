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
        private const int MaxSearchTermLength = 200;
        private const int PageSize = 10;
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

        [BindProperty(SupportsGet = true, Name = "page")]
        public int PageNumber { get; set; } = 1;

        public List<ProductListItem> Results { get; private set; } = new();

        public IReadOnlyList<CategoryManagement.CategoryOption> CategoryOptions { get; private set; } = Array.Empty<CategoryManagement.CategoryOption>();

        public IReadOnlyList<string> ConditionOptions { get; private set; } = Array.Empty<string>();

        public IReadOnlyList<string> SellerOptions { get; private set; } = Array.Empty<string>();

        public IReadOnlyList<ActiveFilter> ActiveFilters { get; private set; } = Array.Empty<ActiveFilter>();

        public IReadOnlyList<int> PageNumbers { get; private set; } = Array.Empty<int>();

        public int TotalResults { get; private set; }

        public int TotalPages { get; private set; }

        public bool HasQuery => !string.IsNullOrWhiteSpace(Q);

        public bool HasActiveFilters => ActiveFilters.Any();

        public bool HasNextPage => PageNumber < TotalPages;

        public bool HasPreviousPage => PageNumber > 1;

        public int StartResult => TotalResults == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;

        public int EndResult => TotalResults == 0 ? 0 : Math.Min(PageNumber * PageSize, TotalResults);

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
            if (normalized.Length > MaxSearchTermLength)
            {
                normalized = normalized[..MaxSearchTermLength];
            }

            Q = normalized;

            PageNumber = NormalizePage(PageNumber);
            var products = await _getProducts.Search(normalized, Sort.Value);
            var baseList = products.ToList();
            ConditionOptions = BuildConditionOptions(baseList);
            SellerOptions = BuildSellerOptions(baseList);
            var filtered = ProductFiltering.Apply(baseList, BuildFilters()).ToList();
            TotalResults = filtered.Count;
            TotalPages = (int)Math.Ceiling(TotalResults / (double)PageSize);
            if (TotalPages > 0)
            {
                PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
            }

            PageNumbers = TotalPages > 0
                ? Enumerable.Range(1, TotalPages).ToList()
                : Array.Empty<int>();
            Results = filtered.Skip((PageNumber - 1) * PageSize).Take(PageSize).Select(p =>
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

            return trimmed.Length > MaxSearchTermLength ? trimmed[..MaxSearchTermLength] : trimmed;
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

        private static int NormalizePage(int page) => page < 1 ? 1 : page;
    }
}
