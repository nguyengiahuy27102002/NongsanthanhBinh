namespace DuongVanDung.WebApp.Models.Warehouse;

public sealed class WarehouseOverviewViewModel
{
    public int TotalWarehouses { get; set; }

    // Coffee totals (nhập - xuất)
    public decimal CoffeeTongTon   { get; set; }
    public decimal CoffeeTruBi     { get; set; }
    public decimal CoffeeQuyChuan  { get; set; }
    public decimal CoffeeMonthKg   { get; set; }  // nhập tháng này

    // Pepper totals
    public decimal PepperTongTon   { get; set; }
    public decimal PepperTruBi     { get; set; }
    public decimal PepperQuyChuan  { get; set; }
    public decimal PepperMonthKg   { get; set; }

    public IReadOnlyList<WarehouseCard> Warehouses { get; set; } = Array.Empty<WarehouseCard>();

    // Hourly activity (7am-11pm)
    public IReadOnlyList<HourlyActivity> HourlyActivity { get; set; } = Array.Empty<HourlyActivity>();

    // Quality comparison
    public IReadOnlyList<WarehouseQualityRow> QualityComparison { get; set; } = Array.Empty<WarehouseQualityRow>();
}

public sealed class WarehouseCard
{
    public string  Name { get; set; } = "";
    public string  Status { get; set; } = "normal";
    public string  StatusLabel { get; set; } = "";

    // Coffee stock (nhập - xuất)
    public decimal CoffeeTongTon   { get; set; }
    public decimal CoffeeTruBi     { get; set; }
    public decimal CoffeeQuyChuan  { get; set; }
    public decimal CoffeeMonthKg   { get; set; }    // nhập tháng
    public decimal CoffeeExportKg  { get; set; }    // xuất tháng
    public decimal CoffeeTodayKg   { get; set; }
    public long    CoffeeTodayTien { get; set; }
    public long    CoffeeMonthTien { get; set; }

    // Pepper stock
    public decimal PepperTongTon   { get; set; }
    public decimal PepperTruBi     { get; set; }
    public decimal PepperQuyChuan  { get; set; }
    public decimal PepperMonthKg   { get; set; }
    public decimal PepperExportKg  { get; set; }
    public decimal PepperTodayKg   { get; set; }
    public long    PepperTodayTien { get; set; }
    public long    PepperMonthTien { get; set; }

    // Export today
    public decimal CoffeeExportTodayKg { get; set; }
    public decimal PepperExportTodayKg { get; set; }

    // Dates
    public DateTime? LastImportDate { get; set; }
    public DateTime? LastExportDate { get; set; }
}

public sealed class HourlyActivity
{
    public int Hour  { get; set; }
    public int Count { get; set; }
}

public sealed class WarehouseQualityRow
{
    public string  Name         { get; set; } = "";
    public string  Product      { get; set; } = "";
    public decimal AvgMoisture  { get; set; }
    public decimal AvgImpurity  { get; set; }
    public decimal? AvgDem      { get; set; }
    public decimal TotalKg      { get; set; }
    public double  QualityScore { get; set; }
}

// Trend data (for AJAX period switching)
public sealed class TrendPoint
{
    public string  Label     { get; set; } = "";
    public string  SortKey   { get; set; } = "";   // yyyy-MM-dd for correct ordering
    public string  Warehouse { get; set; } = "";
    public decimal Kg        { get; set; }
}
