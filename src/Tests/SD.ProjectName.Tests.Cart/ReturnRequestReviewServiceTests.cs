using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Tests.Cart;

public class ReturnRequestReviewServiceTests
{
    [Fact]
    public async Task ApplySellerDecisionAsync_UpdatesStatus_WhenPendingAndOwned()
    {
        var now = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var repo = new Mock<ICartRepository>();
        var existing = new ReturnRequestModel
        {
            Id = 10,
            SellerOrderId = 5,
            OrderId = 2,
            BuyerId = "buyer-1",
            Status = ReturnRequestStatus.Requested
        };
        repo.Setup(r => r.GetReturnRequestForSellerAsync(10, "seller-1"))
            .ReturnsAsync(existing);
        repo.Setup(r => r.UpdateReturnRequestStatusAsync(10, "seller-1", ReturnRequestStatus.Approved, now))
            .ReturnsAsync(new ReturnRequestModel
            {
                Id = existing.Id,
                SellerOrderId = existing.SellerOrderId,
                OrderId = existing.OrderId,
                BuyerId = existing.BuyerId,
                Status = ReturnRequestStatus.Approved,
                UpdatedAt = now
            });

        var service = new ReturnRequestReviewService(repo.Object, new FixedTimeProvider(now));

        var result = await service.ApplySellerDecisionAsync(10, "seller-1", ReturnRequestWorkflow.SellerActionAcceptReturn);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Request);
        Assert.Equal(ReturnRequestStatus.Approved, result.Request!.Status);
        repo.Verify(r => r.UpdateReturnRequestStatusAsync(10, "seller-1", ReturnRequestStatus.Approved, now), Times.Once);
    }

    [Fact]
    public async Task ApplySellerDecisionAsync_ReturnsForbidden_WhenCaseOwnedByAnotherSeller()
    {
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetReturnRequestForSellerAsync(10, "seller-1"))
            .ReturnsAsync((ReturnRequestModel?)null);
        repo.Setup(r => r.GetReturnRequestByIdAsync(10))
            .ReturnsAsync(new ReturnRequestModel { Id = 10, SellerOrderId = 7, BuyerId = "buyer-9" });

        var service = new ReturnRequestReviewService(repo.Object, TimeProvider.System);

        var result = await service.ApplySellerDecisionAsync(10, "seller-1", ReturnRequestWorkflow.SellerActionReject);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsForbidden);
        repo.Verify(r => r.UpdateReturnRequestStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task ApplySellerDecisionAsync_Fails_WhenNotAwaitingReview()
    {
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetReturnRequestForSellerAsync(10, "seller-1"))
            .ReturnsAsync(new ReturnRequestModel { Id = 10, Status = ReturnRequestStatus.Approved, BuyerId = "buyer-1" });

        var service = new ReturnRequestReviewService(repo.Object, TimeProvider.System);

        var result = await service.ApplySellerDecisionAsync(10, "seller-1", ReturnRequestWorkflow.SellerActionReject);

        Assert.False(result.IsSuccess);
        Assert.Equal("This case is no longer awaiting seller review.", result.Error);
        repo.Verify(r => r.UpdateReturnRequestStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
