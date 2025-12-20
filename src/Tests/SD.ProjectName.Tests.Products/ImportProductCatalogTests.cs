using System.Text;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;
using SD.ProjectName.Modules.Products.Infrastructure;

namespace SD.ProjectName.Tests.Products
{
    public class ImportProductCatalogTests
    {
        private static (ImportProductCatalog importer, IProductRepository repository, ProductDbContext context) BuildSut(string databaseName)
        {
            var options = new DbContextOptionsBuilder<ProductDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            var context = new ProductDbContext(options);
            var repository = new ProductRepository(context);
            var importer = new ImportProductCatalog(repository, context);
            return (importer, repository, context);
        }

        [Fact]
        public async Task PreviewAsync_ShouldShowCreatesAndUpdates()
        {
            var (importer, repository, context) = BuildSut(nameof(PreviewAsync_ShouldShowCreatesAndUpdates));
            await repository.Add(new ProductModel
            {
                Sku = "SKU-1",
                Name = "Existing",
                Category = "Books",
                Price = 10,
                Stock = 5,
                SellerId = "seller-1",
                Status = ProductStatuses.Active
            });

            var csv = """
                      Sku,Title,Price,Stock,Category
                      SKU-1,Updated title,12.5,4,Books
                      SKU-2,New item,19.99,7,Gadgets
                      """;

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            var preview = await importer.PreviewAsync(stream, "import.csv", "seller-1");

            Assert.Equal(2, preview.TotalRows);
            Assert.Equal(1, preview.UpdateCount);
            Assert.Equal(1, preview.CreateCount);
            Assert.Empty(preview.Errors);
        }

        [Fact]
        public async Task ImportAsync_ShouldCreateUpdateAndLogErrors()
        {
            var (importer, repository, context) = BuildSut(nameof(ImportAsync_ShouldCreateUpdateAndLogErrors));
            await repository.Add(new ProductModel
            {
                Sku = "SKU-EXIST",
                Name = "Existing",
                Category = "Books",
                Price = 8,
                Stock = 1,
                SellerId = "seller-1",
                Status = ProductStatuses.Draft
            });

            var csv = """
                      Sku,Title,Price,Stock,Category,Description
                      SKU-EXIST,Updated name,15.00,3,Books,Updated description
                      SKU-NEW,New product,not-a-number,10,Gadgets,Invalid price row
                      SKU-NEW2,Another product,22.50,5,Gadgets,Valid row
                      """;

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            var job = await importer.ImportAsync(stream, "import.csv", "seller-1");

            Assert.Equal(ProductImportStatuses.Completed, job.Status);
            Assert.Equal(3, job.TotalRows);
            Assert.Equal(1, job.UpdatedCount);
            Assert.Equal(1, job.CreatedCount);
            Assert.Equal(1, job.FailedCount);
            Assert.Contains("Price must be a positive number", job.ErrorReport);

            var products = await repository.GetBySeller("seller-1", includeDrafts: true);
            Assert.Equal(2, products.Count);
            var updated = products.Single(p => p.Sku == "SKU-EXIST");
            Assert.Equal("Updated name", updated.Name);
            Assert.Equal(3, updated.Stock);

            var created = products.Single(p => p.Sku == "SKU-NEW2");
            Assert.Equal("Another product", created.Name);
            Assert.Equal(ProductStatuses.Draft, created.Status);
        }
    }
}
