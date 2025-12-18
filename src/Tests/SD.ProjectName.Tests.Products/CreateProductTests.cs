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
                Stock = 10
            };
            var sellerId = "seller-123";

            // Act
            var result = await handler.CreateAsync(request, sellerId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("draft", result.Status);
            Assert.Equal(sellerId, result.SellerId);
            Assert.Equal(request.Title, result.Name);
            Assert.Equal(request.Category, result.Category);
            Assert.Equal(request.Price, result.Price);
            Assert.Equal(request.Stock, result.Stock);
            repository.Verify(r => r.Add(It.IsAny<ProductModel>()), Times.Once);
            Assert.Equal(saved, result);
        }
    }
}
