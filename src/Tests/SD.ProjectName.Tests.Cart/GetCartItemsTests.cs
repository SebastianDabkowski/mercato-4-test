using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Tests.Cart
{
    public class GetCartItemsTests
    {
        [Fact]
        public async Task ExecuteAsync_ReturnsItemsForBuyer()
        {
            // Arrange
            var items = new List<CartItemModel>
            {
                new() { Id = 1, BuyerId = "buyer1", ProductId = 1, SellerId = "seller1", SellerName = "Store1", Quantity = 1 },
                new() { Id = 2, BuyerId = "buyer1", ProductId = 2, SellerId = "seller2", SellerName = "Store2", Quantity = 3 }
            };
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByBuyerIdAsync("buyer1")).ReturnsAsync(items);
            
            var handler = new GetCartItems(repo.Object);

            // Act
            var result = await handler.ExecuteAsync("buyer1");
            
            // Assert
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task ExecuteAsync_EmptyCart_ReturnsEmptyList()
        {
            // Arrange
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByBuyerIdAsync("buyer1")).ReturnsAsync(new List<CartItemModel>());
            
            var handler = new GetCartItems(repo.Object);

            // Act
            var result = await handler.ExecuteAsync("buyer1");
            
            // Assert
            Assert.Empty(result);
        }
    }
}
