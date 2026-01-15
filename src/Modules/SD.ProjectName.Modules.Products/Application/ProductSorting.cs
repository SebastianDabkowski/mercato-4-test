using System.Collections.Generic;
using System.Linq;
using SD.ProjectName.Modules.Products.Domain;

namespace SD.ProjectName.Modules.Products.Application
{
    public enum ProductSort
    {
        Relevance,
        PriceAsc,
        PriceDesc,
        Newest
    }

    public static class ProductSorting
    {
        public static IEnumerable<ProductModel> Apply(IEnumerable<ProductModel> products, ProductSort sort, string? keyword = null)
        {
            var list = products.ToList();
            return sort switch
            {
                ProductSort.PriceAsc => list.OrderBy(p => p.Price).ThenBy(p => p.Id),
                ProductSort.PriceDesc => list.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
                ProductSort.Newest => list.OrderByDescending(p => p.Id),
                ProductSort.Relevance => ApplyRelevance(list, keyword),
                _ => list
            };
        }

        private static IEnumerable<ProductModel> ApplyRelevance(IReadOnlyList<ProductModel> products, string? keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return products.OrderBy(p => p.Name).ThenBy(p => p.Id);
            }

            return products
                .Select(p => new
                {
                    Product = p,
                    NameMatch = ContainsInsensitive(p.Name, keyword),
                    DescriptionMatch = ContainsInsensitive(p.Description, keyword)
                })
                .OrderByDescending(p => p.NameMatch)
                .ThenByDescending(p => p.DescriptionMatch)
                .ThenBy(p => p.Product.Name)
                .ThenBy(p => p.Product.Id)
                .Select(p => p.Product);
        }

        private static bool ContainsInsensitive(string? source, string keyword) =>
            !string.IsNullOrWhiteSpace(source) &&
            source.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
