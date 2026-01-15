using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;

namespace SD.ProjectName.Tests.Products
{
    public class ProductFilteringTests
    {
        [Fact]
        public void Apply_ShouldFilterByCategoryPriceConditionAndSeller()
        {
            var products = new List<ProductModel>
            {
                new() { Id = 1, Name = "Item 1", Category = "Books", Price = 15m, SellerId = "seller-1", Condition = ProductConditions.New },
                new() { Id = 2, Name = "Item 2", Category = "Books", Price = 25m, SellerId = "seller-2", Condition = ProductConditions.Used },
                new() { Id = 3, Name = "Item 3", Category = "Games", Price = 30m, SellerId = "seller-2", Condition = ProductConditions.Used }
            };

            var filters = new ProductFilterOptions("Books", 20m, 30m, ProductConditions.Used, "seller-2");

            var results = ProductFiltering.Apply(products, filters).ToList();

            Assert.Single(results);
            Assert.Equal(2, results[0].Id);
        }
    }
}
