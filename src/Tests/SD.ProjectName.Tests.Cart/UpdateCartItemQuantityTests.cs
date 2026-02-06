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
            
            var handler = new UpdateCartItemQuantity(repo.Object);

            // Act
            var result = await handler.ExecuteAsync(1, 5);
            
            // Assert
            Assert.True(result);
            Assert.Equal(5, item.Quantity);
            repo.Verify(r => r.UpdateAsync(item), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_InvalidQuantity_ReturnsFalse()
        {
            // Arrange
            var repo = new Mock<ICartRepository>();
            var handler = new UpdateCartItemQuantity(repo.Object);
            
            // Act
            var result = await handler.ExecuteAsync(1, 0);
            
            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ExecuteAsync_ItemNotFound_ReturnsFalse()
        {
            // Arrange
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((CartItemModel?)null);
            
            var handler = new UpdateCartItemQuantity(repo.Object);

            // Act
            var result = await handler.ExecuteAsync(1, 5);
            
            // Assert
            Assert.False(result);
        }
    }
}
