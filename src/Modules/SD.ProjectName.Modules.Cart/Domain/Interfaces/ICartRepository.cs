using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.Modules.Cart.Domain.Interfaces
{
    public interface ICartRepository
    {
        Task<List<CartItemModel>> GetByBuyerIdAsync(string buyerId);
        Task<CartItemModel?> GetByBuyerAndProductAsync(string buyerId, int productId);
        Task<CartItemModel?> GetByIdAsync(int id);
        Task<CartModel?> GetByUserIdAsync(string userId);
        Task<List<ShippingRuleModel>> GetShippingRulesAsync();
        Task<List<DeliveryAddressModel>> GetAddressesAsync(string buyerId);
        Task<DeliveryAddressModel?> GetAddressAsync(int addressId);
        Task<DeliveryAddressModel> AddOrUpdateAddressAsync(DeliveryAddressModel address);
        Task DeleteAddressAsync(int addressId);
        Task<bool> IsAddressUsedInActiveOrderAsync(string buyerId, DeliveryAddressModel address);
        Task SetSelectedAddressAsync(string buyerId, int addressId);
        Task ClearSelectedAddressAsync(string buyerId);
        Task<DeliveryAddressModel?> GetSelectedAddressAsync(string buyerId);
        Task<List<ShippingSelectionModel>> GetShippingSelectionsAsync(string buyerId);
        Task SetShippingSelectionAsync(string buyerId, string sellerId, string shippingMethod, decimal cost, string? deliveryEstimate = null);
        Task ClearShippingSelectionsAsync(string buyerId);
        Task<PaymentSelectionModel?> GetPaymentSelectionAsync(string buyerId);
        Task<PaymentSelectionModel?> GetPaymentSelectionByOrderIdAsync(int orderId);
        Task<PaymentSelectionModel?> GetPaymentSelectionByReferenceAsync(string providerReference);
        Task<PaymentSelectionModel> UpsertPaymentSelectionAsync(PaymentSelectionModel selection);
        Task ClearPaymentSelectionAsync(string buyerId);
        Task<PromoSelectionModel?> GetPromoSelectionAsync(string buyerId);
        Task<PromoSelectionModel> UpsertPromoSelectionAsync(PromoSelectionModel selection);
        Task ClearPromoSelectionAsync(string buyerId);
        Task<PromoCodeModel?> GetPromoCodeAsync(string code);
        Task<bool> HasEscrowEntriesAsync(int orderId);
        Task<List<EscrowLedgerEntry>> GetPayoutEligibleEscrowEntriesAsync(DateTimeOffset asOf);
        Task<List<EscrowLedgerEntry>> GetCommissionableEscrowEntriesAsync(string sellerId, DateTimeOffset periodStart, DateTimeOffset periodEnd);
        Task<List<EscrowLedgerEntry>> GetCommissionCorrectionsAsync(string sellerId, DateTimeOffset periodStart, DateTimeOffset periodEnd);
        Task AddEscrowEntriesAsync(List<EscrowLedgerEntry> entries);
        Task AddPayoutScheduleAsync(PayoutSchedule schedule);
        Task<PayoutSchedule?> GetPayoutScheduleAsync(int scheduleId);
        Task<PayoutSchedule?> GetPayoutScheduleWithItemsAsync(int scheduleId);
        Task<PayoutSchedule?> GetPayoutScheduleDetailsAsync(int scheduleId, string sellerId);
        Task<List<PayoutSchedule>> GetPayoutSchedulesForSellerAsync(string sellerId, int take = 5);
        Task<PayoutScheduleResult> GetPayoutSchedulesForSellerAsync(string sellerId, PayoutScheduleQuery query);
        Task<List<EscrowLedgerEntry>> GetEscrowEntriesForOrderAsync(int orderId);
        Task<EscrowLedgerEntry?> GetEscrowEntryForSellerOrderAsync(int sellerOrderId);
        Task<CommissionInvoice?> GetCommissionInvoiceAsync(int invoiceId, string sellerId);
        Task<CommissionInvoice?> GetCommissionInvoiceForPeriodAsync(string sellerId, DateTimeOffset periodStart, DateTimeOffset periodEnd);
        Task<CommissionInvoiceResult> GetCommissionInvoicesAsync(string sellerId, CommissionInvoiceQuery query);
        Task AddCommissionInvoiceAsync(CommissionInvoice invoice);
      
        Task<CartItemModel> AddAsync(CartItemModel item);
        Task<CartModel> CreateAsync(string userId);
        Task<OrderModel> AddOrderAsync(OrderModel order);
        Task<ReturnRequestModel> AddReturnRequestAsync(ReturnRequestModel request);
        Task<OrderModel?> GetOrderAsync(int orderId, string buyerId);
        Task<OrderModel?> GetOrderWithSubOrdersAsync(int orderId);
        Task<List<OrderModel>> GetOrdersForBuyerAsync(string buyerId);
        Task<BuyerOrdersResult> GetOrdersForBuyerAsync(string buyerId, BuyerOrdersQuery query);
        Task<SellerOrderModel?> GetSellerOrderAsync(int sellerOrderId, string sellerId);
        Task<SellerOrderModel?> GetSellerOrderByIdAsync(int sellerOrderId);
        Task<SellerOrdersResult> GetSellerOrdersAsync(string sellerId, SellerOrdersQuery query);
        Task SaveChangesAsync();
        
        Task UpdateAsync(CartModel cart);
        Task UpdateAsync(CartItemModel item);
        Task ClearCartItemsAsync(string buyerId);
      
        Task RemoveAsync(int id);
    }
}
