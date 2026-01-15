using System;
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
            return sort switch
            {
                ProductSort.PriceAsc => products.OrderBy(p => p.Price).ThenBy(p => p.Id),
                ProductSort.PriceDesc => products.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
                ProductSort.Newest => products.OrderByDescending(p => p.Id),
                ProductSort.Relevance => ApplyRelevance(products, keyword),
                _ => products
            };
        }

        private static IEnumerable<ProductModel> ApplyRelevance(IEnumerable<ProductModel> products, string? keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return products.OrderByDescending(p => p.Id);
            }

            var trimmedKeyword = keyword.Trim();
            return products
                .Select(p => new
                {
                    Product = p,
                    NameMatch = ContainsInsensitive(p.Name, trimmedKeyword),
                    DescriptionMatch = ContainsInsensitive(p.Description, trimmedKeyword)
                })
                .OrderByDescending(p => p.NameMatch)
                .ThenByDescending(p => p.DescriptionMatch)
                .ThenBy(p => p.Product.Name)
                .ThenBy(p => p.Product.Id)
                .Select(p => p.Product);
        }

        private static bool ContainsInsensitive(string? source, string keyword) =>
            !string.IsNullOrWhiteSpace(source) &&
            !string.IsNullOrWhiteSpace(keyword) &&
            source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
