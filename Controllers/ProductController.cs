using DuongVanDung.WebApp.Services.Product;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DuongVanDung.WebApp.Controllers;

[Authorize]
public class ProductController : Controller
{
    private readonly IProductService _svc;
    public ProductController(IProductService svc) => _svc = svc;

    public async Task<IActionResult> TonKho()
    {
        var vm = await _svc.GetTonKhoAsync();
        return View(vm);
    }

    public async Task<IActionResult> StockMovement(int days = 30)
    {
        var vm = await _svc.GetStockMovementAsync(days);
        return View(vm);
    }

    public async Task<IActionResult> Quality()
    {
        var vm = await _svc.GetQualityAsync();
        return View(vm);
    }
}
