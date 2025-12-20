using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Infrastructure;

namespace SD.ProjectName.Tests.Products
{
    public class ProductRepositorySearchTests
    {
        [Fact]
        public async Task Search_ShouldReturnOnlyActiveMatches_InNameOrDescription()
        {
            var dbName = $"products-search-{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<ProductDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            await using var context = new ProductDbContext(options);
            context.Products.AddRange(
                new ProductModel { Id = 1, Name = "Red Shoes", Description = "Comfortable running shoes", Status = ProductStatuses.Active, Category = "Shoes", Price = 50, Stock = 5, Sku = "RS-1" },
                new ProductModel { Id = 2, Name = "Blue Hat", Description = "Hat with red stripe", Status = ProductStatuses.Suspended, Category = "Hats", Price = 20, Stock = 2, Sku = "BH-1" },
                new ProductModel { Id = 3, Name = "Trail Backpack", Description = "Backpack with red lining", Status = ProductStatuses.Active, Category = "Bags", Price = 80, Stock = 3, Sku = "TB-1" }
            );
            await context.SaveChangesAsync();

            var repository = new ProductRepository(context);

            var results = await repository.Search("red");

            Assert.Equal(new[] { 1, 3 }, results.Select(r => r.Id).OrderBy(id => id));
            Assert.All(results, r => Assert.Equal(ProductStatuses.Active, r.Status));
        }

        [Fact]
        public async Task Search_ShouldReturnEmpty_WhenKeywordBlank()
        {
            var options = new DbContextOptionsBuilder<ProductDbContext>()
                .UseInMemoryDatabase($"products-search-empty-{Guid.NewGuid():N}")
                .Options;

            await using var context = new ProductDbContext(options);
            var repository = new ProductRepository(context);

            var results = await repository.Search("   ");

            Assert.Empty(results);
        }
    }
}
