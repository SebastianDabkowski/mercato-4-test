using Moq;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;

namespace SD.ProjectName.Tests.Products
{
    public class CreateProductTests
    {
        [Fact]
        public async Task CreateAsync_ShouldPersistProduct_WithDraftStatusAndSeller()
        {
            // Arrange
            var repository = new Mock<IProductRepository>();
            ProductModel? saved = null;
            repository.Setup(r => r.Add(It.IsAny<ProductModel>()))
                .ReturnsAsync((ProductModel p) =>
                {
                    saved = p;
                    return p;
                });

            var handler = new CreateProduct(repository.Object);
            var request = new CreateProduct.Request
            {
                Title = "New Product",
                Category = "Books",
                Description = "A great book",
                Price = 19.99m,
                Stock = 10,
                ImageUrls = "https://example.com/1\n https://example.com/2 ",
                WeightKg = 1.2m,
                LengthCm = 10.5m,
                WidthCm = 5.2m,
                HeightCm = 3.0m,
                ShippingMethods = "Courier\nParcel locker"
            };
            var sellerId = "seller-123";

            // Act
            var result = await handler.CreateAsync(request, sellerId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ProductStatuses.Draft, result.Status);
            Assert.Equal(sellerId, result.SellerId);
            Assert.Equal(request.Title, result.Name);
            Assert.Equal(request.Category, result.Category);
            Assert.Equal(request.Price, result.Price);
            Assert.Equal(request.Stock, result.Stock);
            Assert.Equal("https://example.com/1\nhttps://example.com/2", result.ImageUrls);
            Assert.Equal(request.WeightKg, result.WeightKg);
            Assert.Equal(request.LengthCm, result.LengthCm);
            Assert.Equal(request.WidthCm, result.WidthCm);
            Assert.Equal(request.HeightCm, result.HeightCm);
            Assert.Equal("Courier\nParcel locker", result.ShippingMethods);
            repository.Verify(r => r.Add(It.IsAny<ProductModel>()), Times.Once);
            Assert.Equal(saved, result);
        }
    }
}
