using System;
using System.Collections.Generic;
using System.Linq;
using SD.ProjectName.Modules.Products.Domain;

namespace SD.ProjectName.Modules.Products.Application
{
    public record ProductFilterOptions(
        string? Category,
        decimal? MinPrice,
        decimal? MaxPrice,
        string? Condition,
        string? SellerId);

    public static class ProductFiltering
    {
        public static IEnumerable<ProductModel> Apply(IEnumerable<ProductModel> products, ProductFilterOptions? filters)
        {
            if (products is null)
            {
                return Enumerable.Empty<ProductModel>();
            }

            if (filters is null)
            {
                return products;
            }

            var filtered = products;

            var category = Normalize(filters.Category);
            if (category is not null)
            {
                filtered = filtered.Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase));
            }

            if (filters.MinPrice.HasValue)
            {
                filtered = filtered.Where(p => p.Price >= filters.MinPrice.Value);
            }

            if (filters.MaxPrice.HasValue)
            {
                filtered = filtered.Where(p => p.Price <= filters.MaxPrice.Value);
            }

            var condition = Normalize(filters.Condition);
            if (condition is not null)
            {
                filtered = filtered.Where(p => string.Equals(p.Condition, condition, StringComparison.OrdinalIgnoreCase));
            }

            var sellerId = Normalize(filters.SellerId);
            if (sellerId is not null)
            {
                filtered = filtered.Where(p => string.Equals(p.SellerId, sellerId, StringComparison.OrdinalIgnoreCase));
            }

            return filtered;
        }

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
