using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Tests.Cart;

public class ReturnRequestReviewServiceTests
{
    [Fact]
    public async Task ApplySellerResolutionAsync_UpdatesRefundAndResolution_WhenFullRefundSelected()
    {
        var repo = new Mock<ICartRepository>();
        var existing = new ReturnRequestModel
        {
            Id = 10,
            SellerOrderId = 5,
            OrderId = 2,
            BuyerId = "buyer-1",
            Status = ReturnRequestStatus.Requested,
            SellerOrder = new SellerOrderModel
            {
                Id = 5,
                SellerId = "seller-1",
                TotalAmount = 120m,
                RefundedAmount = 20m
            }
        };
        repo.Setup(r => r.GetReturnRequestForSellerAsync(10, "seller-1"))
            .ReturnsAsync(existing);
        repo.Setup(r => r.UpdateReturnRequestStatusAsync(
                10,
                "seller-1",
                ReturnRequestStatus.Completed,
                It.IsAny<DateTimeOffset>(),
                ReturnRequestResolution.FullRefund,
                100m,
                ReturnRequestRefundStatus.Completed,
                "ref-123",
                "Approved full refund"))
            .ReturnsAsync(new ReturnRequestModel
            {
                Id = existing.Id,
                SellerOrderId = existing.SellerOrderId,
                OrderId = existing.OrderId,
                BuyerId = existing.BuyerId,
                Status = ReturnRequestStatus.Completed,
                UpdatedAt = DateTimeOffset.UtcNow,
                Resolution = ReturnRequestResolution.FullRefund,
                RefundAmount = 100m,
                RefundReference = "ref-123",
                RefundStatus = ReturnRequestRefundStatus.Completed
            });

        var refundCalled = false;
        var service = new ReturnRequestReviewService(
            repo.Object,
            orderStatusService: null!,
            TimeProvider.System,
            (sellerOrderId, sellerId, amount, reason) =>
            {
                refundCalled = true;
                Assert.Equal(5, sellerOrderId);
                Assert.Equal("seller-1", sellerId);
                Assert.Equal(100m, amount);
                return Task.FromResult(OrderStatusResult.SuccessResult(OrderStatus.Refunded, OrderStatus.Refunded));
            });

        var result = await service.ApplySellerResolutionAsync(
            10,
            "seller-1",
            new SellerResolutionCommand(
                ReturnRequestResolution.FullRefund,
                null,
                false,
                "ref-123",
                "Approved full refund"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Request);
        Assert.Equal(ReturnRequestStatus.Completed, result.Request!.Status);
        Assert.True(refundCalled);
        repo.Verify(r => r.UpdateReturnRequestStatusAsync(
            10,
            "seller-1",
            ReturnRequestStatus.Completed,
            It.IsAny<DateTimeOffset>(),
            ReturnRequestResolution.FullRefund,
            100m,
            ReturnRequestRefundStatus.Completed,
            "ref-123",
            "Approved full refund"), Times.Once);
    }

    [Fact]
    public async Task ApplySellerResolutionAsync_ReturnsForbidden_WhenCaseOwnedByAnotherSeller()
    {
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetReturnRequestForSellerAsync(10, "seller-1"))
            .ReturnsAsync((ReturnRequestModel?)null);
        repo.Setup(r => r.GetReturnRequestByIdAsync(10))
            .ReturnsAsync(new ReturnRequestModel { Id = 10, SellerOrderId = 7, BuyerId = "buyer-9" });

        var service = new ReturnRequestReviewService(repo.Object, orderStatusService: null!, TimeProvider.System, (_, _, _, _) => Task.FromResult(OrderStatusResult.SuccessResult(null, null)));

        var result = await service.ApplySellerResolutionAsync(
            10,
            "seller-1",
            new SellerResolutionCommand(ReturnRequestResolution.NoRefund, null, false, null, "Not applicable"));

        Assert.False(result.IsSuccess);
        Assert.True(result.IsForbidden);
        repo.Verify(r => r.UpdateReturnRequestStatusAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string?>(),
            It.IsAny<decimal?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ApplySellerResolutionAsync_RequiresReason_ForNoRefund()
    {
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetReturnRequestForSellerAsync(10, "seller-1"))
            .ReturnsAsync(new ReturnRequestModel
            {
                Id = 10,
                Status = ReturnRequestStatus.Requested,
                BuyerId = "buyer-1",
                SellerOrder = new SellerOrderModel
                {
                    Id = 5,
                    SellerId = "seller-1",
                    TotalAmount = 50m
                }
            });

        var service = new ReturnRequestReviewService(repo.Object, orderStatusService: null!, TimeProvider.System, (_, _, _, _) => Task.FromResult(OrderStatusResult.SuccessResult(null, null)));

        var result = await service.ApplySellerResolutionAsync(
            10,
            "seller-1",
            new SellerResolutionCommand(ReturnRequestResolution.NoRefund, null, false, null, string.Empty));

        Assert.False(result.IsSuccess);
        Assert.Equal("Provide a reason for choosing no refund.", result.Error);
        repo.Verify(r => r.UpdateReturnRequestStatusAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string?>(),
            It.IsAny<decimal?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>()), Times.Never);
    }

}
