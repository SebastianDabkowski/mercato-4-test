using Microsoft.EntityFrameworkCore;
using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.Modules.Cart.Infrastructure;

namespace SD.ProjectName.Tests.Cart
{
    public class DeliveryAddressTests
    {
        [Fact]
        public async Task SaveNewAsync_ValidAddress_SavesAndSelects()
        {
            var repository = new Mock<ICartRepository>();
            repository.Setup(r => r.AddOrUpdateAddressAsync(It.IsAny<DeliveryAddressModel>()))
                .ReturnsAsync((DeliveryAddressModel a) => a);

            var handler = new SetDeliveryAddressForCheckout(repository.Object, TimeProvider.System);
            var input = new DeliveryAddressInput("Jane Doe", "Main St 1", null, "Warsaw", "Mazowieckie", "00-001", "PL", "123456789");

            var result = await handler.SaveNewAsync("buyer-1", input, saveToProfile: true);

            Assert.True(result.Success);
            Assert.NotNull(result.Address);
            Assert.True(result.Address!.IsSelectedForCheckout);
            Assert.True(result.Address.SavedToProfile);
            repository.Verify(r => r.ClearSelectedAddressAsync("buyer-1"), Times.Once);
            repository.Verify(r => r.AddOrUpdateAddressAsync(It.Is<DeliveryAddressModel>(a => a.BuyerId == "buyer-1" && a.RecipientName == "Jane Doe")), Times.Once);
        }

        [Fact]
        public async Task SaveNewAsync_UnsupportedCountry_ReturnsErrors()
        {
            var repository = new Mock<ICartRepository>();
            var handler = new SetDeliveryAddressForCheckout(repository.Object, TimeProvider.System);
            var input = new DeliveryAddressInput("Jane Doe", "Main St 1", null, "Warsaw", "Mazowieckie", "00-001", "CN", null);

            var result = await handler.SaveNewAsync("buyer-1", input, saveToProfile: false);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("Items cannot be shipped"));
            repository.Verify(r => r.AddOrUpdateAddressAsync(It.IsAny<DeliveryAddressModel>()), Times.Never);
        }

        [Fact]
        public async Task SelectExistingAsync_MarksSelected()
        {
            var repository = new Mock<ICartRepository>();
            var existing = new DeliveryAddressModel
            {
                Id = 5,
                BuyerId = "buyer-1",
                CountryCode = "PL"
            };

            repository.Setup(r => r.GetAddressAsync(existing.Id)).ReturnsAsync(existing);
            repository.Setup(r => r.AddOrUpdateAddressAsync(It.IsAny<DeliveryAddressModel>()))
                .ReturnsAsync((DeliveryAddressModel a) => a);

            var handler = new SetDeliveryAddressForCheckout(repository.Object, TimeProvider.System);

            var result = await handler.SelectExistingAsync("buyer-1", existing.Id);

            Assert.True(result.Success);
            Assert.True(existing.IsSelectedForCheckout);
            repository.Verify(r => r.ClearSelectedAddressAsync("buyer-1"), Times.Once);
            repository.Verify(r => r.AddOrUpdateAddressAsync(existing), Times.Once);
        }

        [Fact]
        public async Task GetSelectedAddressAsync_SelectsMostRecent_WhenNoneMarked()
        {
            var options = new DbContextOptionsBuilder<CartDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var context = new CartDbContext(options);
            var repo = new CartRepository(context);

            var older = new DeliveryAddressModel
            {
                BuyerId = "buyer-2",
                RecipientName = "Older",
                Line1 = "Line 1",
                City = "City",
                Region = "Region",
                PostalCode = "00-001",
                CountryCode = "PL",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            };

            var newer = new DeliveryAddressModel
            {
                BuyerId = "buyer-2",
                RecipientName = "Newer",
                Line1 = "Line 2",
                City = "City 2",
                Region = "Region 2",
                PostalCode = "00-002",
                CountryCode = "PL",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            context.DeliveryAddresses.AddRange(older, newer);
            await context.SaveChangesAsync();

            var selected = await repo.GetSelectedAddressAsync("buyer-2");

            Assert.NotNull(selected);
            Assert.Equal("Newer", selected!.RecipientName);
            Assert.True(selected.IsSelectedForCheckout);

            var selectedCount = await context.DeliveryAddresses
                .CountAsync(a => a.BuyerId == "buyer-2" && a.IsSelectedForCheckout);

            Assert.Equal(1, selectedCount);
        }
    }
}
