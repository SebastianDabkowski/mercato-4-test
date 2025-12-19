using Moq;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;

namespace SD.ProjectName.Tests.Products
{
    public class UpdateProductTests
    {
        [Fact]
        public async Task UpdateAsync_ShouldApplyChanges_ForOwner()
        {
            var repository = new Mock<IProductRepository>();
            var existing = new ProductModel
            {
                Id = 5,
                SellerId = "seller-5",
                Name = "Old",
                Description = "Old description",
                Category = "Old",
                Price = 10,
                Stock = 1,
                ImageUrls = "old",
                ShippingMethods = "old",
                WeightKg = 1,
                LengthCm = 1,
                WidthCm = 1,
                HeightCm = 1
            };

            repository.Setup(r => r.GetById(existing.Id)).ReturnsAsync(existing);
            repository.Setup(r => r.Update(existing)).Returns(Task.CompletedTask);

            var handler = new UpdateProduct(repository.Object);
            var request = new UpdateProduct.Request
            {
                Title = "Updated",
                Description = "Updated description",
                Category = "Books",
                Price = 20,
                Stock = 3,
                ImageUrls = "https://example.com/img\nhttps://example.com/img2",
                ShippingMethods = "Courier\nLocker",
                WeightKg = 2.5m,
                LengthCm = 10,
                WidthCm = 5,
                HeightCm = 2,
                Publish = true
            };

            var result = await handler.UpdateAsync(existing.Id, request, existing.SellerId);

            Assert.NotNull(result);
            Assert.Equal(request.Title, result!.Name);
            Assert.Equal(request.Description, result.Description);
            Assert.Equal(request.Category, result.Category);
            Assert.Equal(request.Price, result.Price);
            Assert.Equal(request.Stock, result.Stock);
            Assert.Equal("https://example.com/img\nhttps://example.com/img2", result.ImageUrls);
            Assert.Equal(request.ShippingMethods, result.ShippingMethods);
            Assert.Equal(request.WeightKg, result.WeightKg);
            Assert.Equal(request.LengthCm, result.LengthCm);
            Assert.Equal(request.WidthCm, result.WidthCm);
            Assert.Equal(request.HeightCm, result.HeightCm);
            Assert.Equal(ProductStatuses.Active, result.Status);
            repository.Verify(r => r.Update(existing), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldSuspend_WhenUnpublishingActiveProduct()
        {
            var repository = new Mock<IProductRepository>();
            var existing = new ProductModel
            {
                Id = 12,
                SellerId = "seller-12",
                Name = "Published",
                Category = "Books",
                Description = "Desc",
                Price = 5,
                Stock = 2,
                ImageUrls = "https://example.com/img",
                Status = ProductStatuses.Active
            };

            repository.Setup(r => r.GetById(existing.Id)).ReturnsAsync(existing);
            repository.Setup(r => r.Update(existing)).Returns(Task.CompletedTask);

            var handler = new UpdateProduct(repository.Object);
            var request = new UpdateProduct.Request
            {
                Title = existing.Name,
                Description = existing.Description,
                Category = existing.Category,
                Price = existing.Price,
                Stock = existing.Stock,
                ImageUrls = existing.ImageUrls,
                Publish = false
            };

            var result = await handler.UpdateAsync(existing.Id, request, existing.SellerId);

            Assert.Equal(ProductStatuses.Suspended, result!.Status);
            repository.Verify(r => r.Update(existing), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenActivatingWithMissingRequiredFields()
        {
            var repository = new Mock<IProductRepository>();
            var existing = new ProductModel
            {
                Id = 25,
                SellerId = "seller-25",
                Status = ProductStatuses.Draft
            };

            repository.Setup(r => r.GetById(existing.Id)).ReturnsAsync(existing);

            var handler = new UpdateProduct(repository.Object);
            var request = new UpdateProduct.Request
            {
                Title = "Draft product",
                Category = "Home",
                Price = 10,
                Stock = 0,
                Description = "",
                ImageUrls = " ",
                Publish = true
            };

            await Assert.ThrowsAsync<UpdateProduct.ProductActivationException>(() =>
                handler.UpdateAsync(existing.Id, request, existing.SellerId));

            repository.Verify(r => r.Update(It.IsAny<ProductModel>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNull_WhenNotOwner()
        {
            var repository = new Mock<IProductRepository>();
            repository.Setup(r => r.GetById(10)).ReturnsAsync(new ProductModel
            {
                Id = 10,
                SellerId = "owner"
            });

            var handler = new UpdateProduct(repository.Object);
            var request = new UpdateProduct.Request
            {
                Title = "Updated",
                Category = "Books",
                Price = 10,
                Stock = 1
            };

            var result = await handler.UpdateAsync(10, request, "different-seller");

            Assert.Null(result);
            repository.Verify(r => r.Update(It.IsAny<ProductModel>()), Times.Never);
        }
    }
}
