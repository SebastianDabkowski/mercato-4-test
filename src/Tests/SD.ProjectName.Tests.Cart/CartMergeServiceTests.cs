using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Tests.Cart
{
    public class CartMergeServiceTests
    {
        [Fact]
        public async Task MergeAsync_MovesGuestItemsToUser()
        {
            var guestItem = new CartItemModel { Id = 1, BuyerId = "guest-1", ProductId = 10, Quantity = 2 };
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByBuyerIdAsync("guest-1")).ReturnsAsync(new List<CartItemModel> { guestItem });
            repo.Setup(r => r.GetByBuyerIdAsync("user-1")).ReturnsAsync(new List<CartItemModel>());

            var availability = new Mock<IProductAvailabilityService>();
            availability.Setup(a => a.GetAvailableStockAsync(guestItem.ProductId)).ReturnsAsync(5);

            var service = new CartMergeService(repo.Object, availability.Object);

            await service.MergeAsync("guest-1", "user-1");

            Assert.Equal("user-1", guestItem.BuyerId);
            Assert.Equal(2, guestItem.Quantity);
            repo.Verify(r => r.UpdateAsync(guestItem), Times.Once);
            repo.Verify(r => r.RemoveAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task MergeAsync_SumsQuantitiesWithClamp()
        {
            var guestItem = new CartItemModel { Id = 2, BuyerId = "guest-1", ProductId = 20, Quantity = 3 };
            var userItem = new CartItemModel { Id = 3, BuyerId = "user-1", ProductId = 20, Quantity = 2 };
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByBuyerIdAsync("guest-1")).ReturnsAsync(new List<CartItemModel> { guestItem });
            repo.Setup(r => r.GetByBuyerIdAsync("user-1")).ReturnsAsync(new List<CartItemModel> { userItem });

            var availability = new Mock<IProductAvailabilityService>();
            availability.Setup(a => a.GetAvailableStockAsync(guestItem.ProductId)).ReturnsAsync(4);

            var service = new CartMergeService(repo.Object, availability.Object);

            await service.MergeAsync("guest-1", "user-1");

            Assert.Equal(4, userItem.Quantity);
            repo.Verify(r => r.UpdateAsync(userItem), Times.Once);
            repo.Verify(r => r.RemoveAsync(guestItem.Id), Times.Once);
        }

        [Fact]
        public async Task MergeAsync_RemovesOutOfStockGuestItems()
        {
            var guestItem = new CartItemModel { Id = 4, BuyerId = "guest-1", ProductId = 30, Quantity = 1 };
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByBuyerIdAsync("guest-1")).ReturnsAsync(new List<CartItemModel> { guestItem });
            repo.Setup(r => r.GetByBuyerIdAsync("user-1")).ReturnsAsync(new List<CartItemModel>());

            var availability = new Mock<IProductAvailabilityService>();
            availability.Setup(a => a.GetAvailableStockAsync(guestItem.ProductId)).ReturnsAsync(0);

            var service = new CartMergeService(repo.Object, availability.Object);

            await service.MergeAsync("guest-1", "user-1");

            repo.Verify(r => r.RemoveAsync(guestItem.Id), Times.Once);
            repo.Verify(r => r.UpdateAsync(It.IsAny<CartItemModel>()), Times.Never);
        }
    }
}
