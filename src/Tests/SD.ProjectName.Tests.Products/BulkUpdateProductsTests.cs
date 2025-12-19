using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;

namespace SD.ProjectName.Tests.Products
{
    public class BulkUpdateProductsTests
    {
        [Fact]
        public async Task ApplyAsync_ShouldUpdateSelectedProducts_WhenChangesAreValid()
        {
            var repository = new Mock<IProductRepository>();
            var products = new List<ProductModel>
            {
                new() { Id = 1, SellerId = "seller", Name = "One", Price = 10m, Stock = 5 },
                new() { Id = 2, SellerId = "seller", Name = "Two", Price = 20m, Stock = 3 }
            };
            repository.Setup(r => r.GetBySeller("seller", true)).ReturnsAsync(products);
            repository.Setup(r => r.UpdateRange(It.IsAny<IEnumerable<ProductModel>>())).Returns(Task.CompletedTask);

            var handler = new BulkUpdateProducts(repository.Object, NullLogger<BulkUpdateProducts>.Instance);
            var request = new BulkUpdateProducts.Request
            {
                ProductIds = new List<int> { 1, 2 },
                PriceOperation = BulkUpdateProducts.PriceOperation.IncreaseByPercentage,
                PriceValue = 10,
                StockOperation = BulkUpdateProducts.StockOperation.DecreaseByAmount,
                StockValue = 1,
                ApplyChanges = true
            };

            var result = await handler.ApplyAsync(request, "seller");

            Assert.True(result.ChangesApplied);
            Assert.Equal(2, result.Updated.Count);
            Assert.Empty(result.Failed);
            repository.Verify(r => r.UpdateRange(It.Is<IEnumerable<ProductModel>>(items =>
                items.Any(p => p.Id == 1 && p.Price == 11m && p.Stock == 4) &&
                items.Any(p => p.Id == 2 && p.Price == 22m && p.Stock == 2)
            )), Times.Once);
        }

        [Fact]
        public async Task ApplyAsync_ShouldSkipInvalidProducts_AndReportFailures()
        {
            var repository = new Mock<IProductRepository>();
            var products = new List<ProductModel>
            {
                new() { Id = 5, SellerId = "seller", Name = "Valid", Price = 12m, Stock = 10 },
                new() { Id = 6, SellerId = "seller", Name = "Invalid", Price = 8m, Stock = 2 }
            };
            repository.Setup(r => r.GetBySeller("seller", true)).ReturnsAsync(products);
            repository.Setup(r => r.UpdateRange(It.IsAny<IEnumerable<ProductModel>>())).Returns(Task.CompletedTask);

            var handler = new BulkUpdateProducts(repository.Object, NullLogger<BulkUpdateProducts>.Instance);
            var request = new BulkUpdateProducts.Request
            {
                ProductIds = new List<int> { 5, 6 },
                PriceOperation = BulkUpdateProducts.PriceOperation.None,
                StockOperation = BulkUpdateProducts.StockOperation.DecreaseByAmount,
                StockValue = 5,
                ApplyChanges = true
            };

            var result = await handler.ApplyAsync(request, "seller");

            Assert.True(result.ChangesApplied);
            Assert.Single(result.Updated);
            Assert.Single(result.Failed);
            Assert.Equal(6, result.Failed[0].ProductId);
            repository.Verify(r => r.UpdateRange(It.Is<IEnumerable<ProductModel>>(items =>
                items.Count() == 1 && items.First().Id == 5 && items.First().Stock == 5
            )), Times.Once);
        }

        [Fact]
        public async Task ApplyAsync_ShouldOnlyPreview_WhenApplyChangesIsFalse()
        {
            var repository = new Mock<IProductRepository>();
            repository.Setup(r => r.GetBySeller("seller", true)).ReturnsAsync(new List<ProductModel>
            {
                new() { Id = 10, SellerId = "seller", Name = "Preview", Price = 5m, Stock = 1 }
            });

            var handler = new BulkUpdateProducts(repository.Object, NullLogger<BulkUpdateProducts>.Instance);
            var request = new BulkUpdateProducts.Request
            {
                ProductIds = new List<int> { 10 },
                PriceOperation = BulkUpdateProducts.PriceOperation.SetTo,
                PriceValue = 9,
                StockOperation = BulkUpdateProducts.StockOperation.IncreaseByAmount,
                StockValue = 2,
                ApplyChanges = false
            };

            var result = await handler.ApplyAsync(request, "seller");

            Assert.False(result.ChangesApplied);
            Assert.Single(result.Updated);
            repository.Verify(r => r.UpdateRange(It.IsAny<IEnumerable<ProductModel>>()), Times.Never);
        }
    }
}
