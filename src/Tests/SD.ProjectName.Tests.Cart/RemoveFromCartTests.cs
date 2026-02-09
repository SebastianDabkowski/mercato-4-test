using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Tests.Cart
{
    public class RemoveFromCartTests
    {
        [Fact]
        public async Task ExecuteAsync_CallsRepositoryRemove()
        {
            // Arrange
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new CartItemModel { Id = 1, BuyerId = "buyer1" });
            var handler = new RemoveFromCart(repo.Object);
            
            // Act
            await handler.ExecuteAsync(1, "buyer1");
            
            // Assert
            repo.Verify(r => r.RemoveAsync(1), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_IgnoresItemForDifferentBuyer()
        {
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new CartItemModel { Id = 1, BuyerId = "buyer2" });
            var handler = new RemoveFromCart(repo.Object);

            await handler.ExecuteAsync(1, "buyer1");

            repo.Verify(r => r.RemoveAsync(It.IsAny<int>()), Times.Never);
        }
    }
}
