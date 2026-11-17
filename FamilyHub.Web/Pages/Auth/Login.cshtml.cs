using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FamilyHub.Web.Pages.Auth;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    public void OnGet()
    {
    }
}
