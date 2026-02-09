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
            .Include(o => o.ReturnRequests)
                .ThenInclude(r => r.Items)
            .FirstOrDefaultAsync(o => o.Id == sellerOrderId);
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
        return await _context.DeliveryAddresses
            .Where(a => a.BuyerId == buyerId && a.IsSelectedForCheckout)
            .OrderByDescending(a => a.UpdatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ShippingSelectionModel>> GetShippingSelectionsAsync(string buyerId)
    {
        return await _context.ShippingSelections
            .Where(s => s.BuyerId == buyerId)
            .ToListAsync();
    }

    public async Task SetShippingSelectionAsync(string buyerId, string sellerId, string shippingMethod, decimal cost)
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
                SelectedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.ShippingMethod = shippingMethod;
            existing.Cost = cost;
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

    public async Task<bool> HasEscrowEntriesAsync(int orderId)
    {
        return await _context.EscrowLedgerEntries.AnyAsync(e => e.OrderId == orderId);
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
