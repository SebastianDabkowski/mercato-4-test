using System;
using System.Linq;
using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Tests.Cart
{
    public class DeliveryAddressBookTests
    {
        [Fact]
        public async Task SaveAsync_NewAddress_SetsSavedAndDefault()
        {
            var repository = new Mock<ICartRepository>();
            repository.Setup(r => r.AddOrUpdateAddressAsync(It.IsAny<DeliveryAddressModel>()))
                .ReturnsAsync((DeliveryAddressModel a) =>
                {
                    a.Id = 10;
                    return a;
                });

            var provider = new TestTimeProvider { UtcNowValue = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
            var service = new DeliveryAddressBook(repository.Object, provider);

            var input = new DeliveryAddressInput("Jane Doe", "Street 1", null, "Warsaw", "Mazowieckie", "00-001", "PL", "123456789");
            var result = await service.SaveAsync("buyer-1", null, input, setAsDefault: true);

            Assert.True(result.Success);
            Assert.NotNull(result.Address);
            Assert.True(result.Address!.SavedToProfile);
            Assert.True(result.Address.IsSelectedForCheckout);
            Assert.Equal(provider.UtcNowValue, result.Address.CreatedAt);
            repository.Verify(r => r.ClearSelectedAddressAsync("buyer-1"), Times.Once);
        }

        [Fact]
        public async Task SaveAsync_InvalidOwner_ReturnsError()
        {
            var repository = new Mock<ICartRepository>();
            repository.Setup(r => r.GetAddressAsync(5))
                .ReturnsAsync(new DeliveryAddressModel { Id = 5, BuyerId = "someone-else" });

            var service = new DeliveryAddressBook(repository.Object, TimeProvider.System);
            var input = new DeliveryAddressInput("Jane", "Line", null, "City", "Region", "12345", "PL", null);

            var result = await service.SaveAsync("buyer-1", 5, input, setAsDefault: false);

            Assert.False(result.Success);
            Assert.Contains("Address not found.", result.Errors);
            repository.Verify(r => r.AddOrUpdateAddressAsync(It.IsAny<DeliveryAddressModel>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_BlocksWhenActiveOrder()
        {
            var repository = new Mock<ICartRepository>();
            var address = new DeliveryAddressModel { Id = 3, BuyerId = "buyer-1" };
            repository.Setup(r => r.GetAddressAsync(address.Id)).ReturnsAsync(address);
            repository.Setup(r => r.IsAddressUsedInActiveOrderAsync("buyer-1", address)).ReturnsAsync(true);

            var service = new DeliveryAddressBook(repository.Object, TimeProvider.System);

            var result = await service.DeleteAsync("buyer-1", address.Id);

            Assert.False(result.Success);
            Assert.Contains("active order", result.Errors.First(), StringComparison.OrdinalIgnoreCase);
            repository.Verify(r => r.DeleteAddressAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SetDefaultAsync_SetsSelection()
        {
            var repository = new Mock<ICartRepository>();
            var address = new DeliveryAddressModel { Id = 2, BuyerId = "buyer-1", CountryCode = "PL" };
            repository.Setup(r => r.GetAddressAsync(address.Id)).ReturnsAsync(address);
            repository.Setup(r => r.AddOrUpdateAddressAsync(address)).ReturnsAsync(address);

            var provider = new TestTimeProvider { UtcNowValue = new DateTimeOffset(2025, 2, 1, 12, 0, 0, TimeSpan.Zero) };
            var service = new DeliveryAddressBook(repository.Object, provider);

            var result = await service.SetDefaultAsync("buyer-1", address.Id);

            Assert.True(result.Success);
            Assert.True(address.IsSelectedForCheckout);
            Assert.Equal(provider.UtcNowValue, address.UpdatedAt);
            repository.Verify(r => r.ClearSelectedAddressAsync("buyer-1"), Times.Once);
        }

        private sealed class TestTimeProvider : TimeProvider
        {
            public DateTimeOffset UtcNowValue { get; set; }

            public override DateTimeOffset GetUtcNow() => UtcNowValue;
        }
    }
}
