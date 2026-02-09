using Microsoft.Extensions.Options;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.Tests.Cart;

public class CommissionServiceTests
{
    [Fact]
    public void EnsureCommissionCalculated_UsesSellerOverride()
    {
        var options = Options.Create(new CommissionOptions
        {
            DefaultRate = 0.01m,
            SellerOverrides = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                { "seller-1", 0.05m }
            },
            CategoryOverrides = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                { "books", 0.02m }
            }
        });

        var service = new CommissionService(options, TimeProvider.System);
        var order = new OrderModel
        {
            SubOrders = new List<SellerOrderModel>
            {
                new()
                {
                    SellerId = "seller-1",
                    ItemsSubtotal = 100m,
                    ShippingTotal = 10m,
                    DiscountTotal = 0m,
                    TotalAmount = 110m,
                    Items = new List<OrderItemModel>
                    {
                        new() { UnitPrice = 50m, Quantity = 1, Category = "books" },
                        new() { UnitPrice = 50m, Quantity = 1, Category = "other" }
                    }
                }
            }
        };

        service.EnsureCommissionCalculated(order);

        var sub = order.SubOrders[0];
        Assert.Equal(0.05m, sub.CommissionRateApplied);
        Assert.Equal(5.50m, sub.CommissionAmount);
        Assert.True(sub.CommissionCalculatedAt.HasValue);
        Assert.Equal(5.50m, order.CommissionTotal);
    }

    [Fact]
    public void RecalculateAfterRefund_UsesStoredRate()
    {
        var service = new CommissionService(Options.Create(new CommissionOptions { DefaultRate = 0.02m }), TimeProvider.System);
        var order = new OrderModel();
        var subOrder = new SellerOrderModel
        {
            Order = order,
            SellerId = "seller-2",
            ItemsSubtotal = 200m,
            ShippingTotal = 0m,
            DiscountTotal = 0m,
            TotalAmount = 200m,
            Items = new List<OrderItemModel>
            {
                new() { UnitPrice = 200m, Quantity = 1, Category = "electronics" }
            }
        };
        order.SubOrders.Add(subOrder);

        service.EnsureCommissionCalculated(order);
        subOrder.RefundedAmount = 50m;
        service.RecalculateAfterRefund(subOrder);

        Assert.Equal(0.02m, subOrder.CommissionRateApplied);
        Assert.Equal(3.00m, subOrder.CommissionAmount);
        Assert.Equal(subOrder.CommissionAmount, order.CommissionTotal);
    }
}
