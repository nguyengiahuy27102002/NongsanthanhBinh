namespace DuongVanDung.WebApp.Models.Report;

public sealed class QualityReportFilter
{
    public DateTime DateFrom { get; set; } = DateTime.Now.AddDays(-30).Date;
    public DateTime DateTo   { get; set; } = DateTime.Now.Date;
    public string   Branch   { get; set; } = "";
    public string DateFromStr => DateFrom.ToString("yyyy-MM-dd");
    public string DateToStr   => DateTo.ToString("yyyy-MM-dd");
}

public sealed class QualityDataRow
{
    public string   Branch       { get; set; } = "";
    public string   Product      { get; set; } = "";
    public DateTime PurchaseDate { get; set; }
    public string   Supplier     { get; set; } = "";
    public decimal  Volume       { get; set; }
    public decimal  Moisture     { get; set; }
    public decimal  Impurity     { get; set; }
    public decimal? Dem          { get; set; }
    public decimal  StandardQty  { get; set; }
    public long     Amount       { get; set; }
    public int      Price        { get; set; }
    public int      OrderId      { get; set; }
}

// ── KPI per product ──────────────────────────────────────────────────────────
public sealed class ProductKpi
{
    public string   ProductName   { get; set; } = "";
    public int      TotalRecords  { get; set; }
    public decimal  TotalVolume   { get; set; }
    public decimal  TotalStandard { get; set; }
    public decimal  AvgMoisture   { get; set; }
    public decimal  AvgImpurity   { get; set; }
    public decimal? AvgDem        { get; set; }   // pepper only
    public long     TotalAmount   { get; set; }
    public int      AlertCount    { get; set; }
}

// ── Branch quality ───────────────────────────────────────────────────────────
public sealed class BranchQuality
{
    public string  Branch        { get; set; } = "";
    public decimal CoffeeVolume  { get; set; }
    public decimal PepperVolume  { get; set; }
    public decimal TotalVolume   => CoffeeVolume + PepperVolume;
    public double  QualityScore  { get; set; }
}

// ── Monthly quality trend ────────────────────────────────────────────────────
public sealed class QualityTrend
{
    public string  Label       { get; set; } = "";
    public decimal CoffeeMoisture { get; set; }
    public decimal CoffeeImpurity { get; set; }
    public decimal PepperMoisture { get; set; }
    public decimal PepperImpurity { get; set; }
    public decimal? PepperDem     { get; set; }
}

// ── Year-over-year ───────────────────────────────────────────────────────────
public sealed class YoYRow
{
    public string  Metric        { get; set; } = "";
    public decimal CurrentValue  { get; set; }
    public decimal LastYearValue { get; set; }
    public decimal Delta         => CurrentValue - LastYearValue;
}

public sealed class YearOverYearQuality
{
    public bool HasLastYear { get; set; }
    public IReadOnlyList<YoYRow> CoffeeRows { get; set; } = Array.Empty<YoYRow>();
    public IReadOnlyList<YoYRow> PepperRows { get; set; } = Array.Empty<YoYRow>();
}

// ── Supplier quality ranking ─────────────────────────────────────────────────
public sealed class SupplierQuality
{
    public string  Name         { get; set; } = "";
    public string  Branch       { get; set; } = "";
    public int     Transactions { get; set; }
    public decimal TotalVolume  { get; set; }
    public decimal AvgMoisture  { get; set; }
    public decimal AvgImpurity  { get; set; }
    public decimal? AvgDem      { get; set; }
    public long    TotalAmount  { get; set; }
    public double  QualityScore { get; set; }
}

// ── Quality alert ────────────────────────────────────────────────────────────
public sealed class QualityAlert
{
    public string   Severity  { get; set; } = "warning";
    public string   Type      { get; set; } = "";
    public string   Product   { get; set; } = "";
    public string   Message   { get; set; } = "";
    public string   Branch    { get; set; } = "";
    public string   Supplier  { get; set; } = "";
    public DateTime Date      { get; set; }
    public decimal  Value     { get; set; }
    public decimal  Threshold { get; set; }
    public bool     IsExport  { get; set; }
    public int      OrderId   { get; set; }
}

// ── Distribution bucket ──────────────────────────────────────────────────────
public sealed class DistributionBucket
{
    public string Label { get; set; } = "";
    public int    Count { get; set; }
}

