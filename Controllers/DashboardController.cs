using System.Security.Claims;
using DuongVanDung.WebApp.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DuongVanDung.WebApp.Controllers;

[Authorize]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        var displayName = User.FindFirstValue("display_name") ?? User.Identity?.Name ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();

        DashboardFinanceCard CreateExpenseCard(
            string total,
            string cashCoffee,
            string transferCoffee,
            string cashPepper,
            string transferPepper,
            string other) =>
            new()
            {
                Icon = "cashier",
                Title = "Chi phí",
                Accent = "expense",
                Metrics = new[]
                {
                    new WarehouseMetric { Icon = "money", Label = "Tổng chi", Value = total, Unit = "đồng" },
                    new WarehouseMetric { Icon = "money", Label = "TM cà phê", Value = cashCoffee, Unit = "đồng" },
                    new WarehouseMetric { Icon = "money", Label = "CK cà phê", Value = transferCoffee, Unit = "đồng" },
                    new WarehouseMetric { Icon = "money", Label = "TM tiêu", Value = cashPepper, Unit = "đồng" },
                    new WarehouseMetric { Icon = "money", Label = "CK tiêu", Value = transferPepper, Unit = "đồng" },
                    new WarehouseMetric { Icon = "money", Label = "Khác", Value = other, Unit = "đồng" }
                }
            };

        DashboardFinanceCard CreateDebtCard(string total, string coffee, string pepper) =>
            new()
            {
                Icon = "debt",
                Title = "Nợ",
                Accent = "debt",
                Metrics = new[]
                {
                    new WarehouseMetric { Icon = "money", Label = "Tổng nợ", Value = total, Unit = "đồng" },
                    new WarehouseMetric { Icon = "coffee", Label = "Cà phê", Value = coffee, Unit = "đồng" },
                    new WarehouseMetric { Icon = "pepper", Label = "Tiêu", Value = pepper, Unit = "đồng" }
                }
            };

        var model = new DashboardViewModel
        {
            Username = User.Identity?.Name ?? string.Empty,
            DisplayName = displayName,
            Roles = roles,
            CommoditySummaries = new[]
            {
                new DashboardCommoditySummary
                {
                    Name = "Cà phê",
                    Theme = "coffee",
                    Metrics = new[]
                    {
                        new DashboardSummaryCard { Icon = "inventory", Label = "Tổng tồn kho", Value = "23,819.6 kg", Note = string.Empty },
                        new DashboardSummaryCard { Icon = "standard", Label = "Tổng quy chuẩn", Value = "9,884.6 kg", Note = string.Empty },
                        new DashboardSummaryCard { Icon = "inbound", Label = "Tổng nhập", Value = "2,652.1 kg", Note = string.Empty },
                        new DashboardSummaryCard { Icon = "outbound", Label = "Tổng xuất", Value = "180 kg", Note = string.Empty },
                        new DashboardSummaryCard { Icon = "retail", Label = "Mua hàng hôm nay", Value = "1,732.1 kg", Note = string.Empty },
                        new DashboardSummaryCard { Icon = "price", Label = "Giá sản phẩm", Value = "87,000 đ/kg", Note = string.Empty }
                    }
                },
                new DashboardCommoditySummary
                {
                    Name = "Tiêu",
                    Theme = "pepper",
                    Metrics = new[]
                    {
                        new DashboardSummaryCard { Icon = "inventory", Label = "Tổng tồn kho", Value = "4,775,479.5 kg", Note = string.Empty },
                        new DashboardSummaryCard { Icon = "standard", Label = "Tổng quy chuẩn", Value = "4,429,384.9 kg", Note = string.Empty },
                        new DashboardSummaryCard { Icon = "inbound", Label = "Tổng nhập", Value = "6,889.7 kg", Note = string.Empty },
                        new DashboardSummaryCard { Icon = "outbound", Label = "Tổng xuất", Value = "350 kg", Note = string.Empty },
                        new DashboardSummaryCard { Icon = "retail", Label = "Mua hàng hôm nay", Value = "4,579.7 kg", Note = string.Empty },
                        new DashboardSummaryCard { Icon = "price", Label = "Giá sản phẩm", Value = "140,000 đ/kg", Note = string.Empty }
                    }
                }
            },
            MenuItems = new[]
            {
                new DashboardMenuItem { Icon = "home", Title = "Trang chủ", Description = "Tổng quan nhanh hoạt động các kho", Controller = "Dashboard", Action = "Index" },
                new DashboardMenuItem { Icon = "warehouse", Title = "Kho hàng", Description = "Tồn kho, quy chuẩn và cân đối tại từng điểm", Controller = "Dashboard", Action = "Feature", FeatureKey = "warehouse" },
                new DashboardMenuItem { Icon = "inbound", Title = "Nhập hàng", Description = "Phiếu nhập cà phê và tiêu theo từng kho", Controller = "Dashboard", Action = "Feature", FeatureKey = "receiving" },
                new DashboardMenuItem { Icon = "outbound", Title = "Xuất hàng", Description = "Theo dõi các lượt xuất kho và giao hàng", Controller = "Dashboard", Action = "Feature", FeatureKey = "shipping" },
                new DashboardMenuItem { Icon = "database", Title = "Dữ liệu", Description = "Quản lý dữ liệu phát sinh và đồng bộ nghiệp vụ", Controller = "Dashboard", Action = "Feature", FeatureKey = "data" },
                new DashboardMenuItem { Icon = "report", Title = "Báo cáo", Description = "Tổng hợp số liệu bán hàng và tồn kho", Controller = "Dashboard", Action = "Feature", FeatureKey = "reports" },
                new DashboardMenuItem { Icon = "cashier", Title = "Thủ quỹ", Description = "Theo dõi thu chi và dòng tiền tại cửa hàng", Controller = "Dashboard", Action = "Feature", FeatureKey = "cashier" },
                new DashboardMenuItem { Icon = "customers", Title = "Khách hàng", Description = "Danh sách khách, công nợ và lịch sử giao dịch", Controller = "Dashboard", Action = "Feature", FeatureKey = "customers" },
                new DashboardMenuItem { Icon = "transfer", Title = "Chuyển khoản", Description = "Quản lý các giao dịch thanh toán chuyển khoản", Controller = "Dashboard", Action = "Feature", FeatureKey = "transfers" },
                new DashboardMenuItem { Icon = "account", Title = "Tài khoản", Description = "Quản lý người dùng, vai trò và quyền truy cập", Controller = "Admin", Action = "Accounts" },
                new DashboardMenuItem { Icon = "price", Title = "Giá sản phẩm", Description = "Cập nhật bảng giá cà phê và tiêu hiện hành", Controller = "Dashboard", Action = "Feature", FeatureKey = "prices" }
            },
            Warehouses = new[]
            {
                new WarehouseDashboardCard
                {
                    Name = "Kho Dương Văn Dũng",
                    Note = "Kho chính theo dõi đầy đủ tồn kho, nhập hàng, xuất hàng và tài chính vận hành.",
                    Products = new[]
                    {
                        new WarehouseProductCard
                        {
                            Name = "Cà phê",
                            Theme = "coffee",
                            Metrics = new[]
                            {
                                new WarehouseMetric { Icon = "inventory", Label = "Tồn kho", Value = "19,319.6", Unit = "kg" },
                                new WarehouseMetric { Icon = "weight", Label = "Trọng lượng trừ bì", Value = "17,213", Unit = "kg" },
                                new WarehouseMetric { Icon = "standard", Label = "Quy chuẩn", Value = "6,374.6", Unit = "kg" },
                                new WarehouseMetric { Icon = "inbound", Label = "Nhập hàng", Value = "1,732.1", Unit = "kg" },
                                new WarehouseMetric { Icon = "outbound", Label = "Xuất hàng", Value = "0", Unit = "kg" }
                            }
                        },
                        new WarehouseProductCard
                        {
                            Name = "Tiêu",
                            Theme = "pepper",
                            Metrics = new[]
                            {
                                new WarehouseMetric { Icon = "inventory", Label = "Tồn kho", Value = "231,839.5", Unit = "kg" },
                                new WarehouseMetric { Icon = "weight", Label = "Trọng lượng trừ bì", Value = "224,334.6", Unit = "kg" },
                                new WarehouseMetric { Icon = "standard", Label = "Quy chuẩn", Value = "-272,710.8", Unit = "kg" },
                                new WarehouseMetric { Icon = "inbound", Label = "Nhập hàng", Value = "4,579.7", Unit = "kg" },
                                new WarehouseMetric { Icon = "outbound", Label = "Xuất hàng", Value = "0", Unit = "kg" }
                            }
                        }
                    },
                    FinanceCards = new[]
                    {
                        CreateExpenseCard("18,703,000", "4,076,000", "146,295,000", "14,627,000", "652,383,000", "0"),
                        CreateDebtCard("0", "0", "0")
                    }
                },
                new WarehouseDashboardCard
                {
                    Name = "Kho Thông Đào",
                    Note = "Kho vệ tinh tập trung cân đối tiêu, đồng thời cập nhật số liệu cà phê theo phát sinh.",
                    Products = new[]
                    {
                        new WarehouseProductCard
                        {
                            Name = "Cà phê",
                            Theme = "coffee",
                            Metrics = new[]
                            {
                                new WarehouseMetric { Icon = "inventory", Label = "Tồn kho", Value = "0", Unit = "kg" },
                                new WarehouseMetric { Icon = "weight", Label = "Trọng lượng trừ bì", Value = "1,493.6", Unit = "kg" },
                                new WarehouseMetric { Icon = "standard", Label = "Quy chuẩn", Value = "0", Unit = "kg" },
                                new WarehouseMetric { Icon = "inbound", Label = "Nhập hàng", Value = "0", Unit = "kg" },
                                new WarehouseMetric { Icon = "outbound", Label = "Xuất hàng", Value = "0", Unit = "kg" }
                            }
                        },
                        new WarehouseProductCard
                        {
                            Name = "Tiêu",
                            Theme = "pepper",
                            Metrics = new[]
                            {
                                new WarehouseMetric { Icon = "inventory", Label = "Tồn kho", Value = "4,414,220", Unit = "kg" },
                                new WarehouseMetric { Icon = "weight", Label = "Trọng lượng trừ bì", Value = "4,373,074.8", Unit = "kg" },
                                new WarehouseMetric { Icon = "standard", Label = "Quy chuẩn", Value = "4,583,675.7", Unit = "kg" },
                                new WarehouseMetric { Icon = "inbound", Label = "Nhập hàng", Value = "0", Unit = "kg" },
                                new WarehouseMetric { Icon = "outbound", Label = "Xuất hàng", Value = "0", Unit = "kg" }
                            }
                        }
                    },
                    FinanceCards = new[]
                    {
                        CreateExpenseCard("6,250,000", "1,100,000", "0", "5,150,000", "0", "0"),
                        CreateDebtCard("1,250,000", "0", "1,250,000")
                    }
                },
                new WarehouseDashboardCard
                {
                    Name = "Kho Ymoal",
                    Note = "Kho hỗ trợ thu mua tiêu và cà phê khu vực vệ tinh, ưu tiên cập nhật nhập và công nợ.",
                    Products = new[]
                    {
                        new WarehouseProductCard
                        {
                            Name = "Cà phê",
                            Theme = "coffee",
                            Metrics = new[]
                            {
                                new WarehouseMetric { Icon = "inventory", Label = "Tồn kho", Value = "2,800", Unit = "kg" },
                                new WarehouseMetric { Icon = "weight", Label = "Trọng lượng trừ bì", Value = "2,640", Unit = "kg" },
                                new WarehouseMetric { Icon = "standard", Label = "Quy chuẩn", Value = "1,750", Unit = "kg" },
                                new WarehouseMetric { Icon = "inbound", Label = "Nhập hàng", Value = "520", Unit = "kg" },
                                new WarehouseMetric { Icon = "outbound", Label = "Xuất hàng", Value = "80", Unit = "kg" }
                            }
                        },
                        new WarehouseProductCard
                        {
                            Name = "Tiêu",
                            Theme = "pepper",
                            Metrics = new[]
                            {
                                new WarehouseMetric { Icon = "inventory", Label = "Tồn kho", Value = "56,400", Unit = "kg" },
                                new WarehouseMetric { Icon = "weight", Label = "Trọng lượng trừ bì", Value = "54,980", Unit = "kg" },
                                new WarehouseMetric { Icon = "standard", Label = "Quy chuẩn", Value = "58,320", Unit = "kg" },
                                new WarehouseMetric { Icon = "inbound", Label = "Nhập hàng", Value = "1,120", Unit = "kg" },
                                new WarehouseMetric { Icon = "outbound", Label = "Xuất hàng", Value = "110", Unit = "kg" }
                            }
                        }
                    },
                    FinanceCards = new[]
                    {
                        CreateExpenseCard("3,920,000", "920,000", "0", "2,650,000", "350,000", "0"),
                        CreateDebtCard("860,000", "320,000", "540,000")
                    }
                },
                new WarehouseDashboardCard
                {
                    Name = "Kho Cư Jut",
                    Note = "Kho gom hàng trung chuyển, theo dõi sát tồn kho quy chuẩn trước khi điều phối.",
                    Products = new[]
                    {
                        new WarehouseProductCard
                        {
                            Name = "Cà phê",
                            Theme = "coffee",
                            Metrics = new[]
                            {
                                new WarehouseMetric { Icon = "inventory", Label = "Tồn kho", Value = "1,700", Unit = "kg" },
                                new WarehouseMetric { Icon = "weight", Label = "Trọng lượng trừ bì", Value = "1,590", Unit = "kg" },
                                new WarehouseMetric { Icon = "standard", Label = "Quy chuẩn", Value = "980", Unit = "kg" },
                                new WarehouseMetric { Icon = "inbound", Label = "Nhập hàng", Value = "240", Unit = "kg" },
                                new WarehouseMetric { Icon = "outbound", Label = "Xuất hàng", Value = "50", Unit = "kg" }
                            }
                        },
                        new WarehouseProductCard
                        {
                            Name = "Tiêu",
                            Theme = "pepper",
                            Metrics = new[]
                            {
                                new WarehouseMetric { Icon = "inventory", Label = "Tồn kho", Value = "38,260", Unit = "kg" },
                                new WarehouseMetric { Icon = "weight", Label = "Trọng lượng trừ bì", Value = "37,920", Unit = "kg" },
                                new WarehouseMetric { Icon = "standard", Label = "Quy chuẩn", Value = "39,880", Unit = "kg" },
                                new WarehouseMetric { Icon = "inbound", Label = "Nhập hàng", Value = "690", Unit = "kg" },
                                new WarehouseMetric { Icon = "outbound", Label = "Xuất hàng", Value = "95", Unit = "kg" }
                            }
                        }
                    },
                    FinanceCards = new[]
                    {
                        CreateExpenseCard("2,680,000", "450,000", "125,000", "1,800,000", "305,000", "0"),
                        CreateDebtCard("420,000", "120,000", "300,000")
                    }
                },
                new WarehouseDashboardCard
                {
                    Name = "Kho Hòa Phú",
                    Note = "Kho thu mua cuối tuyến, duy trì lượng hàng ổn định và xử lý nợ phát sinh nhỏ.",
                    Products = new[]
                    {
                        new WarehouseProductCard
                        {
                            Name = "Cà phê",
                            Theme = "coffee",
                            Metrics = new[]
                            {
                                new WarehouseMetric { Icon = "inventory", Label = "Tồn kho", Value = "0", Unit = "kg" },
                                new WarehouseMetric { Icon = "weight", Label = "Trọng lượng trừ bì", Value = "845", Unit = "kg" },
                                new WarehouseMetric { Icon = "standard", Label = "Quy chuẩn", Value = "780", Unit = "kg" },
                                new WarehouseMetric { Icon = "inbound", Label = "Nhập hàng", Value = "160", Unit = "kg" },
                                new WarehouseMetric { Icon = "outbound", Label = "Xuất hàng", Value = "50", Unit = "kg" }
                            }
                        },
                        new WarehouseProductCard
                        {
                            Name = "Tiêu",
                            Theme = "pepper",
                            Metrics = new[]
                            {
                                new WarehouseMetric { Icon = "inventory", Label = "Tồn kho", Value = "34,760", Unit = "kg" },
                                new WarehouseMetric { Icon = "weight", Label = "Trọng lượng trừ bì", Value = "33,980", Unit = "kg" },
                                new WarehouseMetric { Icon = "standard", Label = "Quy chuẩn", Value = "19,510", Unit = "kg" },
                                new WarehouseMetric { Icon = "inbound", Label = "Nhập hàng", Value = "500", Unit = "kg" },
                                new WarehouseMetric { Icon = "outbound", Label = "Xuất hàng", Value = "145", Unit = "kg" }
                            }
                        }
                    },
                    FinanceCards = new[]
                    {
                        CreateExpenseCard("1,940,000", "280,000", "0", "1,410,000", "250,000", "0"),
                        CreateDebtCard("260,000", "0", "260,000")
                    }
                }
            },
            FinanceCards = new[]
            {
                new DashboardFinanceCard
                {
                    Icon = "report",
                    Title = "Lợi nhuận tháng",
                    Accent = "profit",
                    Metrics = new[]
                    {
                        new WarehouseMetric { Icon = "money", Label = "Doanh thu", Value = "1,000,000,000", Unit = "đồng" },
                        new WarehouseMetric { Icon = "expense", Label = "Chi phí", Value = "100,000.00", Unit = "đồng" },
                        new WarehouseMetric { Icon = "profit", Label = "Lợi nhuận", Value = "100,000.00", Unit = "đồng" },
                        new WarehouseMetric { Icon = "inventory", Label = "Hàng gửi kho", Value = "100,000.00", Unit = "kg" }
                    }
                }
            },
            ProductPrices = new[]
            {
                new ProductPriceCard { Name = "Cà phê", Price = "87,000" },
                new ProductPriceCard { Name = "Tiêu", Price = "140,000" }
            },
            ChartSection = new DashboardChartSection
            {
                InboundLabels = new[] { "Tuần 1", "Tuần 2", "Tuần 3", "Tuần 4" },
                InboundSeries = new[]
                {
                    new DashboardChartSeries
                    {
                        Name = "Cà phê",
                        Color = "#9b5f34",
                        Values = new double[] { 420, 610, 385, 737.1 }
                    },
                    new DashboardChartSeries
                    {
                        Name = "Tiêu",
                        Color = "#4f7c43",
                        Values = new double[] { 980, 1260, 1040, 1299.7 }
                    }
                },
                InventoryShare = new[]
                {
                    new DashboardChartPoint { Label = "Dương Văn Dũng", Value = 251159.1, Color = "#c88b52" },
                    new DashboardChartPoint { Label = "Thông Đào", Value = 4414220, Color = "#6f925d" },
                    new DashboardChartPoint { Label = "Ymoal", Value = 59200, Color = "#7e94c9" },
                    new DashboardChartPoint { Label = "Cư Jut", Value = 39960, Color = "#d2a94f" },
                    new DashboardChartPoint { Label = "Hòa Phú", Value = 34760, Color = "#b56d6d" }
                },
                WarehouseLabels = new[] { "DVD", "Thông Đào", "Ymoal", "Cư Jut", "Hòa Phú" },
                StandardSeries = new[]
                {
                    new DashboardChartSeries
                    {
                        Name = "Quy chuẩn cà phê",
                        Color = "#a56739",
                        Values = new double[] { 6374.6, 0, 1750, 980, 780 }
                    },
                    new DashboardChartSeries
                    {
                        Name = "Quy chuẩn tiêu",
                        Color = "#5d874d",
                        Values = new double[] { -272710.8, 4583675.7, 58320, 39880, 19510 }
                    }
                },
                FinanceSeries = new[]
                {
                    new DashboardChartSeries
                    {
                        Name = "Chi phí",
                        Color = "#b8743c",
                        Values = new double[] { 18703000, 6250000, 3920000, 2680000, 1940000 }
                    },
                    new DashboardChartSeries
                    {
                        Name = "Nợ",
                        Color = "#7a5f9a",
                        Values = new double[] { 0, 1250000, 860000, 420000, 260000 }
                    }
                }
            }
        };

        return View(model);
    }

    public IActionResult Feature(string? featureKey = null)
    {
        var normalized = (featureKey ?? string.Empty).Trim().ToLowerInvariant();

        var featureTitle = normalized switch
        {
            "warehouse" => "Kho hàng",
            "receiving" => "Nhập hàng",
            "shipping" => "Xuất hàng",
            "data" => "Dữ liệu",
            "reports" => "Báo cáo",
            "cashier" => "Thủ quỹ",
            "customers" => "Khách hàng",
            "transfers" => "Chuyển khoản",
            "prices" => "Giá sản phẩm",
            _ => "Tính năng đang phát triển"
        };

        return View("FeaturePlaceholder", $"Khu vực {featureTitle} đang được chuẩn bị.");
    }
}
