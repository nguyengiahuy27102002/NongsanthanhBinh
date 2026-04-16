using System.Security.Claims;
using DuongVanDung.WebApp.Helpers;
using DuongVanDung.WebApp.Models.Auth;
using DuongVanDung.WebApp.Services.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DuongVanDung.WebApp.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _svc;
    public DashboardController(IDashboardService svc) => _svc = svc;

    public async Task<IActionResult> Index()
    {
        var displayName = User.FindFirstValue("display_name") ?? User.Identity?.Name ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();

        var data = await _svc.GetDashboardDataAsync();
        var F = (decimal v) => v.ToString("#,0.#");
        var FL = (long v) => v.ToString("#,0");

        var allCoffeeKg  = data.Warehouses.Sum(w => w.CoffeeTotalKg);
        var allCoffeeQc  = data.Warehouses.Sum(w => w.CoffeeTotalQc);
        var allCoffeeExp = data.Warehouses.Sum(w => w.CoffeeExportKg);
        var allPepperKg  = data.Warehouses.Sum(w => w.PepperTotalKg);
        var allPepperQc  = data.Warehouses.Sum(w => w.PepperTotalQc);
        var allPepperExp = data.Warehouses.Sum(w => w.PepperExportKg);

        var coffeeStock = allCoffeeKg - allCoffeeExp;
        var pepperStock = allPepperKg - allPepperExp;
        var coffeeTodayKg = data.Warehouses.Sum(w => w.CoffeeTodayKg);
        var pepperTodayKg = data.Warehouses.Sum(w => w.PepperTodayKg);
        var coffeeTodayTien = data.Warehouses.Sum(w => w.CoffeeTodayTien);
        var pepperTodayTien = data.Warehouses.Sum(w => w.PepperTodayTien);

        var whColors = new[] { "#2563eb", "#dc2626", "#d97706" };
        var dailyLabels = Enumerable.Range(0, 7).Select(i => VietnamTime.Today.AddDays(-6 + i).ToString("dd/MM")).ToArray();

        var coffeeSeries = new List<DashboardChartSeries>();
        var pepperSeries = new List<DashboardChartSeries>();

        for (var i = 0; i < data.Warehouses.Count && i < data.DailyByWarehouse.Length; i++)
        {
            var wh = data.Warehouses[i];
            var daily = data.DailyByWarehouse[i];
            var color = i < whColors.Length ? whColors[i] : "#64748b";

            coffeeSeries.Add(new DashboardChartSeries
            {
                Name = wh.Name, Color = color,
                Values = daily.Select(d => d.CoffeeKg).ToArray()
            });
            pepperSeries.Add(new DashboardChartSeries
            {
                Name = wh.Name, Color = color,
                Values = daily.Select(d => d.PepperKg).ToArray()
            });
        }

        var model = new DashboardViewModel
        {
            Username = User.Identity?.Name ?? string.Empty,
            DisplayName = displayName,
            Roles = roles,
            CommoditySummaries = new[]
            {
                new DashboardCommoditySummary
                {
                    Name = "Cà phê", Theme = "coffee",
                    Metrics = new[]
                    {
                        new DashboardSummaryCard { Icon = "inventory", Label = "Tồn kho (nhập - xuất)", Value = $"{F(coffeeStock)} kg" },
                        new DashboardSummaryCard { Icon = "standard", Label = "Tổng quy chuẩn", Value = $"{F(allCoffeeQc)} kg" },
                        new DashboardSummaryCard { Icon = "inbound", Label = "Tổng nhập", Value = $"{F(allCoffeeKg)} kg" },
                        new DashboardSummaryCard { Icon = "outbound", Label = "Tổng xuất", Value = $"{F(allCoffeeExp)} kg" },
                        new DashboardSummaryCard { Icon = "retail", Label = "Mua hôm nay", Value = $"{F(coffeeTodayKg)} kg — {FL(coffeeTodayTien)} đ" },
                        new DashboardSummaryCard { Icon = "price", Label = "Giá sản phẩm", Value = $"{data.CoffePrice:#,0} đ/kg" },
                    }
                },
                new DashboardCommoditySummary
                {
                    Name = "Tiêu", Theme = "pepper",
                    Metrics = new[]
                    {
                        new DashboardSummaryCard { Icon = "inventory", Label = "Tồn kho (nhập - xuất)", Value = $"{F(pepperStock)} kg" },
                        new DashboardSummaryCard { Icon = "standard", Label = "Tổng quy chuẩn", Value = $"{F(allPepperQc)} kg" },
                        new DashboardSummaryCard { Icon = "inbound", Label = "Tổng nhập", Value = $"{F(allPepperKg)} kg" },
                        new DashboardSummaryCard { Icon = "outbound", Label = "Tổng xuất", Value = $"{F(allPepperExp)} kg" },
                        new DashboardSummaryCard { Icon = "retail", Label = "Mua hôm nay", Value = $"{F(pepperTodayKg)} kg — {FL(pepperTodayTien)} đ" },
                        new DashboardSummaryCard { Icon = "price", Label = "Giá sản phẩm", Value = $"{data.PepperPrice:#,0} đ/kg" },
                    }
                }
            },
            MenuItems = new[]
            {
                new DashboardMenuItem { Icon = "home", Title = "Trang chủ", Controller = "Dashboard", Action = "Index" },
                new DashboardMenuItem { Icon = "warehouse", Title = "Kho hàng", Controller = "Warehouse", Action = "Index" },
                new DashboardMenuItem { Icon = "inbound", Title = "Nhập hàng", Controller = "Dashboard", Action = "Feature", FeatureKey = "receiving" },
                new DashboardMenuItem { Icon = "outbound", Title = "Xuất hàng", Controller = "Dashboard", Action = "Feature", FeatureKey = "shipping" },
                new DashboardMenuItem { Icon = "database", Title = "Dữ liệu", Controller = "Data", Action = "Index" },
                new DashboardMenuItem { Icon = "report", Title = "Báo cáo", Controller = "Product", Action = "TonKho" },
                new DashboardMenuItem { Icon = "standard", Title = "Chất lượng", Controller = "Report", Action = "Quality" },
                new DashboardMenuItem { Icon = "cashier", Title = "Thủ quỹ", Controller = "Dashboard", Action = "Feature", FeatureKey = "cashier" },
                new DashboardMenuItem { Icon = "debt", Title = "Công nợ", Controller = "Debt", Action = "Index" },
                new DashboardMenuItem { Icon = "customers", Title = "Khách hàng", Controller = "Customer", Action = "Index" },
                new DashboardMenuItem { Icon = "transfer", Title = "Chuyển khoản", Controller = "Dashboard", Action = "Feature", FeatureKey = "transfers" },
                new DashboardMenuItem { Icon = "account", Title = "Tài khoản", Controller = "Admin", Action = "Accounts" },
                new DashboardMenuItem { Icon = "price", Title = "Giá sản phẩm", Controller = "Dashboard", Action = "Feature", FeatureKey = "prices" }
            },
            Warehouses = data.Warehouses.Select((w, idx) => new WarehouseDashboardCard
            {
                Name = $"Kho {w.Name}",
                Note = w.Name switch
                {
                    "Dương Văn Dũng" => "Kho chính — đầy đủ nhập lẻ, sỉ, xuất hàng và tài chính.",
                    "Thông Đào"      => "Kho vệ tinh — tiêu sỉ và cà phê theo phát sinh.",
                    "Ymoal"          => "Kho hỗ trợ — thu mua tiêu và cà phê khu vực vệ tinh.",
                    _                => ""
                },
                Products = new[]
                {
                    new WarehouseProductCard
                    {
                        Name = "Cà phê", Theme = "coffee",
                        Metrics = new[]
                        {
                            new WarehouseMetric { Icon = "inventory", Label = "Tồn kho", Value = F(w.CoffeeTotalKg - w.CoffeeExportKg), Unit = "kg" },
                            new WarehouseMetric { Icon = "standard", Label = "Quy chuẩn", Value = F(w.CoffeeTotalQc), Unit = "kg" },
                            new WarehouseMetric { Icon = "inbound", Label = "Nhập hàng", Value = F(w.CoffeeTotalKg), Unit = "kg" },
                            new WarehouseMetric { Icon = "outbound", Label = "Xuất hàng", Value = F(w.CoffeeExportKg), Unit = "kg" },
                            new WarehouseMetric { Icon = "retail", Label = "Hôm nay", Value = F(w.CoffeeTodayKg), Unit = "kg" },
                        }
                    },
                    new WarehouseProductCard
                    {
                        Name = "Tiêu", Theme = "pepper",
                        Metrics = new[]
                        {
                            new WarehouseMetric { Icon = "inventory", Label = "Tồn kho", Value = F(w.PepperTotalKg - w.PepperExportKg), Unit = "kg" },
                            new WarehouseMetric { Icon = "standard", Label = "Quy chuẩn", Value = F(w.PepperTotalQc), Unit = "kg" },
                            new WarehouseMetric { Icon = "inbound", Label = "Nhập hàng", Value = F(w.PepperTotalKg), Unit = "kg" },
                            new WarehouseMetric { Icon = "outbound", Label = "Xuất hàng", Value = F(w.PepperExportKg), Unit = "kg" },
                            new WarehouseMetric { Icon = "retail", Label = "Hôm nay", Value = F(w.PepperTodayKg), Unit = "kg" },
                        }
                    }
                },
                FinanceCards = new[]
                {
                    new DashboardFinanceCard
                    {
                        Icon = "cashier", Title = "Chi phí", Accent = "expense",
                        Metrics = new[]
                        {
                            new WarehouseMetric { Icon = "money", Label = "Tổng chi", Value = FL(w.CoffeeTotalTien + w.PepperTotalTien), Unit = "đồng" },
                            new WarehouseMetric { Icon = "money", Label = "CK cà phê", Value = FL(w.CoffeeCK), Unit = "đồng" },
                            new WarehouseMetric { Icon = "money", Label = "TM cà phê", Value = FL(w.CoffeeTM), Unit = "đồng" },
                            new WarehouseMetric { Icon = "money", Label = "CK tiêu", Value = FL(w.PepperCK), Unit = "đồng" },
                            new WarehouseMetric { Icon = "money", Label = "TM tiêu", Value = FL(w.PepperTM), Unit = "đồng" },
                        }
                    }
                }
            }).ToArray(),
            FinanceCards = new[]
            {
                new DashboardFinanceCard
                {
                    Icon = "report", Title = "Tổng hợp hôm nay", Accent = "profit",
                    Metrics = new[]
                    {
                        new WarehouseMetric { Icon = "coffee", Label = "Mua cà phê hôm nay", Value = FL(coffeeTodayTien), Unit = "đồng" },
                        new WarehouseMetric { Icon = "pepper", Label = "Mua tiêu hôm nay", Value = FL(pepperTodayTien), Unit = "đồng" },
                        new WarehouseMetric { Icon = "money", Label = "Tổng chi hôm nay", Value = FL(coffeeTodayTien + pepperTodayTien), Unit = "đồng" },
                    }
                }
            },
            ProductPrices = new[]
            {
                new ProductPriceCard { Name = "Cà phê", Price = data.CoffePrice.ToString("#,0") },
                new ProductPriceCard { Name = "Tiêu", Price = data.PepperPrice.ToString("#,0") }
            },
            ChartSection = new DashboardChartSection
            {
                CoffeeShare = data.Warehouses.Select(w => new DashboardChartPoint { Label = w.Name, Value = (double)(w.CoffeeTotalKg - w.CoffeeExportKg) }).ToArray(),
                PepperShare = data.Warehouses.Select(w => new DashboardChartPoint { Label = w.Name, Value = (double)(w.PepperTotalKg - w.PepperExportKg) }).ToArray(),
                DailyLabels = dailyLabels,
                CoffeeDailySeries = coffeeSeries,
                PepperDailySeries = pepperSeries,
            }
        };

        return View(model);
    }

    public IActionResult Feature(string? featureKey = null)
    {
        var featureTitle = (featureKey ?? "").Trim().ToLowerInvariant() switch
        {
            "warehouse" => "Kho hàng", "receiving" => "Nhập hàng", "shipping" => "Xuất hàng",
            "cashier" => "Thủ quỹ", "transfers" => "Chuyển khoản", "prices" => "Giá sản phẩm",
            _ => "Tính năng đang phát triển"
        };
        return View("FeaturePlaceholder", $"Khu vực {featureTitle} đang được chuẩn bị.");
    }
}
