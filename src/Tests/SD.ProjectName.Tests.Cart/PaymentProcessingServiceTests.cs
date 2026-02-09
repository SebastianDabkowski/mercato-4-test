using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.Modules.Cart.Infrastructure;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.Tests.Cart;

public class PaymentProcessingServiceTests
{
    [Fact]
    public async Task HandleCallbackAsync_ReturnsAlreadyProcessed_OnRepeatedSuccess()
    {
        var buyerId = "buyer-pp";
        var selection = new PaymentSelectionModel
        {
            BuyerId = buyerId,
            PaymentMethod = "Card",
            Status = PaymentStatus.Pending,
            ProviderReference = "ref-1",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var cartItems = new List<CartItemModel>
        {
            new() { ProductId = 1, ProductName = "Test", ProductSku = "SKU", Quantity = 1, UnitPrice = 20m, SellerId = "seller-1", SellerName = "Seller" }
        };

        var escrowEntries = new List<EscrowLedgerEntry>();
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetPaymentSelectionByReferenceAsync("ref-1")).ReturnsAsync(selection);
        repo.Setup(r => r.GetByBuyerIdAsync(buyerId)).ReturnsAsync(cartItems);
        repo.Setup(r => r.GetPaymentSelectionAsync(buyerId)).ReturnsAsync(selection);
        repo.Setup(r => r.GetSelectedAddressAsync(buyerId))
            .ReturnsAsync(new DeliveryAddressModel { BuyerId = buyerId, RecipientName = "Buyer" });
        repo.Setup(r => r.GetShippingSelectionsAsync(buyerId))
            .ReturnsAsync(new List<ShippingSelectionModel> { new() { BuyerId = buyerId, SellerId = "seller-1", ShippingMethod = "Standard", Cost = 5m } });
        repo.Setup(r => r.GetShippingRulesAsync()).ReturnsAsync(new List<ShippingRuleModel>
        {
            new() { SellerId = "seller-1", ShippingMethod = "Standard", BasePrice = 5m, IsActive = true }
        });
        repo.Setup(r => r.AddOrderAsync(It.IsAny<OrderModel>()))
            .ReturnsAsync((OrderModel order) =>
            {
                order.Id = 101;
                var sellerOrderId = 1;
                foreach (var sub in order.SubOrders)
                {
                    sub.Id = sellerOrderId++;
                }
                return order;
            });
        repo.Setup(r => r.GetPromoSelectionAsync(It.IsAny<string>())).ReturnsAsync((PromoSelectionModel?)null);
        repo.Setup(r => r.ClearCartItemsAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.ClearShippingSelectionsAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.ClearPromoSelectionAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        repo.Setup(r => r.HasEscrowEntriesAsync(It.IsAny<int>())).ReturnsAsync(false);
        repo.Setup(r => r.AddEscrowEntriesAsync(It.IsAny<List<EscrowLedgerEntry>>())).Returns<List<EscrowLedgerEntry>>(entries =>
        {
            escrowEntries.AddRange(entries);
            return Task.CompletedTask;
        });
        repo.Setup(r => r.GetEscrowEntriesForOrderAsync(It.IsAny<int>())).ReturnsAsync((int orderId) =>
            escrowEntries.Where(e => e.OrderId == orderId).ToList());
        repo.Setup(r => r.GetEscrowEntryForSellerOrderAsync(It.IsAny<int>())).ReturnsAsync((int sellerOrderId) =>
            escrowEntries.FirstOrDefault(e => e.SellerOrderId == sellerOrderId));

        var snapshotService = new Mock<IProductSnapshotService>();
        snapshotService.Setup(s => s.GetSnapshotAsync(1)).ReturnsAsync(new ProductSnapshot(1, 20m, 2));

        var validationService = new CheckoutValidationService(new GetCartItems(repo.Object), repo.Object, snapshotService.Object);
        var promoService = new PromoService(repo.Object, new GetCartItems(repo.Object), new CartCalculationService(), TimeProvider.System);
        var placeOrder = new PlaceOrder(validationService, repo.Object, new CartCalculationService(), promoService, TimeProvider.System);
        var commissionService = new CommissionService(Options.Create(new CommissionOptions()), TimeProvider.System);
        var escrowService = new EscrowService(repo.Object, TimeProvider.System, commissionService, Options.Create(new EscrowOptions()));
        var orderStatusService = new OrderStatusService(repo.Object, escrowService, commissionService, TimeProvider.System);
        var service = new PaymentProcessingService(
            repo.Object,
            placeOrder,
            TimeProvider.System,
            escrowService,
            commissionService,
            orderStatusService,
            Mock.Of<ILogger<PaymentProcessingService>>());

        var first = await service.HandleCallbackAsync("ref-1", "success");

        Assert.True(first.Success);
        Assert.False(first.AlreadyProcessed);
        Assert.Equal(101, selection.OrderId);
        Assert.Equal(PaymentStatus.Paid, selection.Status);
        Assert.Single(escrowEntries);
        Assert.Equal(EscrowLedgerStatus.Held, escrowEntries[0].Status);
        Assert.Equal(25m, escrowEntries[0].HeldAmount);

        var second = await service.HandleCallbackAsync("ref-1", "success");

        Assert.True(second.Success);
        Assert.True(second.AlreadyProcessed);
        Assert.Equal(101, second.OrderId);
    }

    [Fact]
    public async Task HandleCallbackAsync_CreatesEscrowPerSeller()
    {
        var buyerId = "buyer-escrow";
        var selection = new PaymentSelectionModel
        {
            BuyerId = buyerId,
            PaymentMethod = "Card",
            Status = PaymentStatus.Pending,
            ProviderReference = "ref-escrow",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var cartItems = new List<CartItemModel>
        {
            new() { ProductId = 1, ProductName = "Test A", ProductSku = "SKU-A", Quantity = 1, UnitPrice = 10m, SellerId = "seller-1", SellerName = "Seller One" },
            new() { ProductId = 2, ProductName = "Test B", ProductSku = "SKU-B", Quantity = 2, UnitPrice = 5m, SellerId = "seller-2", SellerName = "Seller Two" }
        };

        var escrowEntries = new List<EscrowLedgerEntry>();
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetPaymentSelectionByReferenceAsync("ref-escrow")).ReturnsAsync(selection);
        repo.Setup(r => r.GetByBuyerIdAsync(buyerId)).ReturnsAsync(cartItems);
        repo.Setup(r => r.GetPaymentSelectionAsync(buyerId)).ReturnsAsync(selection);
        repo.Setup(r => r.GetSelectedAddressAsync(buyerId))
            .ReturnsAsync(new DeliveryAddressModel { BuyerId = buyerId, RecipientName = "Buyer" });
        repo.Setup(r => r.GetShippingSelectionsAsync(buyerId))
            .ReturnsAsync(new List<ShippingSelectionModel>
            {
                new() { BuyerId = buyerId, SellerId = "seller-1", ShippingMethod = "Standard", Cost = 3m },
                new() { BuyerId = buyerId, SellerId = "seller-2", ShippingMethod = "Express", Cost = 2m }
            });
        repo.Setup(r => r.GetShippingRulesAsync()).ReturnsAsync(new List<ShippingRuleModel>
        {
            new() { SellerId = "seller-1", ShippingMethod = "Standard", BasePrice = 3m, IsActive = true },
            new() { SellerId = "seller-2", ShippingMethod = "Express", BasePrice = 2m, IsActive = true }
        });
        repo.Setup(r => r.AddOrderAsync(It.IsAny<OrderModel>()))
            .ReturnsAsync((OrderModel order) =>
            {
                order.Id = 202;
                var sellerOrderId = 10;
                foreach (var sub in order.SubOrders)
                {
                    sub.Id = sellerOrderId++;
                }
                return order;
            });
        repo.Setup(r => r.GetPromoSelectionAsync(It.IsAny<string>())).ReturnsAsync((PromoSelectionModel?)null);
        repo.Setup(r => r.ClearCartItemsAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.ClearShippingSelectionsAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.ClearPromoSelectionAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        repo.Setup(r => r.HasEscrowEntriesAsync(It.IsAny<int>())).ReturnsAsync(false);
        repo.Setup(r => r.AddEscrowEntriesAsync(It.IsAny<List<EscrowLedgerEntry>>())).Returns<List<EscrowLedgerEntry>>(entries =>
        {
            escrowEntries.AddRange(entries);
            return Task.CompletedTask;
        });
        repo.Setup(r => r.GetEscrowEntriesForOrderAsync(It.IsAny<int>())).ReturnsAsync((int orderId) =>
            escrowEntries.Where(e => e.OrderId == orderId).ToList());
        repo.Setup(r => r.GetEscrowEntryForSellerOrderAsync(It.IsAny<int>())).ReturnsAsync((int sellerOrderId) =>
            escrowEntries.FirstOrDefault(e => e.SellerOrderId == sellerOrderId));

        var snapshotService = new Mock<IProductSnapshotService>();
        snapshotService.Setup(s => s.GetSnapshotAsync(It.IsAny<int>())).ReturnsAsync((int id) => new ProductSnapshot(id, id == 1 ? 10m : 5m, 10));

        var validationService = new CheckoutValidationService(new GetCartItems(repo.Object), repo.Object, snapshotService.Object);
        var promoService = new PromoService(repo.Object, new GetCartItems(repo.Object), new CartCalculationService(), TimeProvider.System);
        var placeOrder = new PlaceOrder(validationService, repo.Object, new CartCalculationService(), promoService, TimeProvider.System);
        var commissionService = new CommissionService(Options.Create(new CommissionOptions()), TimeProvider.System);
        var escrowService = new EscrowService(repo.Object, TimeProvider.System, commissionService, Options.Create(new EscrowOptions()));
        var orderStatusService = new OrderStatusService(repo.Object, escrowService, commissionService, TimeProvider.System);
        var service = new PaymentProcessingService(
            repo.Object,
            placeOrder,
            TimeProvider.System,
            escrowService,
            commissionService,
            orderStatusService,
            Mock.Of<ILogger<PaymentProcessingService>>());

        var result = await service.HandleCallbackAsync("ref-escrow", "success");

        Assert.True(result.Success);
        Assert.False(result.AlreadyProcessed);
        Assert.Equal(2, escrowEntries.Count);
        Assert.All(escrowEntries, e => Assert.Equal(EscrowLedgerStatus.Held, e.Status));
        Assert.Contains(escrowEntries, e => e.HeldAmount == 13m && e.SellerOrderId == 10);
        Assert.Contains(escrowEntries, e => e.HeldAmount == 12m && e.SellerOrderId == 11);
        Assert.All(escrowEntries, e => Assert.Equal(TimeSpan.FromDays(7), e.PayoutEligibleAt - e.CreatedAt));
    }

    [Fact]
    public async Task HandleCallbackAsync_RecordsPendingStatusAndCreatesOrder()
    {
        var buyerId = "buyer-pending";
        var selection = new PaymentSelectionModel
        {
            BuyerId = buyerId,
            PaymentMethod = "Card",
            Status = PaymentStatus.Pending,
            ProviderReference = "ref-pending",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var cartItems = new List<CartItemModel>
        {
            new() { ProductId = 1, ProductName = "Pending Product", ProductSku = "SKU-P", Quantity = 1, UnitPrice = 15m, SellerId = "seller-1", SellerName = "Seller Pending" }
        };

        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetPaymentSelectionByReferenceAsync("ref-pending")).ReturnsAsync(selection);
        repo.Setup(r => r.GetByBuyerIdAsync(buyerId)).ReturnsAsync(cartItems);
        repo.Setup(r => r.GetPaymentSelectionAsync(buyerId)).ReturnsAsync(selection);
        repo.Setup(r => r.GetSelectedAddressAsync(buyerId))
            .ReturnsAsync(new DeliveryAddressModel { BuyerId = buyerId, RecipientName = "Buyer Pending" });
        repo.Setup(r => r.GetShippingSelectionsAsync(buyerId))
            .ReturnsAsync(new List<ShippingSelectionModel> { new() { BuyerId = buyerId, SellerId = "seller-1", ShippingMethod = "Standard", Cost = 4m } });
        repo.Setup(r => r.GetShippingRulesAsync()).ReturnsAsync(new List<ShippingRuleModel>
        {
            new() { SellerId = "seller-1", ShippingMethod = "Standard", BasePrice = 4m, IsActive = true }
        });
        repo.Setup(r => r.AddOrderAsync(It.IsAny<OrderModel>()))
            .ReturnsAsync((OrderModel order) =>
            {
                order.Id = 303;
                var sellerOrderId = 30;
                foreach (var sub in order.SubOrders)
                {
                    sub.Id = sellerOrderId++;
                }
                return order;
            });
        repo.Setup(r => r.GetPromoSelectionAsync(It.IsAny<string>())).ReturnsAsync((PromoSelectionModel?)null);
        repo.Setup(r => r.ClearCartItemsAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.ClearShippingSelectionsAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.ClearPromoSelectionAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var snapshotService = new Mock<IProductSnapshotService>();
        snapshotService.Setup(s => s.GetSnapshotAsync(1)).ReturnsAsync(new ProductSnapshot(1, 15m, 2));

        var validationService = new CheckoutValidationService(new GetCartItems(repo.Object), repo.Object, snapshotService.Object);
        var promoService = new PromoService(repo.Object, new GetCartItems(repo.Object), new CartCalculationService(), TimeProvider.System);
        var placeOrder = new PlaceOrder(validationService, repo.Object, new CartCalculationService(), promoService, TimeProvider.System);
        var commissionService = new CommissionService(Options.Create(new CommissionOptions()), TimeProvider.System);
        var escrowService = new EscrowService(repo.Object, TimeProvider.System, commissionService, Options.Create(new EscrowOptions()));
        var orderStatusService = new OrderStatusService(repo.Object, escrowService, commissionService, TimeProvider.System);
        var service = new PaymentProcessingService(
            repo.Object,
            placeOrder,
            TimeProvider.System,
            escrowService,
            commissionService,
            orderStatusService,
            Mock.Of<ILogger<PaymentProcessingService>>());

        var result = await service.HandleCallbackAsync("ref-pending", "pending");

        Assert.Equal(PaymentStatus.Pending, result.Status);
        Assert.Equal(PaymentStatus.Pending, selection.Status);
        Assert.Equal(303, selection.OrderId);
        Assert.False(result.AlreadyProcessed);
    }

    [Fact]
    public async Task HandleCallbackAsync_MarksSelectionRefunded()
    {
        var selection = new PaymentSelectionModel
        {
            BuyerId = "buyer-refund",
            PaymentMethod = "Card",
            Status = PaymentStatus.Paid,
            ProviderReference = "ref-refund",
            UpdatedAt = DateTimeOffset.UtcNow,
            OrderId = 777
        };

        var order = new OrderModel
        {
            Id = 777,
            BuyerId = selection.BuyerId,
            PaymentMethod = "Card",
            ItemsSubtotal = 10m,
            ShippingTotal = 0m,
            TotalAmount = 10m,
            Status = OrderStatus.Paid,
            SubOrders = new List<SellerOrderModel>
            {
                new()
                {
                    Id = 900,
                    SellerId = "seller-1",
                    SellerName = "Seller",
                    ItemsSubtotal = 10m,
                    ShippingTotal = 0m,
                    TotalAmount = 10m,
                    Status = OrderStatus.Preparing,
                    Items = new List<OrderItemModel>
                    {
                        new()
                        {
                            ProductId = 1,
                            ProductSku = "SKU",
                            ProductName = "Test",
                            SellerId = "seller-1",
                            SellerName = "Seller",
                            UnitPrice = 10m,
                            Quantity = 1,
                            Status = OrderStatus.Preparing
                        }
                    }
                }
            }
        };

        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetPaymentSelectionByReferenceAsync("ref-refund")).ReturnsAsync(selection);
        repo.Setup(r => r.GetOrderWithSubOrdersAsync(777)).ReturnsAsync(order);
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var validationService = new Mock<ICheckoutValidationService>();
        var promoService = new PromoService(repo.Object, new GetCartItems(repo.Object), new CartCalculationService(), TimeProvider.System);
        var placeOrder = new PlaceOrder(validationService.Object, repo.Object, new CartCalculationService(), promoService, TimeProvider.System);
        var commissionService = new CommissionService(Options.Create(new CommissionOptions()), TimeProvider.System);
        var escrowService = new EscrowService(repo.Object, TimeProvider.System, commissionService, Options.Create(new EscrowOptions()));
        var orderStatusService = new OrderStatusService(repo.Object, escrowService, commissionService, TimeProvider.System);
        var service = new PaymentProcessingService(
            repo.Object,
            placeOrder,
            TimeProvider.System,
            escrowService,
            commissionService,
            orderStatusService,
            Mock.Of<ILogger<PaymentProcessingService>>());

        var result = await service.HandleCallbackAsync("ref-refund", "refunded");

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Refunded, result.Status);
        Assert.False(result.AlreadyProcessed);
        Assert.Equal(PaymentStatus.Refunded, selection.Status);
    }

    [Fact]
    public async Task HandleCallbackAsync_RefundsOrderAndReleasesEscrow()
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(nameof(HandleCallbackAsync_RefundsOrderAndReleasesEscrow))
            .Options;
        var context = new CartDbContext(options);
        var repo = new CartRepository(context);

        var order = new OrderModel
        {
            BuyerId = "buyer-full-refund",
            PaymentMethod = "Card",
            DeliveryRecipientName = "Buyer",
            DeliveryLine1 = "123 Street",
            DeliveryCity = "Town",
            DeliveryRegion = "Region",
            DeliveryPostalCode = "12345",
            DeliveryCountryCode = "US",
            ItemsSubtotal = 20m,
            ShippingTotal = 5m,
            TotalAmount = 25m,
            Status = OrderStatus.Paid,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            SubOrders = new List<SellerOrderModel>
            {
                new()
                {
                    SellerId = "seller-1",
                    SellerName = "Seller",
                    ItemsSubtotal = 20m,
                    ShippingTotal = 5m,
                    TotalAmount = 25m,
                    Status = OrderStatus.Preparing,
                    DeliveredAt = DateTimeOffset.UtcNow.AddDays(-1),
                    Items = new List<OrderItemModel>
                    {
                        new()
                        {
                            ProductId = 1,
                            ProductSku = "SKU-1",
                            ProductName = "Test",
                            SellerId = "seller-1",
                            SellerName = "Seller",
                            UnitPrice = 20m,
                            Quantity = 1,
                            Status = OrderStatus.Preparing
                        }
                    }
                }
            }
        };

        await repo.AddOrderAsync(order);

        var escrowEntry = new EscrowLedgerEntry
        {
            OrderId = order.Id,
            SellerOrderId = order.SubOrders[0].Id,
            BuyerId = order.BuyerId,
            SellerId = order.SubOrders[0].SellerId,
            HeldAmount = 25m,
            CommissionAmount = 0m,
            SellerPayoutAmount = 25m,
            Status = EscrowLedgerStatus.Held,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            PayoutEligibleAt = DateTimeOffset.UtcNow.AddDays(6)
        };
        await repo.AddEscrowEntriesAsync(new List<EscrowLedgerEntry> { escrowEntry });

        var selection = new PaymentSelectionModel
        {
            BuyerId = order.BuyerId,
            PaymentMethod = "Card",
            ProviderReference = "ref-provider-refund",
            Status = PaymentStatus.Paid,
            OrderId = order.Id,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.PaymentSelections.Add(selection);
        await context.SaveChangesAsync();

        var validationService = new Mock<ICheckoutValidationService>();
        var promoService = new PromoService(repo, new GetCartItems(repo), new CartCalculationService(), TimeProvider.System);
        var placeOrder = new PlaceOrder(validationService.Object, repo, new CartCalculationService(), promoService, TimeProvider.System);
        var commissionService = new CommissionService(Options.Create(new CommissionOptions()), TimeProvider.System);
        var escrowService = new EscrowService(repo, TimeProvider.System, commissionService, Options.Create(new EscrowOptions()));
        var orderStatusService = new OrderStatusService(repo, escrowService, commissionService, TimeProvider.System);

        var service = new PaymentProcessingService(
            repo,
            placeOrder,
            TimeProvider.System,
            escrowService,
            commissionService,
            orderStatusService,
            Mock.Of<ILogger<PaymentProcessingService>>());

        var result = await service.HandleCallbackAsync("ref-provider-refund", "refunded");

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Refunded, result.Status);
        var updatedOrder = await repo.GetOrderWithSubOrdersAsync(order.Id);
        Assert.NotNull(updatedOrder);
        Assert.Equal(OrderStatus.Refunded, updatedOrder!.Status);
        Assert.Equal(updatedOrder.TotalAmount, updatedOrder.RefundedAmount);
        Assert.Equal(OrderStatus.Refunded, updatedOrder.SubOrders[0].Status);
        Assert.Equal(updatedOrder.SubOrders[0].TotalAmount, updatedOrder.SubOrders[0].RefundedAmount);
        var ledger = await repo.GetEscrowEntryForSellerOrderAsync(updatedOrder.SubOrders[0].Id);
        Assert.NotNull(ledger);
        Assert.Equal(EscrowLedgerStatus.ReleasedToBuyer, ledger!.Status);
    }
}
