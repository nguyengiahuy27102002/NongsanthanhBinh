using DuongVanDung.WebApp.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using DuongVanDung.WebApp.Models.Report;

namespace DuongVanDung.WebApp.Services.Report;

public interface IQualityReportService
{
    Task<QualityReportViewModel> GetReportAsync(QualityReportFilter filter);
}

public sealed class QualityReportService : IQualityReportService
{
    private sealed record ImportSource(string SqlTable, string Branch, string Product, bool HasDem, string WeightCol, string PriceExpr);
    private sealed record ExportSource(string SqlTable, string Branch, string Product, bool HasDem);

    private static readonly ImportSource[] MainDbImports =
    {
        new("duongvandung",      "Dương Văn Dũng", "Cà phê", false, "TrongLuong",     "ISNULL(GiaCaPhe,0)"),
        new("nhapCaPheSi",       "Dương Văn Dũng", "Cà phê", false, "TrongLuongHang", "ISNULL(GiaCaPhe,0)"),
        new("xntTieu",           "Dương Văn Dũng", "Tiêu",   true,  "TrongLuong",     "COALESCE(NULLIF(GiaMoi,0),GiaTieu,0)"),
        new("nhapTieuKhachSi",   "Dương Văn Dũng", "Tiêu",   true,  "TrongLuongHang", "COALESCE(NULLIF(GiaMoi,0),GiaCaPhe,0)"),
        new("NhapCaPheThongDao", "Thông Đào",      "Cà phê", false, "TrongLuongHang", "ISNULL(GiaCaPhe,0)"),
        new("NhapTieuThongDao",  "Thông Đào",      "Tiêu",   true,  "TrongLuongHang", "COALESCE(NULLIF(GiaMoi,0),GiaCaPhe,0)"),
    };
    private static readonly ImportSource[] YmoalDbImports =
    {
        new("NhapCaPheLe", "Ymoal", "Cà phê", false, "TrongLuong",     "ISNULL(GiaCaPhe,0)"),
        new("NhapCaPheSi", "Ymoal", "Cà phê", false, "TrongLuongHang", "ISNULL(GiaCaPhe,0)"),
        new("NhapTieuLe",  "Ymoal", "Tiêu",   true,  "TrongLuong",     "COALESCE(NULLIF(GiaMoi,0),GiaTieu,0)"),
        new("NhapTieuSi",  "Ymoal", "Tiêu",   true,  "TrongLuongHang", "COALESCE(NULLIF(GiaMoi,0),GiaCaPhe,0)"),
    };
    private static readonly ExportSource[] MainDbExports =
    {
        new("xuatCaPhe",         "Dương Văn Dũng", "Cà phê", false),
        new("xuatTieu",          "Dương Văn Dũng", "Tiêu",   true),
        new("XuatCaPheThongDao", "Thông Đào",      "Cà phê", false),
        new("XuatTieuThongDao",  "Thông Đào",      "Tiêu",   true),
    };
    private static readonly ExportSource[] YmoalDbExports =
    {
        new("XuatCaPhe", "Ymoal", "Cà phê", false),
        new("XuatTieu",  "Ymoal", "Tiêu",   true),
    };

    private const decimal CoffeeMoistureNeutral = 15m, PepperMoistureNeutral = 15m, ImpurityNeutral = 1m, DemNeutral = 500m;

    private readonly string _connMain, _connYmoal;
    private readonly IMemoryCache _cache;

    public QualityReportService(IConfiguration cfg, IMemoryCache cache)
    {
        _connMain  = cfg.GetConnectionString("DefaultConnection")!;
        _connYmoal = cfg.GetConnectionString("KhoYmoal")!;
        _cache     = cache;
    }

