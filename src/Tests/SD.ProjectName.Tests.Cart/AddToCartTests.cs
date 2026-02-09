using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Tests.Cart
{
    public class AddToCartTests
    {
        [Fact]
        public async Task ExecuteAsync_NewItem_AddsToCart()
        {
            // Arrange
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByBuyerAndProductAsync("buyer1", 1)).ReturnsAsync((CartItemModel?)null);
            repo.Setup(r => r.AddAsync(It.IsAny<CartItemModel>())).ReturnsAsync((CartItemModel i) => i);
            
            var handler = new AddToCart(repo.Object);

            // Act
            var result = await handler.ExecuteAsync("buyer1", 1, "Product1", "Category", 10m, "seller1", "Store1");
            
            // Assert
            Assert.Equal("buyer1", result.BuyerId);
            Assert.Equal(1, result.ProductId);
            Assert.Equal(1, result.Quantity);
            Assert.Equal("seller1", result.SellerId);
            repo.Verify(r => r.AddAsync(It.IsAny<CartItemModel>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ExistingItem_IncreasesQuantity()
        {
            // Arrange
            var existing = new CartItemModel { Id = 1, BuyerId = "buyer1", ProductId = 1, Quantity = 2, SellerId = "seller1" };
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByBuyerAndProductAsync("buyer1", 1)).ReturnsAsync(existing);
            
            var handler = new AddToCart(repo.Object);

            // Act
            var result = await handler.ExecuteAsync("buyer1", 1, "Product1", "Category", 10m, "seller1", "Store1");
            
            // Assert
            Assert.Equal(3, result.Quantity);
            repo.Verify(r => r.UpdateAsync(existing), Times.Once);
            repo.Verify(r => r.AddAsync(It.IsAny<CartItemModel>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_DifferentSellers_AddsBothItems()
        {
            // Arrange
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByBuyerAndProductAsync("buyer1", It.IsAny<int>())).ReturnsAsync((CartItemModel?)null);
            repo.Setup(r => r.AddAsync(It.IsAny<CartItemModel>())).ReturnsAsync((CartItemModel i) => i);
            
            var handler = new AddToCart(repo.Object);
            
            // Act
            var result1 = await handler.ExecuteAsync("buyer1", 1, "Product1", "Category", 10m, "seller1", "Store1");
            var result2 = await handler.ExecuteAsync("buyer1", 2, "Product2", "Category", 20m, "seller2", "Store2");
            
            // Assert
            Assert.Equal("seller1", result1.SellerId);
            Assert.Equal("seller2", result2.SellerId);
            repo.Verify(r => r.AddAsync(It.IsAny<CartItemModel>()), Times.Exactly(2));
        }

        [Fact]
        public async Task ExecuteAsync_InvalidQuantity_ThrowsArgumentException()
        {
            // Arrange
            var repo = new Mock<ICartRepository>();
            var handler = new AddToCart(repo.Object);
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                handler.ExecuteAsync("buyer1", 1, "Product1", "Category", 10m, "seller1", "Store1", quantity: 0));
        }
    }
}
