using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Infrastructure;

public class CartRepository : ICartRepository
{
    private readonly CartDbContext _context;

    public CartRepository(CartDbContext context)
    {
        _context = context;
    }

    public async Task<List<CartItemModel>> GetByBuyerIdAsync(string buyerId)
    {
        return await _context.CartItems
            .Where(c => c.BuyerId == buyerId)
            .OrderBy(c => c.AddedAt)
            .ToListAsync();
    }

    public async Task<CartItemModel?> GetByBuyerAndProductAsync(string buyerId, int productId)
    {
        return await _context.CartItems
            .FirstOrDefaultAsync(c => c.BuyerId == buyerId && c.ProductId == productId);
    }

    public async Task<CartItemModel?> GetByIdAsync(int id)
    {
        return await _context.CartItems.FindAsync(id);
    }

    public async Task<CartModel?> GetByUserIdAsync(string userId)
    {
        return await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }

    public async Task<CartModel> CreateAsync(string userId)
    {
        var cart = new CartModel
        {
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Carts.Add(cart);
        await _context.SaveChangesAsync();

        return cart;
    }

    public async Task<CartItemModel> AddAsync(CartItemModel item)
    {
        _context.CartItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<OrderModel> AddOrderAsync(OrderModel order)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<ReturnRequestModel> AddReturnRequestAsync(ReturnRequestModel request)
    {
        _context.ReturnRequests.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<BuyerReturnRequestsResult> GetReturnRequestsForBuyerAsync(string buyerId, BuyerReturnRequestsQuery query)
    {
        var normalizedPage = query.Page < 1 ? 1 : query.Page;
        var normalizedPageSize = query.PageSize < 1 ? 10 : query.PageSize;
        var statusFilter = (query.Statuses ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.ToLowerInvariant())
            .Distinct()
            .ToList();

        var filteredQuery = _context.ReturnRequests
            .AsNoTracking()
            .Where(r => r.BuyerId == buyerId);

        if (statusFilter.Count > 0)
        {
            filteredQuery = filteredQuery.Where(r => statusFilter.Contains(r.Status));
        }

        if (query.CreatedFrom.HasValue)
        {
            filteredQuery = filteredQuery.Where(r => r.RequestedAt >= query.CreatedFrom.Value);
        }

        if (query.CreatedTo.HasValue)
        {
            filteredQuery = filteredQuery.Where(r => r.RequestedAt <= query.CreatedTo.Value);
        }

        var totalCount = await filteredQuery.CountAsync();
        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var currentPage = normalizedPage > totalPages ? totalPages : normalizedPage;
        var skip = (currentPage - 1) * normalizedPageSize;

        var requests = await filteredQuery
            .Include(r => r.Order)
            .Include(r => r.SellerOrder)
            .Include(r => r.Items)
                .ThenInclude(i => i.OrderItem)
            .OrderByDescending(r => r.RequestedAt)
            .Skip(skip)
            .Take(normalizedPageSize)
            .ToListAsync();

        return new BuyerReturnRequestsResult
        {
            Requests = requests,
            TotalCount = totalCount,
            Page = currentPage,
            PageSize = normalizedPageSize
        };
    }

    public async Task<ReturnRequestModel?> GetReturnRequestAsync(int requestId, string buyerId)
    {
        return await _context.ReturnRequests
            .AsNoTracking()
            .Include(r => r.Items)
                .ThenInclude(i => i.OrderItem)
            .Include(r => r.SellerOrder)
            .Include(r => r.Order)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.BuyerId == buyerId);
    }

    public async Task<ReturnRequestModel?> GetReturnRequestByIdAsync(int requestId)
    {
        return await _context.ReturnRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId);
    }

    public async Task<OrderModel?> GetOrderAsync(int orderId, string buyerId)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.ShippingSelections)
            .Include(o => o.SubOrders)
                .ThenInclude(o => o.Items)
            .Include(o => o.SubOrders)
                .ThenInclude(o => o.ShippingSelection)
            .Include(o => o.SubOrders)
                .ThenInclude(o => o.ShippingHistory)
            .Include(o => o.SubOrders)
                .ThenInclude(o => o.ReturnRequests)
                    .ThenInclude(r => r.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.BuyerId == buyerId);
    }

    public async Task<OrderModel?> GetOrderWithSubOrdersAsync(int orderId)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.ShippingSelections)
            .Include(o => o.SubOrders)
                .ThenInclude(o => o.Items)
            .Include(o => o.SubOrders)
                .ThenInclude(o => o.ShippingSelection)
            .Include(o => o.SubOrders)
                .ThenInclude(o => o.ShippingHistory)
            .Include(o => o.SubOrders)
                .ThenInclude(o => o.ReturnRequests)
                    .ThenInclude(r => r.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);
    }

    public async Task<List<OrderModel>> GetOrdersForBuyerAsync(string buyerId)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.ShippingSelections)
            .Include(o => o.SubOrders)
            .Where(o => o.BuyerId == buyerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<BuyerOrdersResult> GetOrdersForBuyerAsync(string buyerId, BuyerOrdersQuery query)
    {
        var normalizedPage = query.Page < 1 ? 1 : query.Page;
        var normalizedPageSize = query.PageSize < 1 ? 10 : query.PageSize;
        var statusFilter = (query.Statuses ?? Array.Empty<string>())
            .Select(OrderStatusFlow.NormalizeStatus)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.ToLowerInvariant())
            .Distinct()
            .ToList();

        var filteredQuery = _context.Orders
            .AsNoTracking()
            .Where(o => o.BuyerId == buyerId);

        if (statusFilter.Count > 0)
        {
            filteredQuery = filteredQuery.Where(o => statusFilter.Contains(o.Status));
        }

        if (query.CreatedFrom.HasValue)
        {
            filteredQuery = filteredQuery.Where(o => o.CreatedAt >= query.CreatedFrom.Value);
        }

        if (query.CreatedTo.HasValue)
        {
            filteredQuery = filteredQuery.Where(o => o.CreatedAt <= query.CreatedTo.Value);
        }

        if (!string.IsNullOrEmpty(query.SellerId))
        {
            filteredQuery = filteredQuery.Where(o => o.SubOrders.Any(s => s.SellerId == query.SellerId));
        }

        var totalCount = await filteredQuery.CountAsync();
        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var currentPage = normalizedPage > totalPages ? totalPages : normalizedPage;
        var skip = (currentPage - 1) * normalizedPageSize;

        var orders = await filteredQuery
            .Include(o => o.Items)
            .Include(o => o.ShippingSelections)
            .Include(o => o.SubOrders)
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Take(normalizedPageSize)
            .ToListAsync();

        var sellers = await _context.SellerOrders
            .AsNoTracking()
            .Where(s => s.Order!.BuyerId == buyerId)
            .Select(s => new { s.SellerId, s.SellerName })
            .Distinct()
            .ToListAsync();

        return new BuyerOrdersResult
        {
            Orders = orders,
            TotalCount = totalCount,
            Page = currentPage,
            PageSize = normalizedPageSize,
            Sellers = sellers
                .Select(s => new SellerSummary
                {
                    SellerId = s.SellerId,
                    SellerName = s.SellerName
                })
                .ToList()
        };
    }

    public async Task<SellerOrderModel?> GetSellerOrderAsync(int sellerOrderId, string sellerId)
    {
        return await _context.SellerOrders
            .Include(o => o.Items)
            .Include(o => o.ShippingSelection)
            .Include(o => o.Order)
                .ThenInclude(o => o!.SubOrders)
            .Include(o => o.ShippingHistory)
            .Include(o => o.ReturnRequests)
                .ThenInclude(r => r.Items)
            .FirstOrDefaultAsync(o => o.Id == sellerOrderId && o.SellerId == sellerId);
    }

    public async Task<SellerOrderModel?> GetSellerOrderByIdAsync(int sellerOrderId)
    {
        return await _context.SellerOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.ShippingSelection)
            .Include(o => o.Order)
            .Include(o => o.ShippingHistory)
            .Include(o => o.ReturnRequests)
                .ThenInclude(r => r.Items)
            .FirstOrDefaultAsync(o => o.Id == sellerOrderId);
    }

    public async Task<SellerOrderModel?> GetSellerOrderByTrackingAsync(string trackingNumber)
    {
        return await _context.SellerOrders
            .Include(o => o.Items)
            .Include(o => o.ShippingSelection)
            .Include(o => o.Order)
            .Include(o => o.ShippingHistory)
            .Include(o => o.ReturnRequests)
                .ThenInclude(r => r.Items)
            .FirstOrDefaultAsync(o => o.TrackingNumber == trackingNumber);
    }

    public async Task<SellerOrdersResult> GetSellerOrdersAsync(string sellerId, SellerOrdersQuery query)
    {
        var normalizedPage = query.Page < 1 ? 1 : query.Page;
        var normalizedPageSize = query.PageSize < 1 ? 10 : query.PageSize;
        var statusFilter = (query.Statuses ?? Array.Empty<string>())
            .Select(OrderStatusFlow.NormalizeStatus)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.ToLowerInvariant())
            .Distinct()
            .ToList();

        var filteredQuery = _context.SellerOrders
            .AsNoTracking()
            .Where(o => o.SellerId == sellerId);

        if (statusFilter.Count > 0)
        {
            filteredQuery = filteredQuery.Where(o => statusFilter.Contains(o.Status));
        }

        if (query.CreatedFrom.HasValue)
        {
            filteredQuery = filteredQuery.Where(o => o.Order!.CreatedAt >= query.CreatedFrom.Value);
        }

        if (query.CreatedTo.HasValue)
        {
            filteredQuery = filteredQuery.Where(o => o.Order!.CreatedAt <= query.CreatedTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.BuyerId))
        {
            filteredQuery = filteredQuery.Where(o => o.Order!.BuyerId == query.BuyerId);
        }

        if (query.WithoutTracking)
        {
            filteredQuery = filteredQuery.Where(o => string.IsNullOrWhiteSpace(o.TrackingNumber));
        }

        var totalCount = await filteredQuery.CountAsync();
        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var currentPage = normalizedPage > totalPages ? totalPages : normalizedPage;
        var skip = (currentPage - 1) * normalizedPageSize;

        var orders = await filteredQuery
            .Include(o => o.Items)
            .Include(o => o.ShippingSelection)
            .Include(o => o.Order)
            .Include(o => o.ShippingHistory)
            .Include(o => o.ReturnRequests)
                .ThenInclude(r => r.Items)
            .OrderByDescending(o => o.Order!.CreatedAt)
            .Skip(skip)
            .Take(normalizedPageSize)
            .ToListAsync();

        return new SellerOrdersResult
        {
            Orders = orders,
            TotalCount = totalCount,
            Page = currentPage,
            PageSize = normalizedPageSize
        };
    }

    public async Task UpdateAsync(CartModel cart)
    {
        _context.Carts.Update(cart);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CartItemModel item)
    {
        _context.CartItems.Update(item);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAsync(int id)
    {
        var item = await _context.CartItems.FindAsync(id);
        if (item is not null)
        {
            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<ShippingRuleModel>> GetShippingRulesAsync()
    {
        return await _context.ShippingRules
            .Where(sr => sr.IsActive)
            .ToListAsync();
    }

    public async Task<ShippingRuleModel> UpsertShippingRuleAsync(ShippingRuleModel rule)
    {
        var existing = await _context.ShippingRules
            .FirstOrDefaultAsync(r => r.SellerId == rule.SellerId && r.ShippingMethod == rule.ShippingMethod);

        if (existing is null)
        {
            _context.ShippingRules.Add(rule);
        }
        else
        {
            existing.BasePrice = rule.BasePrice;
            existing.DeliveryEstimate = rule.DeliveryEstimate;
            existing.AllowedRegions = rule.AllowedRegions;
            existing.AllowedCountryCodes = rule.AllowedCountryCodes;
            existing.FreeShippingThreshold = rule.FreeShippingThreshold;
            existing.PricePerKg = rule.PricePerKg;
            existing.MaxWeightKg = rule.MaxWeightKg;
            existing.IsActive = rule.IsActive;
        }

        await _context.SaveChangesAsync();
        return existing ?? rule;
    }

    public async Task<List<DeliveryAddressModel>> GetAddressesAsync(string buyerId)
    {
        return await _context.DeliveryAddresses
            .Where(a => a.BuyerId == buyerId)
            .OrderByDescending(a => a.IsSelectedForCheckout)
            .ThenByDescending(a => a.UpdatedAt)
            .ToListAsync();
    }

    public async Task<DeliveryAddressModel?> GetAddressAsync(int addressId)
    {
        return await _context.DeliveryAddresses.FindAsync(addressId);
    }

    public async Task<DeliveryAddressModel> AddOrUpdateAddressAsync(DeliveryAddressModel address)
    {
        if (address.Id == 0)
        {
            _context.DeliveryAddresses.Add(address);
        }
        else
        {
            _context.DeliveryAddresses.Update(address);
        }

        await _context.SaveChangesAsync();
        return address;
    }

    public async Task DeleteAddressAsync(int addressId)
    {
        var existing = await _context.DeliveryAddresses.FindAsync(addressId);
        if (existing is null)
        {
            return;
        }

        _context.DeliveryAddresses.Remove(existing);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsAddressUsedInActiveOrderAsync(string buyerId, DeliveryAddressModel address)
    {
        var activeStatuses = new[]
        {
            OrderStatus.Pending,
            OrderStatus.Confirmed,
            OrderStatus.New,
            OrderStatus.Paid,
            OrderStatus.Preparing,
            OrderStatus.Shipped
        };

        var normalizedRecipient = address.RecipientName.Trim().ToUpperInvariant();
        var normalizedLine1 = address.Line1.Trim().ToUpperInvariant();
        var normalizedLine2 = (address.Line2 ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedCity = address.City.Trim().ToUpperInvariant();
        var normalizedRegion = address.Region.Trim().ToUpperInvariant();
        var normalizedPostal = address.PostalCode.Trim().ToUpperInvariant();
        var normalizedCountry = address.CountryCode.Trim().ToUpperInvariant();
        var normalizedPhone = (address.PhoneNumber ?? string.Empty).Trim().ToUpperInvariant();

        return await _context.Orders.AnyAsync(o =>
            o.BuyerId == buyerId &&
            activeStatuses.Contains(o.Status) &&
            (o.DeliveryRecipientName ?? string.Empty).ToUpper() == normalizedRecipient &&
            (o.DeliveryLine1 ?? string.Empty).ToUpper() == normalizedLine1 &&
            (o.DeliveryLine2 ?? string.Empty).ToUpper() == normalizedLine2 &&
            (o.DeliveryCity ?? string.Empty).ToUpper() == normalizedCity &&
            (o.DeliveryRegion ?? string.Empty).ToUpper() == normalizedRegion &&
            (o.DeliveryPostalCode ?? string.Empty).ToUpper() == normalizedPostal &&
            (o.DeliveryCountryCode ?? string.Empty).ToUpper() == normalizedCountry &&
            (o.DeliveryPhoneNumber ?? string.Empty).ToUpper() == normalizedPhone);
    }

    public async Task SetSelectedAddressAsync(string buyerId, int addressId)
    {
        await ClearSelectedAddressAsync(buyerId);

        var existing = await _context.DeliveryAddresses.FirstOrDefaultAsync(a => a.Id == addressId && a.BuyerId == buyerId);
        if (existing is null)
        {
            return;
        }

        existing.IsSelectedForCheckout = true;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task ClearSelectedAddressAsync(string buyerId)
    {
        var selected = await _context.DeliveryAddresses
            .Where(a => a.BuyerId == buyerId && a.IsSelectedForCheckout)
            .ToListAsync();

        if (selected.Count == 0)
        {
            return;
        }

        foreach (var address in selected)
        {
            address.IsSelectedForCheckout = false;
            address.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<DeliveryAddressModel?> GetSelectedAddressAsync(string buyerId)
    {
        var selected = await _context.DeliveryAddresses
            .Where(a => a.BuyerId == buyerId && a.IsSelectedForCheckout)
            .OrderByDescending(a => a.UpdatedAt)
            .FirstOrDefaultAsync();

        if (selected is not null)
        {
            return selected;
        }

        var fallback = await _context.DeliveryAddresses
            .Where(a => a.BuyerId == buyerId)
            .OrderByDescending(a => a.UpdatedAt)
            .FirstOrDefaultAsync();

        if (fallback is null)
        {
            return null;
        }

        fallback.IsSelectedForCheckout = true;
        fallback.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
        return fallback;
    }

    public async Task<List<ShippingSelectionModel>> GetShippingSelectionsAsync(string buyerId)
    {
        return await _context.ShippingSelections
            .Where(s => s.BuyerId == buyerId)
            .ToListAsync();
    }

    public async Task SetShippingSelectionAsync(string buyerId, string sellerId, string shippingMethod, decimal cost, string? deliveryEstimate = null)
    {
        var existing = await _context.ShippingSelections
            .FirstOrDefaultAsync(s => s.BuyerId == buyerId && s.SellerId == sellerId);

        if (existing is null)
        {
            _context.ShippingSelections.Add(new ShippingSelectionModel
            {
                BuyerId = buyerId,
                SellerId = sellerId,
                ShippingMethod = shippingMethod,
                Cost = cost,
                DeliveryEstimate = deliveryEstimate,
                SelectedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.ShippingMethod = shippingMethod;
            existing.Cost = cost;
            existing.DeliveryEstimate = deliveryEstimate;
            existing.SelectedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task ClearShippingSelectionsAsync(string buyerId)
    {
        var selections = await _context.ShippingSelections
            .Where(s => s.BuyerId == buyerId)
            .ToListAsync();

        if (selections.Count == 0)
        {
            return;
        }

        _context.ShippingSelections.RemoveRange(selections);
        await _context.SaveChangesAsync();
    }

    public async Task<PaymentSelectionModel?> GetPaymentSelectionAsync(string buyerId)
    {
        return await _context.PaymentSelections.FirstOrDefaultAsync(p => p.BuyerId == buyerId);
    }

    public async Task<PaymentSelectionModel?> GetPaymentSelectionByOrderIdAsync(int orderId)
    {
        return await _context.PaymentSelections.FirstOrDefaultAsync(p => p.OrderId == orderId);
    }

    public async Task<bool> HasEscrowEntriesAsync(int orderId)
    {
        return await _context.EscrowLedgerEntries.AnyAsync(e => e.OrderId == orderId);
    }

    public async Task<List<EscrowLedgerEntry>> GetPayoutEligibleEscrowEntriesAsync(DateTimeOffset asOf)
    {
        var eligible = _context.EscrowLedgerEntries
            .Where(e => e.Status == EscrowLedgerStatus.Held && e.PayoutEligibleAt <= asOf)
            .Where(e => !_context.PayoutScheduleItems.Any(p => p.EscrowLedgerEntryId == e.Id));

        return await eligible
            .OrderBy(e => e.PayoutEligibleAt)
            .ToListAsync();
    }

    public async Task<List<EscrowLedgerEntry>> GetCommissionableEscrowEntriesAsync(string sellerId, DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        var invoicedEntries = _context.CommissionInvoiceLines
            .Where(l => !l.IsCorrection)
            .Select(l => l.EscrowLedgerEntryId);

        return await _context.EscrowLedgerEntries
            .AsNoTracking()
            .Where(e => e.SellerId == sellerId)
            .Where(e => e.CreatedAt >= periodStart && e.CreatedAt <= periodEnd)
            .Where(e => !invoicedEntries.Contains(e.Id))
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<EscrowLedgerEntry>> GetCommissionCorrectionsAsync(string sellerId, DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        return await _context.EscrowLedgerEntries
            .AsNoTracking()
            .Where(e => e.SellerId == sellerId)
            .Where(e => e.Status == EscrowLedgerStatus.ReleasedToBuyer)
            .Where(e => e.ReleasedAt.HasValue && e.ReleasedAt.Value >= periodStart && e.ReleasedAt.Value <= periodEnd)
            .Where(e => _context.CommissionInvoiceLines.Any(l => l.EscrowLedgerEntryId == e.Id && !l.IsCorrection))
            .Where(e => !_context.CommissionInvoiceLines.Any(l => l.EscrowLedgerEntryId == e.Id && l.IsCorrection))
            .OrderBy(e => e.ReleasedAt)
            .ToListAsync();
    }

    public async Task<PaymentSelectionModel?> GetPaymentSelectionByReferenceAsync(string providerReference)
    {
        return await _context.PaymentSelections.FirstOrDefaultAsync(p =>
            p.ProviderReference == providerReference);
    }

    public async Task<PaymentSelectionModel> UpsertPaymentSelectionAsync(PaymentSelectionModel selection)
    {
        var existing = await _context.PaymentSelections.FirstOrDefaultAsync(p => p.BuyerId == selection.BuyerId);

        if (existing is null)
        {
            _context.PaymentSelections.Add(selection);
        }
        else
        {
            existing.PaymentMethod = selection.PaymentMethod;
            existing.Status = selection.Status;
            existing.UpdatedAt = selection.UpdatedAt;
            existing.ProviderReference = selection.ProviderReference;
            existing.OrderId = selection.OrderId;
        }

        await _context.SaveChangesAsync();
        return existing ?? selection;
    }

    public async Task AddEscrowEntriesAsync(List<EscrowLedgerEntry> entries)
    {
        _context.EscrowLedgerEntries.AddRange(entries);
        await _context.SaveChangesAsync();
    }

    public async Task AddPayoutScheduleAsync(PayoutSchedule schedule)
    {
        _context.PayoutSchedules.Add(schedule);
        await _context.SaveChangesAsync();
    }

    public async Task<PromoSelectionModel?> GetPromoSelectionAsync(string buyerId)
    {
        return await _context.PromoSelections.FirstOrDefaultAsync(p => p.BuyerId == buyerId);
    }

    public async Task<PromoSelectionModel> UpsertPromoSelectionAsync(PromoSelectionModel selection)
    {
        var normalizedCode = selection.PromoCode.Trim().ToUpperInvariant();
        var existing = await _context.PromoSelections.FirstOrDefaultAsync(p => p.BuyerId == selection.BuyerId);

        if (existing is null)
        {
            selection.PromoCode = normalizedCode;
            _context.PromoSelections.Add(selection);
        }
        else
        {
            existing.PromoCode = normalizedCode;
            existing.AppliedAt = selection.AppliedAt;
        }

        await _context.SaveChangesAsync();
        return existing ?? selection;
    }

    public async Task ClearPromoSelectionAsync(string buyerId)
    {
        var existing = await _context.PromoSelections.FirstOrDefaultAsync(p => p.BuyerId == buyerId);
        if (existing is null)
        {
            return;
        }

        _context.PromoSelections.Remove(existing);
        await _context.SaveChangesAsync();
    }

    public async Task<PromoCodeModel?> GetPromoCodeAsync(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await _context.PromoCodes.FirstOrDefaultAsync(p => p.Code == normalized);
    }

    public async Task ClearPaymentSelectionAsync(string buyerId)
    {
        var existing = await _context.PaymentSelections.FirstOrDefaultAsync(p => p.BuyerId == buyerId);
        if (existing is null)
        {
            return;
        }

        _context.PaymentSelections.Remove(existing);
        await _context.SaveChangesAsync();
    }

    public async Task<List<EscrowLedgerEntry>> GetEscrowEntriesForOrderAsync(int orderId)
    {
        return await _context.EscrowLedgerEntries.Where(e => e.OrderId == orderId).ToListAsync();
    }

    public async Task<EscrowLedgerEntry?> GetEscrowEntryForSellerOrderAsync(int sellerOrderId)
    {
        return await _context.EscrowLedgerEntries.FirstOrDefaultAsync(e => e.SellerOrderId == sellerOrderId);
    }

    public async Task<PayoutSchedule?> GetPayoutScheduleAsync(int scheduleId)
    {
        return await _context.PayoutSchedules.FirstOrDefaultAsync(p => p.Id == scheduleId);
    }

    public async Task<PayoutSchedule?> GetPayoutScheduleWithItemsAsync(int scheduleId)
    {
        return await _context.PayoutSchedules
            .Include(p => p.Items)
                .ThenInclude(i => i.EscrowEntry)
            .FirstOrDefaultAsync(p => p.Id == scheduleId);
    }

    public async Task<PayoutSchedule?> GetPayoutScheduleDetailsAsync(int scheduleId, string sellerId)
    {
        return await _context.PayoutSchedules
            .AsNoTracking()
            .Include(p => p.Items)
                .ThenInclude(i => i.EscrowEntry)
            .FirstOrDefaultAsync(p => p.Id == scheduleId && p.SellerId == sellerId);
    }

    public async Task<List<PayoutSchedule>> GetPayoutSchedulesForSellerAsync(string sellerId, int take = 5)
    {
        var normalizedTake = take < 1 ? 1 : take;
        return await _context.PayoutSchedules
            .AsNoTracking()
            .Where(p => p.SellerId == sellerId)
            .OrderByDescending(p => p.ScheduledAt)
            .Take(normalizedTake)
            .ToListAsync();
    }

    public async Task<PayoutScheduleResult> GetPayoutSchedulesForSellerAsync(string sellerId, PayoutScheduleQuery query)
    {
        var normalizedPage = query.Page < 1 ? 1 : query.Page;
        var normalizedPageSize = query.PageSize < 1 ? 10 : query.PageSize > 50 ? 50 : query.PageSize;

        var statusFilters = (query.Statuses ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .ToList();

        var filtered = _context.PayoutSchedules
            .AsNoTracking()
            .Where(p => p.SellerId == sellerId);

        if (query.ScheduledFrom.HasValue)
        {
            filtered = filtered.Where(p => p.ScheduledFor >= query.ScheduledFrom.Value);
        }

        if (query.ScheduledTo.HasValue)
        {
            filtered = filtered.Where(p => p.ScheduledFor <= query.ScheduledTo.Value);
        }

        if (statusFilters.Count > 0)
        {
            filtered = filtered.Where(p => statusFilters.Contains(p.Status));
        }

        var totalCount = await filtered.CountAsync();
        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var currentPage = normalizedPage > totalPages ? totalPages : normalizedPage;
        var skip = (currentPage - 1) * normalizedPageSize;

        var schedules = await filtered
            .OrderByDescending(p => p.ScheduledAt)
            .Skip(skip)
            .Take(normalizedPageSize)
            .ToListAsync();

        return new PayoutScheduleResult
        {
            Schedules = schedules,
            TotalCount = totalCount,
            Page = currentPage,
            PageSize = normalizedPageSize
        };
    }

    public async Task<CommissionInvoice?> GetCommissionInvoiceAsync(int invoiceId, string sellerId)
    {
        return await _context.CommissionInvoices
            .AsNoTracking()
            .Include(i => i.Lines)
                .ThenInclude(l => l.EscrowLedgerEntry)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.SellerId == sellerId);
    }

    public async Task<CommissionInvoice?> GetCommissionInvoiceForPeriodAsync(string sellerId, DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        return await _context.CommissionInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.SellerId == sellerId && i.PeriodStart == periodStart && i.PeriodEnd == periodEnd);
    }

    public async Task<CommissionInvoiceResult> GetCommissionInvoicesAsync(string sellerId, CommissionInvoiceQuery query)
    {
        var normalizedPage = query.Page < 1 ? 1 : query.Page;
        var normalizedPageSize = query.PageSize < 1 ? 10 : query.PageSize > 50 ? 50 : query.PageSize;
        var statusFilters = (query.Statuses ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        var filtered = _context.CommissionInvoices
            .AsNoTracking()
            .Where(i => i.SellerId == sellerId);

        if (query.PeriodFrom.HasValue)
        {
            filtered = filtered.Where(i => i.PeriodStart >= query.PeriodFrom.Value);
        }

        if (query.PeriodTo.HasValue)
        {
            filtered = filtered.Where(i => i.PeriodEnd <= query.PeriodTo.Value);
        }

        if (!query.IncludeCreditNotes)
        {
            filtered = filtered.Where(i => !i.IsCreditNote);
        }

        if (statusFilters.Count > 0)
        {
            filtered = filtered.Where(i => statusFilters.Contains(i.Status));
        }

        var totalCount = await filtered.CountAsync();
        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var currentPage = normalizedPage > totalPages ? totalPages : normalizedPage;
        var skip = (currentPage - 1) * normalizedPageSize;

        var invoices = await filtered
            .OrderByDescending(i => i.PeriodStart)
            .ThenByDescending(i => i.IssuedAt)
            .Skip(skip)
            .Take(normalizedPageSize)
            .ToListAsync();

        return new CommissionInvoiceResult
        {
            Invoices = invoices,
            TotalCount = totalCount,
            Page = currentPage,
            PageSize = normalizedPageSize
        };
    }

    public async Task AddCommissionInvoiceAsync(CommissionInvoice invoice)
    {
        _context.CommissionInvoices.Add(invoice);
        await _context.SaveChangesAsync();
    }

    public async Task ClearCartItemsAsync(string buyerId)
    {
        var items = await _context.CartItems
            .Where(c => c.BuyerId == buyerId)
            .ToListAsync();

        if (items.Count == 0)
        {
            return;
        }

        _context.CartItems.RemoveRange(items);
        await _context.SaveChangesAsync();
    }

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
