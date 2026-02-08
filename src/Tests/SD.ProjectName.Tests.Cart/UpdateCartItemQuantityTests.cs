using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Tests.Cart
{
    public class UpdateCartItemQuantityTests
    {
        [Fact]
        public async Task ExecuteAsync_ValidQuantity_UpdatesItem()
        {
            // Arrange
            var item = new CartItemModel { Id = 1, BuyerId = "buyer1", ProductId = 1, Quantity = 1 };
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(item);
            var availability = new Mock<IProductAvailabilityService>();
            availability.Setup(a => a.GetAvailableStockAsync(item.ProductId)).ReturnsAsync(10);
            
            var handler = new UpdateCartItemQuantity(repo.Object, availability.Object);

            // Act
            var result = await handler.ExecuteAsync(1, 5);
            
            // Assert
            Assert.True(result);
            Assert.Equal(5, item.Quantity);
            repo.Verify(r => r.UpdateAsync(item), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ZeroQuantity_RemovesItem()
        {
            // Arrange
            var item = new CartItemModel { Id = 1, BuyerId = "buyer1", ProductId = 1, Quantity = 1 };
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            var availability = new Mock<IProductAvailabilityService>();
            var handler = new UpdateCartItemQuantity(repo.Object, availability.Object);
            
            // Act
            var result = await handler.ExecuteAsync(1, 0);
            
            // Assert
            Assert.True(result);
            repo.Verify(r => r.RemoveAsync(1), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ItemNotFound_ReturnsFalse()
        {
            // Arrange
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((CartItemModel?)null);
            var availability = new Mock<IProductAvailabilityService>();
            
            var handler = new UpdateCartItemQuantity(repo.Object, availability.Object);

            // Act
            var result = await handler.ExecuteAsync(1, 5);
            
            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ExecuteAsync_QuantityAboveStock_ClampsToStock()
        {
            // Arrange
            var item = new CartItemModel { Id = 2, BuyerId = "buyer1", ProductId = 2, Quantity = 1 };
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            var availability = new Mock<IProductAvailabilityService>();
            availability.Setup(a => a.GetAvailableStockAsync(item.ProductId)).ReturnsAsync(3);

            var handler = new UpdateCartItemQuantity(repo.Object, availability.Object);

            // Act
            var result = await handler.ExecuteAsync(item.Id, 5);

            // Assert
            Assert.True(result);
            Assert.Equal(3, item.Quantity);
            repo.Verify(r => r.UpdateAsync(item), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NoAvailableStock_RemovesItem()
        {
            // Arrange
            var item = new CartItemModel { Id = 3, BuyerId = "buyer1", ProductId = 3, Quantity = 2 };
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            var availability = new Mock<IProductAvailabilityService>();
            availability.Setup(a => a.GetAvailableStockAsync(item.ProductId)).ReturnsAsync(0);

            var handler = new UpdateCartItemQuantity(repo.Object, availability.Object);

            // Act
            var result = await handler.ExecuteAsync(item.Id, 2);

            // Assert
            Assert.True(result);
            repo.Verify(r => r.RemoveAsync(item.Id), Times.Once);
            repo.Verify(r => r.UpdateAsync(It.IsAny<CartItemModel>()), Times.Never);
        }
    }
}
