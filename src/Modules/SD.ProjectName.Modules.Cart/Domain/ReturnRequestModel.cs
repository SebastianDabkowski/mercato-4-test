namespace SD.ProjectName.Modules.Cart.Domain;

public class ReturnRequestModel
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int SellerOrderId { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string RequestType { get; set; } = ReturnRequestType.Return;
    public string Status { get; set; } = ReturnRequestStatus.Requested;
    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int BuyerUnreadCount { get; set; }
    public int SellerUnreadCount { get; set; }
    public OrderModel? Order { get; set; }
    public SellerOrderModel? SellerOrder { get; set; }
    public List<ReturnRequestItemModel> Items { get; set; } = new();
    public List<ReturnRequestMessageModel> Messages { get; set; } = new();
}

public class ReturnRequestItemModel
{
    public int Id { get; set; }
    public int ReturnRequestId { get; set; }
    public int OrderItemId { get; set; }
    public int Quantity { get; set; }
    public ReturnRequestModel? ReturnRequest { get; set; }
    public OrderItemModel? OrderItem { get; set; }
}

public class ReturnRequestMessageModel
{
    public int Id { get; set; }
    public int ReturnRequestId { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public ReturnRequestModel? ReturnRequest { get; set; }
}

public static class ReturnRequestStatus
{
    public const string Requested = "requested";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string InfoRequested = "info_requested";
    public const string PartialProposed = "partial_proposed";
    public const string Completed = "completed";
}

public static class ReturnRequestType
{
    public const string Return = "return";
    public const string Complaint = "complaint";
}

public static class ReturnRequestMessageSender
{
    public const string Buyer = "buyer";
    public const string Seller = "seller";
    public const string Admin = "admin";
}
