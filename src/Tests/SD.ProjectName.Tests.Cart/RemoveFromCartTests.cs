using Moq;
using SD.ProjectName.Modules.Cart.Application;
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
            var handler = new RemoveFromCart(repo.Object);
            
            // Act
            await handler.ExecuteAsync(1);
            
            // Assert
            repo.Verify(r => r.RemoveAsync(1), Times.Once);
        }
    }
}
