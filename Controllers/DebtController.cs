using DuongVanDung.WebApp.Models.Debt;
using DuongVanDung.WebApp.Services.Debt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DuongVanDung.WebApp.Controllers;

[Authorize]
public class DebtController : Controller
{
    private readonly IDebtService _svc;
    public DebtController(IDebtService svc) => _svc = svc;

    public async Task<IActionResult> Index(
        string? product = null, string? branch = null,
        string? search = null, string? sort = null, int page = 1)
    {
        var filter = new DebtFilter
        {
            Product = product ?? "",
            Branch  = branch ?? "",
            Search  = search ?? "",
            Sort    = sort ?? "debt",
            Page    = Math.Max(1, page),
        };
        var vm = await _svc.GetDebtOverviewAsync(filter);
        return View(vm);
    }
}
