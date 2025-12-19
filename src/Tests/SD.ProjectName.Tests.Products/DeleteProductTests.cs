using Moq;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;

namespace SD.ProjectName.Tests.Products
{
    public class DeleteProductTests
    {
        [Fact]
        public async Task ArchiveAsync_ShouldArchive_WhenOwner()
        {
            var repository = new Mock<IProductRepository>();
            var product = new ProductModel
            {
                Id = 7,
                SellerId = "seller-7",
                Status = ProductStatuses.Active
            };

            repository.Setup(r => r.GetById(product.Id)).ReturnsAsync(product);
            repository.Setup(r => r.Update(product)).Returns(Task.CompletedTask);

            var handler = new DeleteProduct(repository.Object);

            var result = await handler.ArchiveAsync(product.Id, product.SellerId);

            Assert.True(result);
            Assert.Equal(ProductStatuses.Archived, product.Status);
            repository.Verify(r => r.Update(product), Times.Once);
        }

        [Fact]
        public async Task ArchiveAsync_ShouldReturnFalse_WhenNotOwner()
        {
            var repository = new Mock<IProductRepository>();
            repository.Setup(r => r.GetById(9)).ReturnsAsync(new ProductModel
            {
                Id = 9,
                SellerId = "owner",
                Status = ProductStatuses.Active
            });

            var handler = new DeleteProduct(repository.Object);

            var result = await handler.ArchiveAsync(9, "other");

            Assert.False(result);
            repository.Verify(r => r.Update(It.IsAny<ProductModel>()), Times.Never);
        }
    }
}
