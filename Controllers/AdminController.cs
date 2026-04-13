using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DuongVanDung.WebApp.Controllers;

[Authorize]
public class AdminController : Controller
{
    public IActionResult Accounts()
    {
        return View();
    }
}
