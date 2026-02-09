using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Pages.Buyer.Orders;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.Tests.Cart;

public class BuyerOrderDetailsModelTests
{
    [Fact]
    public async Task OnGetAsync_ReturnsForbid_WhenOrderBelongsToAnotherBuyer()
    {
        var cartRepository = new Mock<ICartRepository>();
        cartRepository.Setup(r => r.GetOrderAsync(1, "buyer-123")).ReturnsAsync((OrderModel?)null);
        cartRepository.Setup(r => r.GetOrderWithSubOrdersAsync(1))
            .ReturnsAsync(new OrderModel { Id = 1, BuyerId = "other-buyer" });

        var identity = new Mock<ICartIdentityService>();
        identity.Setup(s => s.GetOrCreateBuyerId()).Returns("buyer-123");

        var commissionService = new CommissionService(Options.Create(new CommissionOptions()), TimeProvider.System);
        var escrowService = new EscrowService(
            cartRepository.Object,
            TimeProvider.System,
            commissionService,
            Options.Create(new EscrowOptions()));
        var model = new DetailsModel(
            identity.Object,
            cartRepository.Object,
            new OrderStatusService(cartRepository.Object, escrowService, commissionService),
            new ReturnRequestService(cartRepository.Object, TimeProvider.System));

        var result = await model.OnGetAsync(1);

        Assert.IsType<ForbidResult>(result);
    }
}
