namespace DuongVanDung.WebApp.Models.Auth;

public sealed class DashboardViewModel
{
    public string DisplayName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<DashboardCommoditySummary> CommoditySummaries { get; set; } = Array.Empty<DashboardCommoditySummary>();

    public IReadOnlyCollection<DashboardMenuItem> MenuItems { get; set; } = Array.Empty<DashboardMenuItem>();

    public IReadOnlyCollection<WarehouseDashboardCard> Warehouses { get; set; } = Array.Empty<WarehouseDashboardCard>();

    public IReadOnlyCollection<DashboardFinanceCard> FinanceCards { get; set; } = Array.Empty<DashboardFinanceCard>();

    public IReadOnlyCollection<ProductPriceCard> ProductPrices { get; set; } = Array.Empty<ProductPriceCard>();

    public DashboardChartSection? ChartSection { get; set; }
}

public sealed class DashboardCommoditySummary
{
    public string Name { get; set; } = string.Empty;

    public string Theme { get; set; } = string.Empty;

    public IReadOnlyCollection<DashboardSummaryCard> Metrics { get; set; } = Array.Empty<DashboardSummaryCard>();
}

public sealed class DashboardSummaryCard
{
    public string Icon { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;
}

public sealed class DashboardMenuItem
{
    public string Icon { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Controller { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string? FeatureKey { get; set; }
}

public sealed class WarehouseDashboardCard
{
    public string Name { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public IReadOnlyCollection<WarehouseProductCard> Products { get; set; } = Array.Empty<WarehouseProductCard>();

    public IReadOnlyCollection<DashboardFinanceCard> FinanceCards { get; set; } = Array.Empty<DashboardFinanceCard>();
}

public sealed class WarehouseProductCard
{
    public string Name { get; set; } = string.Empty;

    public string Theme { get; set; } = string.Empty;

    public IReadOnlyCollection<WarehouseMetric> Metrics { get; set; } = Array.Empty<WarehouseMetric>();
}

public sealed class WarehouseMetric
{
    public string Icon { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;
}

public sealed class DashboardFinanceCard
{
    public string Icon { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Accent { get; set; } = string.Empty;

    public IReadOnlyCollection<WarehouseMetric> Metrics { get; set; } = Array.Empty<WarehouseMetric>();
}

public sealed class ProductPriceCard
{
    public string Name { get; set; } = string.Empty;

    public string Price { get; set; } = string.Empty;
}

public sealed class DashboardChartSection
{
    public IReadOnlyCollection<DashboardChartPoint> CoffeeShare { get; set; } = Array.Empty<DashboardChartPoint>();

    public IReadOnlyCollection<DashboardChartPoint> PepperShare { get; set; } = Array.Empty<DashboardChartPoint>();

    public IReadOnlyCollection<string> DailyLabels { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<DashboardChartSeries> CoffeeDailySeries { get; set; } = Array.Empty<DashboardChartSeries>();

    public IReadOnlyCollection<DashboardChartSeries> PepperDailySeries { get; set; } = Array.Empty<DashboardChartSeries>();
}

public sealed class DashboardChartSeries
{
    public string Name { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public IReadOnlyCollection<double> Values { get; set; } = Array.Empty<double>();
}

public sealed class DashboardChartPoint
{
    public string Label { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public double Value { get; set; }
}
