using System.Text;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Infrastructure;

namespace SD.ProjectName.Tests.Products
{
    public class ExportProductCatalogTests
    {
        private const int DefaultThreshold = 50;
        private const int SmallThreshold = 2;

        [Fact]
        public async Task ExportAsync_ShouldApplyFiltersAndReturnCsv()
        {
            var options = new DbContextOptionsBuilder<ProductDbContext>()
                .UseInMemoryDatabase(nameof(ExportAsync_ShouldApplyFiltersAndReturnCsv))
                .Options;

            var context = new ProductDbContext(options);
            var repository = new ProductRepository(context);

            await repository.AddRange(new[]
            {
                new ProductModel { SellerId = "seller-1", Name = "Book One", Sku = "BOOK-1", Category = "Books", Price = 10, Stock = 5, Status = ProductStatuses.Active },
                new ProductModel { SellerId = "seller-1", Name = "Gadget", Sku = "GAD-1", Category = "Gadgets", Price = 20, Stock = 2, Status = ProductStatuses.Active },
                new ProductModel { SellerId = "seller-2", Name = "Other Seller", Sku = "OTHER", Category = "Books", Price = 5, Stock = 1, Status = ProductStatuses.Active }
            });

            var exporter = new ExportProductCatalog(repository, context, backgroundThreshold: DefaultThreshold);
            var request = new ExportProductCatalog.ExportRequest
            {
                Format = ExportProductCatalog.ExportFormat.Csv,
                ApplyFilters = true,
                Category = "Books",
                Search = "Book",
                IncludeDrafts = true
            };

            var result = await exporter.ExportAsync(request, "seller-1");

            Assert.False(result.QueuedAsJob);
            Assert.NotNull(result.FileContent);
            Assert.Equal(1, result.ExportedCount);

            var content = Encoding.UTF8.GetString(result.FileContent!);
            Assert.Contains("Sku,Title", content);
            Assert.Contains("Book One", content);
            Assert.DoesNotContain("Gadget", content);
            Assert.DoesNotContain("Other Seller", content);
        }

        [Fact]
        public async Task ExportAsync_ShouldQueueBackgroundJobAndAllowDownload()
        {
            var options = new DbContextOptionsBuilder<ProductDbContext>()
                .UseInMemoryDatabase(nameof(ExportAsync_ShouldQueueBackgroundJobAndAllowDownload))
                .Options;

            var context = new ProductDbContext(options);
            var repository = new ProductRepository(context);

            await repository.AddRange(new[]
            {
                new ProductModel { SellerId = "seller-1", Name = "One", Sku = "ONE", Category = "Books", Price = 10, Stock = 5, Status = ProductStatuses.Active },
                new ProductModel { SellerId = "seller-1", Name = "Two", Sku = "TWO", Category = "Books", Price = 12, Stock = 4, Status = ProductStatuses.Active },
                new ProductModel { SellerId = "seller-1", Name = "Three", Sku = "THREE", Category = "Books", Price = 15, Stock = 7, Status = ProductStatuses.Active }
            });

            var exporter = new ExportProductCatalog(repository, context, backgroundThreshold: SmallThreshold);

            var result = await exporter.ExportAsync(new ExportProductCatalog.ExportRequest
            {
                Format = ExportProductCatalog.ExportFormat.Xls,
                ApplyFilters = false,
                IncludeDrafts = true
            }, "seller-1");

            Assert.True(result.QueuedAsJob);
            Assert.NotNull(result.JobId);
            Assert.Null(result.FileContent);
            Assert.NotNull(result.DownloadLink);
            Assert.Equal(3, result.ExportedCount);

            var job = await context.ExportJobs.SingleAsync();
            Assert.Equal(ProductExportStatuses.Completed, job.Status);
            Assert.Equal(3, job.TotalRows);
            Assert.NotNull(job.FileContent);

            var download = await exporter.DownloadAsync(job.Id, "seller-1");

            Assert.NotNull(download);
            var content = Encoding.UTF8.GetString(download!.FileContent);
            Assert.Contains("Three", content);
            Assert.False(string.IsNullOrWhiteSpace(result.DownloadLink));
        }
    }
}
