using DuongVanDung.WebApp.Models.Report;
using DuongVanDung.WebApp.Helpers;
using DuongVanDung.WebApp.Services.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DuongVanDung.WebApp.Controllers;
[Authorize]
public class ReportController : Controller
{
    private readonly IQualityReportService _svc;
    public ReportController(IQualityReportService svc) => _svc = svc;
    public async Task<IActionResult> Quality(
        string? dateFrom = null, string? dateTo = null, string? branch = null)
    {
        var filter = new QualityReportFilter
        {
            DateFrom = DateTime.TryParse(dateFrom, out var df) ? df : VietnamTime.Now.AddDays(-30).Date,
            DateTo   = DateTime.TryParse(dateTo,   out var dt) ? dt : VietnamTime.Today,
            Branch   = branch ?? "",
        };
        var vm = await _svc.GetReportAsync(filter);
        return View(vm);
    }
}