    // ── Entry point with cache ───────────────────────────────────────────────
    public async Task<QualityReportViewModel> GetReportAsync(QualityReportFilter filter)
    {
        var cacheKey = $"quality_{filter.DateFromStr}_{filter.DateToStr}_{filter.Branch}";
        if (_cache.TryGetValue(cacheKey, out QualityReportViewModel? cached) && cached != null)
            return cached;

        var vm = await BuildReportAsync(filter);
        _cache.Set(cacheKey, vm, TimeSpan.FromMinutes(5));
        return vm;
    }

    // ── Core report builder ──────────────────────────────────────────────────
    private async Task<QualityReportViewModel> BuildReportAsync(QualityReportFilter filter)
    {
        var trendStart = new DateTime(VietnamTime.Now.Year, VietnamTime.Now.Month, 1).AddMonths(-11);
        var wideFrom = trendStart < filter.DateFrom.AddMonths(-3) ? trendStart : filter.DateFrom.AddMonths(-3);

        // Combine current + lookback in one query per DB (reduce connections)
        // Last year fetched only if needed (separate smaller query)
        var lyFrom = filter.DateFrom.AddYears(-1);
        var lyTo   = filter.DateTo.AddYears(-1);

        var t1 = FetchImportRowsAsync(_connMain,  MainDbImports,  wideFrom, filter.DateTo);
        var t2 = FetchImportRowsAsync(_connYmoal, YmoalDbImports, wideFrom, filter.DateTo);
        var t3 = FetchExportRowsAsync(_connMain,  MainDbExports,  filter.DateFrom, filter.DateTo);
        var t4 = FetchExportRowsAsync(_connYmoal, YmoalDbExports, filter.DateFrom, filter.DateTo);
        var t5 = FetchImportRowsAsync(_connMain,  MainDbImports,  lyFrom, lyTo);
        var t6 = FetchImportRowsAsync(_connYmoal, YmoalDbImports, lyFrom, lyTo);
        await Task.WhenAll(t1, t2, t3, t4, t5, t6);

        var allImports = new List<QualityDataRow>(t1.Result.Count + t2.Result.Count);
        allImports.AddRange(t1.Result); allImports.AddRange(t2.Result);
        var allExports = new List<ExportRow>(t3.Result.Count + t4.Result.Count);
        allExports.AddRange(t3.Result); allExports.AddRange(t4.Result);
        var lyImports = new List<QualityDataRow>(t5.Result.Count + t6.Result.Count);
        lyImports.AddRange(t5.Result); lyImports.AddRange(t6.Result);

        var reportData = allImports.Where(r => r.PurchaseDate.Date >= filter.DateFrom.Date && r.PurchaseDate.Date <= filter.DateTo.Date);
        if (!string.IsNullOrEmpty(filter.Branch)) reportData = reportData.Where(r => r.Branch == filter.Branch);
        var data = reportData.ToList();

        var lyData = lyImports.AsEnumerable();
        if (!string.IsNullOrEmpty(filter.Branch)) lyData = lyData.Where(r => r.Branch == filter.Branch);
        var ly = lyData.ToList();

        var coffee = data.Where(r => r.Product == "Cà phê").ToList();
        var pepper = data.Where(r => r.Product == "Tiêu").ToList();

        var vm = new QualityReportViewModel { Filter = filter };
        vm.AvailableBranches = allImports.Select(r => r.Branch).Distinct().OrderBy(b => b).ToList();
        vm.CoffeeKpi = BuildProductKpi("Cà phê", coffee);
        vm.PepperKpi = BuildProductKpi("Tiêu", pepper);

        if (data.Count == 0 && allExports.Count == 0) return vm;

        if (data.Count > 0)
        {
            vm.BranchComparison = data.GroupBy(r => r.Branch).Select(g => new BranchQuality
            {
                Branch = g.Key,
                CoffeeVolume = g.Where(r => r.Product == "Cà phê").Sum(r => r.Volume),
                PepperVolume = g.Where(r => r.Product == "Tiêu").Sum(r => r.Volume),
                QualityScore = ComputeScore(g.ToList()),
            }).OrderByDescending(b => b.TotalVolume).ToList();

            // Monthly trends (12 months)
            var trendData = allImports.Where(r => r.PurchaseDate >= trendStart);
            if (!string.IsNullOrEmpty(filter.Branch)) trendData = trendData.Where(r => r.Branch == filter.Branch);
            vm.Trends = trendData.GroupBy(r => new { r.PurchaseDate.Year, r.PurchaseDate.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g =>
                {
                    var gc = g.Where(r => r.Product == "Cà phê").ToList();
                    var gp = g.Where(r => r.Product == "Tiêu").ToList();
                    var pd = gp.Where(r => r.Dem.HasValue && r.Dem > 0).ToList();
                    return new QualityTrend
                    {
                        Label = $"{g.Key.Month:00}/{g.Key.Year}",
                        CoffeeMoisture = gc.Count > 0 ? Math.Round(gc.Average(r => r.Moisture), 2) : 0,
                        CoffeeImpurity = gc.Count > 0 ? Math.Round(gc.Average(r => r.Impurity), 2) : 0,
                        PepperMoisture = gp.Count > 0 ? Math.Round(gp.Average(r => r.Moisture), 2) : 0,
                        PepperImpurity = gp.Count > 0 ? Math.Round(gp.Average(r => r.Impurity), 2) : 0,
                        PepperDem = pd.Count > 0 ? Math.Round(pd.Average(r => r.Dem!.Value), 1) : null,
                    };
                }).ToList();

            // YoY
            vm.YoY = new YearOverYearQuality
            {
                HasLastYear = ly.Count > 0,
                CoffeeRows = BuildYoYRows(coffee, ly.Where(r => r.Product == "Cà phê").ToList(), false),
                PepperRows = BuildYoYRows(pepper, ly.Where(r => r.Product == "Tiêu").ToList(), true),
            };

            vm.TopCoffeeSuppliers = BuildTopSuppliers(coffee, 10);
            vm.TopPepperSuppliers = BuildTopSuppliers(pepper, 10);
            vm.Alerts = BuildAlerts(data, allExports, filter);
            vm.CoffeeKpi.AlertCount = vm.Alerts.Count(a => a.Product == "Cà phê");
            vm.PepperKpi.AlertCount = vm.Alerts.Count(a => a.Product == "Tiêu");
            vm.MoistureDistribution = BuildDistribution(data.Select(r => r.Moisture), new[] { 0m, 10, 12, 13, 14, 15, 16, 18, 20, 25, 100 });
            vm.ImpurityDistribution = BuildDistribution(data.Select(r => r.Impurity), new[] { 0m, 0.5m, 0.8m, 1, 1.5m, 2, 3, 5, 100 });
        }

        vm.ExportComparisons = BuildFifoComparison(allImports, allExports, filter);
        if (vm.ExportComparisons.Count > 0)
        {
            var mD = vm.ExportComparisons.Select(e => e.MoistureDelta).ToList();
            var iD = vm.ExportComparisons.Select(e => e.ImpurityDelta).ToList();
            var dD = vm.ExportComparisons.Where(e => e.DemDelta.HasValue).Select(e => e.DemDelta!.Value).ToList();
            vm.FifoSummary = new FifoProfitSummary
            {
                TotalExports = vm.ExportComparisons.Count,
                TotalExportKg = vm.ExportComparisons.Sum(e => e.ExportWeight),
                AvgMoistureDelta = Math.Round(mD.Average(), 2),
                AvgImpurityDelta = Math.Round(iD.Average(), 2),
                AvgDemDelta = dD.Count > 0 ? Math.Round(dD.Average(), 1) : null,
                ProfitableCount = vm.ExportComparisons.Count(e => e.MoistureDelta <= 0 && e.ImpurityDelta <= 0),
                LossCount = vm.ExportComparisons.Count(e => e.MoistureDelta > 0.5m || e.ImpurityDelta > 0.2m),
            };
        }

        return vm;
    }

    // ── Helpers (unchanged logic, optimized SQL) ─────────────────────────────
    private static ProductKpi BuildProductKpi(string name, List<QualityDataRow> rows)
    {
        if (rows.Count == 0) return new ProductKpi { ProductName = name };
        var pd = rows.Where(r => r.Dem.HasValue && r.Dem > 0).ToList();
        return new ProductKpi
        {
            ProductName = name, TotalRecords = rows.Count,
            TotalVolume = rows.Sum(r => r.Volume), TotalStandard = rows.Sum(r => r.StandardQty),
            AvgMoisture = Math.Round(rows.Average(r => r.Moisture), 2),
            AvgImpurity = Math.Round(rows.Average(r => r.Impurity), 2),
            AvgDem = pd.Count > 0 ? Math.Round(pd.Average(r => r.Dem!.Value), 1) : null,
            TotalAmount = rows.Sum(r => r.Amount),
        };
    }

    private static IReadOnlyList<YoYRow> BuildYoYRows(List<QualityDataRow> curr, List<QualityDataRow> ly, bool dem)
    {
        if (curr.Count == 0) return Array.Empty<YoYRow>();
        var rows = new List<YoYRow>
        {
            new() { Metric = "Độ ẩm TB (%)", CurrentValue = Math.Round(curr.Average(r => r.Moisture), 2), LastYearValue = ly.Count > 0 ? Math.Round(ly.Average(r => r.Moisture), 2) : 0 },
            new() { Metric = "Tạp chất TB (%)", CurrentValue = Math.Round(curr.Average(r => r.Impurity), 2), LastYearValue = ly.Count > 0 ? Math.Round(ly.Average(r => r.Impurity), 2) : 0 },
        };
        if (dem) { var cd = curr.Where(r => r.Dem > 0).ToList(); var ld = ly.Where(r => r.Dem > 0).ToList(); if (cd.Count > 0) rows.Add(new YoYRow { Metric = "DEM TB", CurrentValue = Math.Round(cd.Average(r => r.Dem!.Value), 1), LastYearValue = ld.Count > 0 ? Math.Round(ld.Average(r => r.Dem!.Value), 1) : 0 }); }
        return rows;
    }

    private static IReadOnlyList<SupplierQuality> BuildTopSuppliers(List<QualityDataRow> rows, int top)
    {
        return rows.Where(r => !string.IsNullOrWhiteSpace(r.Supplier))
            .GroupBy(r => new { r.Supplier, r.Branch }).Where(g => g.Count() >= 2)
            .Select(g => { var it = g.ToList(); var pd = it.Where(r => r.Dem > 0).ToList(); return new SupplierQuality { Name = g.Key.Supplier, Branch = g.Key.Branch, Transactions = it.Count, TotalVolume = it.Sum(r => r.Volume), AvgMoisture = Math.Round(it.Average(r => r.Moisture), 2), AvgImpurity = Math.Round(it.Average(r => r.Impurity), 2), AvgDem = pd.Count > 0 ? Math.Round(pd.Average(r => r.Dem!.Value), 1) : null, TotalAmount = it.Sum(r => r.Amount), QualityScore = ComputeScore(it) }; })
            .OrderByDescending(s => s.QualityScore).Take(top).ToList();
    }

    private IReadOnlyList<QualityAlert> BuildAlerts(List<QualityDataRow> imports, List<ExportRow> exports, QualityReportFilter filter)
    {
        var alerts = new List<QualityAlert>(); var seen = new HashSet<string>();
        var avg = imports.GroupBy(r => (r.Branch, r.Product)).ToDictionary(g => g.Key, g => (m: g.Average(r => r.Moisture), i: g.Average(r => r.Impurity)));
        foreach (var r in imports.OrderByDescending(r => r.PurchaseDate))
        {
            var dk = $"I|{r.Supplier}|{r.PurchaseDate:yyyyMMdd}|{r.Moisture}|{r.Impurity}|{r.Dem}";
            if (!seen.Add(dk)) continue;
            var mn = r.Product == "Tiêu" ? PepperMoistureNeutral : CoffeeMoistureNeutral;
            var a = avg.GetValueOrDefault((r.Branch, r.Product));
            var md = r.Moisture - (decimal)a.m;
            if (r.Moisture > mn + 5 || md > 5) alerts.Add(new QualityAlert { Severity = "danger", Type = "moisture", Product = r.Product, Message = $"Độ ẩm {r.Moisture}% — vượt ngưỡng {mn}%", Branch = r.Branch, Supplier = r.Supplier, Date = r.PurchaseDate, Value = r.Moisture, Threshold = mn, OrderId = r.OrderId });
            else if (r.Moisture > mn + 2 && md > 2) alerts.Add(new QualityAlert { Severity = "warning", Type = "moisture", Product = r.Product, Message = $"Độ ẩm {r.Moisture}%", Branch = r.Branch, Supplier = r.Supplier, Date = r.PurchaseDate, Value = r.Moisture, Threshold = mn, OrderId = r.OrderId });
            var id = r.Impurity - (decimal)a.i;
            if (r.Impurity > ImpurityNeutral + 1.5m || id > 1.5m) alerts.Add(new QualityAlert { Severity = "danger", Type = "impurity", Product = r.Product, Message = $"Tạp chất {r.Impurity}%", Branch = r.Branch, Supplier = r.Supplier, Date = r.PurchaseDate, Value = r.Impurity, Threshold = ImpurityNeutral, OrderId = r.OrderId });
            else if (r.Impurity > ImpurityNeutral + 0.5m && id > 0.5m) alerts.Add(new QualityAlert { Severity = "warning", Type = "impurity", Product = r.Product, Message = $"Tạp chất {r.Impurity}%", Branch = r.Branch, Supplier = r.Supplier, Date = r.PurchaseDate, Value = r.Impurity, Threshold = ImpurityNeutral, OrderId = r.OrderId });
            if (r.Dem > 0 && r.Dem < DemNeutral - 100) alerts.Add(new QualityAlert { Severity = "danger", Type = "dem", Product = r.Product, Message = $"DEM {r.Dem}", Branch = r.Branch, Supplier = r.Supplier, Date = r.PurchaseDate, Value = r.Dem!.Value, Threshold = DemNeutral, OrderId = r.OrderId });
        }
        var fe = exports.AsEnumerable(); if (!string.IsNullOrEmpty(filter.Branch)) fe = fe.Where(e => e.Branch == filter.Branch);
        foreach (var e in fe) { var mn = e.Product == "Tiêu" ? PepperMoistureNeutral : CoffeeMoistureNeutral; if (e.Moisture > mn + 3) alerts.Add(new QualityAlert { Severity = "warning", Type = "moisture", Product = e.Product, Message = $"Xuất — Ẩm {e.Moisture}%", Branch = e.Branch, Supplier = e.Buyer, Date = e.Date, Value = e.Moisture, Threshold = mn, IsExport = true, OrderId = e.OrderId }); }
        return alerts.OrderByDescending(a => a.Severity == "danger" ? 1 : 0).ThenByDescending(a => Math.Abs(a.Value - a.Threshold)).Take(50).ToList();
    }

    private static IReadOnlyList<ExportVsImportRow> BuildFifoComparison(List<QualityDataRow> allImports, List<ExportRow> exports, QualityReportFilter filter)
    {
        if (exports.Count == 0) return Array.Empty<ExportVsImportRow>();
        var fe = exports.AsEnumerable(); if (!string.IsNullOrEmpty(filter.Branch)) fe = fe.Where(e => e.Branch == filter.Branch);
        var byKey = allImports.GroupBy(i => (i.Branch, i.Product)).ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.PurchaseDate).ThenByDescending(i => i.Volume).ToList());
        var result = new List<ExportVsImportRow>();
        foreach (var exp in fe.OrderByDescending(e => e.Date).Take(50))
        {
            if (exp.Weight <= 0 || !byKey.TryGetValue((exp.Branch, exp.Product), out var imp)) continue;
            decimal tot = 0, wM = 0, wI = 0, wD = 0; int dc = 0, mc = 0; var det = new List<MatchedImportDetail>();
            foreach (var i in imp) { if (i.PurchaseDate > exp.Date || tot >= exp.Weight) continue; var t = Math.Min(i.Volume, exp.Weight - tot); if (t <= 0) continue; tot += t; wM += i.Moisture * t; wI += i.Impurity * t; if (i.Dem > 0) { wD += i.Dem!.Value * t; dc++; } mc++; det.Add(new MatchedImportDetail { Date = i.PurchaseDate, Supplier = i.Supplier, TotalWeight = i.Volume, MatchedWeight = t, Moisture = i.Moisture, Impurity = i.Impurity, Dem = i.Dem, OrderId = i.OrderId }); }
            if (tot <= 0) continue;
            result.Add(new ExportVsImportRow { ExportDate = exp.Date, Buyer = exp.Buyer, Destination = exp.Destination, Branch = exp.Branch, Product = exp.Product, ExportWeight = exp.Weight, ExportMoisture = exp.Moisture, ExportImpurity = exp.Impurity, ExportDem = exp.Dem, ExportStandard = exp.Standard, ExportBags = exp.Bags, ExportOrderId = exp.OrderId, ImportAvgMoisture = Math.Round(wM / tot, 2), ImportAvgImpurity = Math.Round(wI / tot, 2), ImportAvgDem = dc > 0 ? Math.Round(wD / tot, 1) : null, ImportMatchedWeight = Math.Round(tot, 1), ImportMatchedCount = mc, MatchedImports = det });
        }
        return result;
    }

    private static double ComputeScore(List<QualityDataRow> rows)
    {
        if (rows.Count == 0) return 50;
        double s = 50 + (double)(15m - rows.Average(r => r.Moisture)) * 3;
        var ai = rows.Average(r => r.Impurity); s += ai <= ImpurityNeutral ? (double)(ImpurityNeutral - ai) * 20 : -(double)(ai - ImpurityNeutral) * 50;
        var pd = rows.Where(r => r.Dem > 0).ToList(); if (pd.Count > 0) s += (double)(pd.Average(r => r.Dem!.Value) - DemNeutral) / 50 * 2;
        return Math.Round(Math.Clamp(s, 0, 100), 1);
    }

    // ── Optimized SQL: no CAST on date column → allows index usage ───────────
    private async Task<List<QualityDataRow>> FetchImportRowsAsync(string connStr, ImportSource[] tables, DateTime from, DateTime to)
    {
        var rows = new List<QualityDataRow>(); var parts = new List<string>();
        foreach (var t in tables)
        {
            var dem = t.HasDem ? "ISNULL(Dem,0)" : "CAST(NULL AS decimal(18,2))";
            // Use direct date comparison — no CAST, allows index seek
            parts.Add($@"SELECT N'{t.Branch}',N'{t.Product}',ngayNhap,ISNULL(TenKhachHang,N''),ISNULL({t.WeightCol},0),ISNULL(DoAm,0),ISNULL(TapChat,0),{dem},ISNULL(QuyChuan,0),CAST(ISNULL(ThanhTien,0) AS bigint),{t.PriceExpr},ISNULL(orderID,0) FROM {t.SqlTable} WITH(NOLOCK) WHERE ngayNhap>=@df AND ngayNhap<@dtNext");
        }
        if (parts.Count == 0) return rows;
        try
        {
            await using var cn = new SqlConnection(connStr); await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = string.Join("\nUNION ALL\n", parts);
            cmd.CommandTimeout = 20;
            cmd.Parameters.AddWithValue("@df", from.Date);
            cmd.Parameters.AddWithValue("@dtNext", to.Date.AddDays(1)); // < next day instead of CAST
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add(new QualityDataRow { Branch = r.GetString(0), Product = r.GetString(1), PurchaseDate = r.GetDateTime(2), Supplier = r.IsDBNull(3) ? "" : r.GetString(3).Trim(), Volume = r.IsDBNull(4) ? 0 : r.GetDecimal(4), Moisture = r.IsDBNull(5) ? 0 : r.GetDecimal(5), Impurity = r.IsDBNull(6) ? 0 : r.GetDecimal(6), Dem = r.IsDBNull(7) ? null : r.GetDecimal(7), StandardQty = r.IsDBNull(8) ? 0 : r.GetDecimal(8), Amount = r.IsDBNull(9) ? 0 : r.GetInt64(9), Price = r.IsDBNull(10) ? 0 : r.GetInt32(10), OrderId = r.IsDBNull(11) ? 0 : r.GetInt32(11) });
        } catch { }
        return rows;
    }

    private sealed class ExportRow { public string Branch = "", Product = "", Buyer = "", Destination = ""; public DateTime Date; public decimal Weight, Moisture, Impurity, Standard; public decimal? Dem; public int Bags, OrderId; }

    private async Task<List<ExportRow>> FetchExportRowsAsync(string connStr, ExportSource[] tables, DateTime from, DateTime to)
    {
        var rows = new List<ExportRow>(); var parts = new List<string>();
        foreach (var t in tables)
        {
            var dem = t.HasDem ? "ISNULL(Dem,0)" : "CAST(NULL AS decimal(18,2))";
            parts.Add($@"SELECT N'{t.Branch}',N'{t.Product}',ngayXuat,ISNULL(TenTaiXe,N''),ISNULL(DiaDiemXuatHang,N''),ISNULL(TrongLuongHang,0),ISNULL(DoAm,0),ISNULL(TapChat,0),{dem},ISNULL(QuyChuan,0),ISNULL(SoBao,0),ISNULL(xuatID,0) FROM {t.SqlTable} WITH(NOLOCK) WHERE ngayXuat>=@df AND ngayXuat<@dtNext");
        }
        if (parts.Count == 0) return rows;
        try
        {
            await using var cn = new SqlConnection(connStr); await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = string.Join("\nUNION ALL\n", parts) + " ORDER BY 3 DESC";
            cmd.CommandTimeout = 20;
            cmd.Parameters.AddWithValue("@df", from.Date);
            cmd.Parameters.AddWithValue("@dtNext", to.Date.AddDays(1));
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add(new ExportRow { Branch = r.GetString(0), Product = r.GetString(1), Date = r.GetDateTime(2), Buyer = r.IsDBNull(3) ? "" : r.GetString(3).Trim(), Destination = r.IsDBNull(4) ? "" : r.GetString(4).Trim(), Weight = r.IsDBNull(5) ? 0 : r.GetDecimal(5), Moisture = r.IsDBNull(6) ? 0 : r.GetDecimal(6), Impurity = r.IsDBNull(7) ? 0 : r.GetDecimal(7), Dem = r.IsDBNull(8) ? null : r.GetDecimal(8), Standard = r.IsDBNull(9) ? 0 : r.GetDecimal(9), Bags = r.IsDBNull(10) ? 0 : r.GetInt32(10), OrderId = r.IsDBNull(11) ? 0 : r.GetInt32(11) });
        } catch { }
        return rows;
    }

    private static IReadOnlyList<DistributionBucket> BuildDistribution(IEnumerable<decimal> vals, decimal[] edges)
    {
        var l = vals.ToList();
        return Enumerable.Range(0, edges.Length - 1).Select(i => new DistributionBucket { Label = i == edges.Length - 2 ? $">{edges[i]}" : $"{edges[i]}-{edges[i + 1]}", Count = l.Count(v => v >= edges[i] && v < edges[i + 1]) }).ToList();
    }
}
