using System;
using System.Collections.Generic;

namespace SD.ProjectName.Modules.Cart.Domain;

public static class ReturnRequestWorkflow
{
    public const string SellerActionAcceptReturn = "accept";
    public const string SellerActionProposePartial = "propose_partial";
    public const string SellerActionRequestInfo = "request_info";
    public const string SellerActionReject = "reject";

    public static readonly IReadOnlyDictionary<string, string> SellerActionToStatus = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [SellerActionAcceptReturn] = ReturnRequestStatus.Approved,
        [SellerActionProposePartial] = ReturnRequestStatus.PartialProposed,
        [SellerActionRequestInfo] = ReturnRequestStatus.InfoRequested,
        [SellerActionReject] = ReturnRequestStatus.Rejected
    };

    public static bool IsFinalStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return string.Equals(status, ReturnRequestStatus.Rejected, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, ReturnRequestStatus.Completed, StringComparison.OrdinalIgnoreCase);
    }
}
