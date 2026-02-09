using System;
using System.Collections.Generic;
using System.Reflection;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.WebApp.Pages.Seller.Orders;

namespace SD.ProjectName.Tests.Cart;

public class SellerOrdersExportTests
{
    [Fact]
    public void BuildCsv_IncludesLogisticsFieldsAndItems()
    {
        var orders = new List<SellerOrderModel>
        {
            new()
            {
                Id = 2,
                OrderId = 1,
                SellerId = "seller-1",
                ItemsSubtotal = 25m,
                ShippingTotal = 5m,
                TotalAmount = 30m,
                Status = OrderStatus.Preparing,
                TrackingNumber = "TRK-1",
                TrackingCarrier = "UPS",
                TrackingUrl = "http://tracking.example/1",
                ShippingSelection = new OrderShippingSelectionModel
                {
                    ShippingMethod = "Courier",
                    Cost = 5m
                },
                Order = new OrderModel
                {
                    BuyerId = "buyer-1",
                    DeliveryRecipientName = "Buyer Name",
                    DeliveryLine1 = "123 Street",
                    DeliveryLine2 = "Apt 4B",
                    DeliveryCity = "Town",
                    DeliveryRegion = "Region",
                    DeliveryPostalCode = "12345",
                    DeliveryCountryCode = "US",
                    DeliveryPhoneNumber = "+123456789",
                    CreatedAt = new DateTimeOffset(2024, 02, 01, 10, 00, 00, TimeSpan.Zero)
                },
                Items = new List<OrderItemModel>
                {
                    new()
                    {
                        ProductName = "Widget, Large",
                        ProductSku = "SKU-1",
                        Quantity = 2,
                        UnitPrice = 12.5m
                    }
                }
            }
        };

        var csv = InvokeBuildCsv(orders);
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("OrderId,SubOrderId,SellerId,OrderCreatedAt,Status,BuyerId,BuyerName", lines[0]);
        Assert.Contains("\"buyer-1\"", lines[1]);
        Assert.Contains("\"Buyer Name\"", lines[1]);
        Assert.Contains("\"123 Street\"", lines[1]);
        Assert.Contains("\"Apt 4B\"", lines[1]);
        Assert.Contains("\"Town\"", lines[1]);
        Assert.Contains("\"+123456789\"", lines[1]);
        Assert.Contains("\"Courier\"", lines[1]);
        Assert.Contains("5.00", lines[1]);
        Assert.Contains("30.00", lines[1]);
        Assert.Contains("\"TRK-1\"", lines[1]);
        Assert.Contains("\"UPS\"", lines[1]);
        Assert.Contains("\"http://tracking.example/1\"", lines[1]);
        Assert.Contains("\"Widget, Large (SKU: SKU-1) x2\"", lines[1]);
    }

    private static string InvokeBuildCsv(IEnumerable<SellerOrderModel> orders)
    {
        var method = typeof(IndexModel).GetMethod("BuildCsv", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, new object[] { orders })!;
    }
}
