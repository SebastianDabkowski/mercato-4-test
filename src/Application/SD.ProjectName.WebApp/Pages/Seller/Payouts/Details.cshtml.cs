using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller.Payouts;

[Authorize(Roles = IdentityRoles.Seller)]
public class DetailsModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICartRepository _cartRepository;

    public DetailsModel(UserManager<ApplicationUser> userManager, ICartRepository cartRepository)
    {
        _userManager = userManager;
        _cartRepository = cartRepository;
    }

    public PayoutSchedule? Schedule { get; private set; }

    public List<PayoutBreakdownItem> Breakdown { get; private set; } = new();

    public class PayoutBreakdownItem
    {
        public int SellerOrderId { get; init; }
        public int OrderId { get; init; }
        public decimal HeldAmount { get; init; }
        public decimal CommissionAmount { get; init; }
        public decimal PayoutAmount { get; init; }
        public DateTimeOffset PayoutEligibleAt { get; init; }
    }

    public async Task<IActionResult> OnGetAsync(int scheduleId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        if (user.AccountType != AccountType.Seller)
        {
            return Forbid();
        }

        var schedule = await _cartRepository.GetPayoutScheduleDetailsAsync(scheduleId, user.Id);
        if (schedule is null)
        {
            return NotFound();
        }

        Schedule = schedule;
        Breakdown = schedule.Items
            .Where(i => i.EscrowEntry is not null)
            .Select(i => new PayoutBreakdownItem
            {
                SellerOrderId = i.EscrowEntry!.SellerOrderId,
                OrderId = i.EscrowEntry!.OrderId,
                HeldAmount = i.EscrowEntry!.HeldAmount,
                CommissionAmount = i.EscrowEntry!.CommissionAmount,
                PayoutAmount = i.Amount,
                PayoutEligibleAt = i.EscrowEntry!.PayoutEligibleAt
            })
            .OrderByDescending(i => i.PayoutEligibleAt)
            .ToList();

        return Page();
    }

    public string StatusLabel(string status) => status switch
    {
        PayoutStatus.Paid => "Paid",
        PayoutStatus.Processing => "Processing",
        PayoutStatus.Failed => "Failed",
        _ => "Scheduled"
    };
}
