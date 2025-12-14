using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Buyer
{
    [Authorize(Roles = IdentityRoles.Buyer)]
    public class DashboardModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