// ── Matched import detail ────────────────────────────────────────────────────
public sealed class MatchedImportDetail
{
    public DateTime Date         { get; set; }
    public string   Supplier     { get; set; } = "";
    public decimal  TotalWeight  { get; set; }
    public decimal  MatchedWeight { get; set; }
    public decimal  Moisture     { get; set; }
    public decimal  Impurity     { get; set; }
    public decimal? Dem          { get; set; }
    public int      OrderId      { get; set; }
}

// ── FIFO Export vs Import ────────────────────────────────────────────────────
public sealed class ExportVsImportRow
{
    public DateTime ExportDate      { get; set; }
    public string   Buyer           { get; set; } = "";
    public string   Destination     { get; set; } = "";
    public string   Branch          { get; set; } = "";
    public string   Product         { get; set; } = "";
    public decimal  ExportWeight    { get; set; }
    public decimal  ExportMoisture  { get; set; }
    public decimal  ExportImpurity  { get; set; }
    public decimal? ExportDem       { get; set; }
    public decimal  ExportStandard  { get; set; }
    public int      ExportBags      { get; set; }
    public int      ExportOrderId   { get; set; }
    public decimal  ImportAvgMoisture   { get; set; }
    public decimal  ImportAvgImpurity   { get; set; }
    public decimal? ImportAvgDem        { get; set; }
    public decimal  ImportMatchedWeight { get; set; }
    public int      ImportMatchedCount  { get; set; }
    public IReadOnlyList<MatchedImportDetail> MatchedImports { get; set; } = Array.Empty<MatchedImportDetail>();
    public decimal MoistureDelta => ExportMoisture - ImportAvgMoisture;
    public decimal ImpurityDelta => ExportImpurity - ImportAvgImpurity;
    public decimal? DemDelta     => ExportDem.HasValue && ImportAvgDem.HasValue ? ExportDem - ImportAvgDem : null;
}

// ── FIFO Profit Summary ──────────────────────────────────────────────────────
public sealed class FifoProfitSummary
{
    public int     TotalExports       { get; set; }
    public decimal TotalExportKg      { get; set; }
    public decimal AvgMoistureDelta   { get; set; }   // xuất - nhập: âm = lợi (ẩm giảm)
    public decimal AvgImpurityDelta   { get; set; }
    public decimal? AvgDemDelta       { get; set; }   // dương = lợi (DEM tăng)
    public int     ProfitableCount    { get; set; }   // số lần xuất có lợi
    public int     LossCount          { get; set; }   // số lần xuất bất lợi
}

// ── Complete ViewModel ───────────────────────────────────────────────────────
public sealed class QualityReportViewModel
{
    public QualityReportFilter Filter { get; set; } = new();

    // KPI tách riêng
    public ProductKpi CoffeeKpi { get; set; } = new() { ProductName = "Cà phê" };
    public ProductKpi PepperKpi { get; set; } = new() { ProductName = "Tiêu" };

    public IReadOnlyList<BranchQuality> BranchComparison { get; set; } = Array.Empty<BranchQuality>();
    public IReadOnlyList<QualityTrend> Trends { get; set; } = Array.Empty<QualityTrend>();
    public YearOverYearQuality YoY { get; set; } = new();

    // Top 10 riêng biệt
    public IReadOnlyList<SupplierQuality> TopCoffeeSuppliers { get; set; } = Array.Empty<SupplierQuality>();
    public IReadOnlyList<SupplierQuality> TopPepperSuppliers { get; set; } = Array.Empty<SupplierQuality>();

    public IReadOnlyList<QualityAlert> Alerts { get; set; } = Array.Empty<QualityAlert>();

    public IReadOnlyList<DistributionBucket> MoistureDistribution { get; set; } = Array.Empty<DistributionBucket>();
    public IReadOnlyList<DistributionBucket> ImpurityDistribution { get; set; } = Array.Empty<DistributionBucket>();

    public IReadOnlyList<ExportVsImportRow> ExportComparisons { get; set; } = Array.Empty<ExportVsImportRow>();
    public FifoProfitSummary? FifoSummary { get; set; }

    public IReadOnlyList<string> AvailableBranches { get; set; } = Array.Empty<string>();
}
