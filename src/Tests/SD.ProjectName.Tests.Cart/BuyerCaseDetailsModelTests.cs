using Microsoft.AspNetCore.Mvc;
using Moq;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Pages.Buyer.Cases;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.Tests.Cart;

public class BuyerCaseDetailsModelTests
{
    [Fact]
    public async Task OnGetAsync_ReturnsForbid_WhenCaseBelongsToAnotherBuyer()
    {
        var cartRepository = new Mock<ICartRepository>();
        cartRepository.Setup(r => r.GetReturnRequestAsync(5, "buyer-123"))
            .ReturnsAsync((ReturnRequestModel?)null);
        cartRepository.Setup(r => r.GetReturnRequestByIdAsync(5))
            .ReturnsAsync(new ReturnRequestModel { Id = 5, BuyerId = "other-buyer" });

        var identity = new Mock<ICartIdentityService>();
        identity.Setup(s => s.GetOrCreateBuyerId()).Returns("buyer-123");

        var model = new DetailsModel(identity.Object, cartRepository.Object);

        var result = await model.OnGetAsync(5);

        Assert.IsType<ForbidResult>(result);
    }
}
