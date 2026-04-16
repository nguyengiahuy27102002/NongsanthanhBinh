using DuongVanDung.WebApp.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using DuongVanDung.WebApp.Models.Warehouse;

namespace DuongVanDung.WebApp.Services.Warehouse;

public interface IWarehouseService
{
    Task<WarehouseOverviewViewModel> GetOverviewAsync();
    Task<IReadOnlyList<TrendPoint>> GetTrendAsync(string product, string period);
    Task<IReadOnlyList<TrendPoint>> GetInventoryTrendAsync(string product, string period);
}

public sealed class WarehouseService : IWarehouseService
{
    private sealed record WH(string Name, string ConnKey,
        string[] CoffeeImport, string[] PepperImport,
        string[] CoffeeExport, string[] PepperExport);

    private static readonly WH[] WHs =
    {
        new("Dương Văn Dũng", "DefaultConnection",
            new[]{"duongvandung|TrongLuong","nhapCaPheSi|TrongLuongHang"},
            new[]{"xntTieu|TrongLuong","nhapTieuKhachSi|TrongLuongHang"},
            new[]{"xuatCaPhe"}, new[]{"xuatTieu"}),
        new("Thông Đào", "DefaultConnection",
            new[]{"NhapCaPheThongDao|TrongLuongHang"},
            new[]{"NhapTieuThongDao|TrongLuongHang"},
            new[]{"XuatCaPheThongDao"}, new[]{"XuatTieuThongDao"}),
        new("Ymoal", "KhoYmoal",
            new[]{"NhapCaPheLe|TrongLuong","NhapCaPheSi|TrongLuongHang"},
            new[]{"NhapTieuLe|TrongLuong","NhapTieuSi|TrongLuongHang"},
            new[]{"XuatCaPhe"}, new[]{"XuatTieu"}),
    };

    private readonly IConfiguration _cfg;
    private readonly IMemoryCache _cache;
    public WarehouseService(IConfiguration cfg, IMemoryCache cache) { _cfg = cfg; _cache = cache; }

    public async Task<WarehouseOverviewViewModel> GetOverviewAsync()
    {
        if (_cache.TryGetValue("wh_ov", out WarehouseOverviewViewModel? c) && c != null) return c;
        var vm = await BuildAsync();
        _cache.Set("wh_ov", vm, TimeSpan.FromMinutes(3));
        return vm;
    }

    private async Task<WarehouseOverviewViewModel> BuildAsync()
    {
        var today = VietnamTime.Today;
        var ms = new DateTime(today.Year, today.Month, 1);

        var tasks = WHs.Select(async wh =>
        {
            var cs = _cfg.GetConnectionString(wh.ConnKey)!;
            var card = new WarehouseCard { Name = wh.Name };
            var qualRows = new List<WarehouseQualityRow>();

            try
            {
                await using var cn = new SqlConnection(cs); await cn.OpenAsync();

                // Coffee imports: SUM(TrongLuong), SUM(TrongLuongTruBi), SUM(QuyChuan)
                foreach (var t in wh.CoffeeImport)
                {
                    var p = t.Split('|');
                    var s = await QImport(cn, p[0], p[1], today, ms);
                    card.CoffeeTongTon += s.tl; card.CoffeeTruBi += s.tb; card.CoffeeQuyChuan += s.qc;
                    card.CoffeeMonthKg += s.monthKg; card.CoffeeMonthTien += s.monthTien;
                    card.CoffeeTodayKg += s.todayKg; card.CoffeeTodayTien += s.todayTien;
                    if (s.lastDate.HasValue && (!card.LastImportDate.HasValue || s.lastDate > card.LastImportDate)) card.LastImportDate = s.lastDate;
                }
                foreach (var t in wh.PepperImport)
                {
                    var p = t.Split('|');
                    var s = await QImport(cn, p[0], p[1], today, ms);
                    card.PepperTongTon += s.tl; card.PepperTruBi += s.tb; card.PepperQuyChuan += s.qc;
                    card.PepperMonthKg += s.monthKg; card.PepperMonthTien += s.monthTien;
                    card.PepperTodayKg += s.todayKg; card.PepperTodayTien += s.todayTien;
                    if (s.lastDate.HasValue && (!card.LastImportDate.HasValue || s.lastDate > card.LastImportDate)) card.LastImportDate = s.lastDate;
                }

                // Exports: subtract from stock
                foreach (var t in wh.CoffeeExport) { var (eTl, eTb, eQc, eMk, eTk, ld) = await QExport(cn, t, today, ms); card.CoffeeTongTon -= eTl; card.CoffeeTruBi -= eTb; card.CoffeeQuyChuan -= eQc; card.CoffeeExportKg += eMk; card.CoffeeExportTodayKg += eTk; if (ld.HasValue) card.LastExportDate = ld; }
                foreach (var t in wh.PepperExport) { var (eTl, eTb, eQc, eMk, eTk, ld) = await QExport(cn, t, today, ms); card.PepperTongTon -= eTl; card.PepperTruBi -= eTb; card.PepperQuyChuan -= eQc; card.PepperExportKg += eMk; card.PepperExportTodayKg += eTk; if (ld.HasValue) card.LastExportDate = ld; }

                // DVD extra: chi table (external debt payments added to coffee cost)
                if (wh.Name == "Dương Văn Dũng")
                {
                    try
                    {
                        await using var chiCmd = cn.CreateCommand();
                        chiCmd.CommandText = $"SELECT ISNULL(SUM(CAST(ThanhTien AS bigint)),0), ISNULL(SUM(CASE WHEN ngayChi>=@ms THEN CAST(ThanhTien AS bigint) ELSE 0 END),0), ISNULL(SUM(CASE WHEN ngayChi>=@td AND ngayChi<@tmr THEN CAST(ThanhTien AS bigint) ELSE 0 END),0) FROM chi WITH(NOLOCK)";
                        chiCmd.CommandTimeout = 10;
                        chiCmd.Parameters.AddWithValue("@ms", ms); chiCmd.Parameters.AddWithValue("@td", today); chiCmd.Parameters.AddWithValue("@tmr", today.AddDays(1));
                        await using var chiR = await chiCmd.ExecuteReaderAsync();
                        if (await chiR.ReadAsync())
                        {
                            card.CoffeeMonthTien += chiR.GetInt64(1);
                            card.CoffeeTodayTien += chiR.GetInt64(2);
                        }
                    }
                    catch { }
                }

                card.Status = (card.CoffeeMonthKg + card.PepperMonthKg) > 100000 ? "active" : (card.CoffeeMonthKg + card.PepperMonthKg) > 10000 ? "normal" : "quiet";
                card.StatusLabel = card.Status switch { "active" => "Hoạt động mạnh", "normal" => "Bình thường", _ => "Ít hoạt động" };
            }
            catch { card.Status = "quiet"; card.StatusLabel = "Lỗi kết nối"; }
            return card;
        }).ToList();

        var cards = (await Task.WhenAll(tasks)).ToList();

        // Hourly activity (all warehouses combined, current month)
        var hourly = new int[24];
        foreach (var wh in WHs)
        {
            try
            {
                var cs = _cfg.GetConnectionString(wh.ConnKey)!;
                await using var cn = new SqlConnection(cs); await cn.OpenAsync();
                var tables = wh.CoffeeImport.Concat(wh.PepperImport).Select(t => t.Split('|')[0]);
                foreach (var tbl in tables)
                {
                    await using var cmd = cn.CreateCommand();
                    cmd.CommandText = $"SELECT DATEPART(HOUR,ngayNhap),COUNT(*) FROM {tbl} WITH(NOLOCK) WHERE ngayNhap>=@ms GROUP BY DATEPART(HOUR,ngayNhap)";
                    cmd.CommandTimeout = 10; cmd.Parameters.AddWithValue("@ms", ms);
                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync()) hourly[r.GetInt32(0)] += r.GetInt32(1);
                }
            }
            catch { }
        }

        return new WarehouseOverviewViewModel
        {
            TotalWarehouses = cards.Count,
            CoffeeTongTon  = cards.Sum(c => c.CoffeeTongTon),
            CoffeeTruBi    = cards.Sum(c => c.CoffeeTruBi),
            CoffeeQuyChuan = cards.Sum(c => c.CoffeeQuyChuan),
            CoffeeMonthKg  = cards.Sum(c => c.CoffeeMonthKg),
            PepperTongTon  = cards.Sum(c => c.PepperTongTon),
            PepperTruBi    = cards.Sum(c => c.PepperTruBi),
            PepperQuyChuan = cards.Sum(c => c.PepperQuyChuan),
            PepperMonthKg  = cards.Sum(c => c.PepperMonthKg),
            Warehouses = cards,
            HourlyActivity = Enumerable.Range(7, 17).Select(h => new HourlyActivity { Hour = h, Count = h < 24 ? hourly[h] : 0 }).ToList(),
            QualityComparison = Array.Empty<WarehouseQualityRow>(), // TODO: add later
        };
    }

    // ── Trend API (switchable period) ────────────────────────────────────────
    public async Task<IReadOnlyList<TrendPoint>> GetTrendAsync(string product, string period)
    {
        var cacheKey = $"wh_trend_{product}_{period}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<TrendPoint>? cached) && cached != null) return cached;

        var (fromDate, groupBy) = period switch
        {
            "1m"  => (VietnamTime.Now.AddDays(-30), "day"),
            "3m"  => (VietnamTime.Now.AddMonths(-3), "month"),
            "6m"  => (VietnamTime.Now.AddMonths(-6), "month"),
            "1y"  => (VietnamTime.Now.AddYears(-1), "month"),
            "2y"  => (VietnamTime.Now.AddYears(-2), "month"),
            "5y"  => (VietnamTime.Now.AddYears(-5), "month"),
            _     => (VietnamTime.Now.AddMonths(-6), "month"),
        };

        var isCoffee = product == "coffee";
        var result = new List<TrendPoint>();

        foreach (var wh in WHs)
        {
            var tables = isCoffee ? wh.CoffeeImport : wh.PepperImport;
            var cs = _cfg.GetConnectionString(wh.ConnKey)!;
            try
            {
                await using var cn = new SqlConnection(cs); await cn.OpenAsync();
                foreach (var t in tables)
                {
                    var tbl = t.Split('|')[0];
                    var groupExpr = groupBy == "day"
                        ? "CAST(ngayNhap AS date)"
                        : "DATEFROMPARTS(YEAR(ngayNhap),MONTH(ngayNhap),1)";
                    // Label: human readable. SortKey: yyyy-MM-dd for correct chronological order
                    var labelExpr = groupBy == "day"
                        ? "FORMAT(CAST(ngayNhap AS date),'dd/MM')"
                        : "FORMAT(DATEFROMPARTS(YEAR(ngayNhap),MONTH(ngayNhap),1),'MM/yyyy')";
                    var sortExpr = $"FORMAT({groupExpr},'yyyy-MM-dd')";

                    var wCol = t.Split('|')[1]; // TrongLuong or TrongLuongHang
                    await using var cmd = cn.CreateCommand();
                    cmd.CommandText = $@"SELECT {labelExpr},{sortExpr},ISNULL(SUM({wCol}),0)
                        FROM {tbl} WITH(NOLOCK) WHERE ngayNhap>=@f
                        GROUP BY {groupExpr},{labelExpr},{sortExpr} ORDER BY {sortExpr}";
                    cmd.CommandTimeout = 15;
                    cmd.Parameters.AddWithValue("@f", fromDate);
                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        var label = r.GetString(0);
                        var sortKey = r.GetString(1);
                        var existing = result.FirstOrDefault(p => p.SortKey == sortKey && p.Warehouse == wh.Name);
                        if (existing == null) { existing = new TrendPoint { Label = label, SortKey = sortKey, Warehouse = wh.Name }; result.Add(existing); }
                        existing.Kg += r.GetDecimal(2);
                    }
                }
            }
            catch { }
        }

        var sorted = result.OrderBy(p => p.SortKey).ToList();
        _cache.Set(cacheKey, (IReadOnlyList<TrendPoint>)sorted, TimeSpan.FromMinutes(5));
        return sorted;
    }

    // ── Inventory trend: SUM nhập tới tháng N - SUM xuất tới tháng N ────────
    // Tồn tháng 1 = SUM(tất cả nhập đến hết tháng 1) - SUM(tất cả xuất đến hết tháng 1)
    // Tồn tháng 2 = SUM(tất cả nhập đến hết tháng 2) - SUM(tất cả xuất đến hết tháng 2)
    public async Task<IReadOnlyList<TrendPoint>> GetInventoryTrendAsync(string product, string period)
    {
        var cacheKey = $"wh_inv_{product}_{period}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<TrendPoint>? cached) && cached != null) return cached;

        // Display range (which months to show on chart)
        var displayFrom = period switch
        {
            "1m" => VietnamTime.Now.AddMonths(-1), "3m" => VietnamTime.Now.AddMonths(-3),
            "6m" => VietnamTime.Now.AddMonths(-6), "1y" => VietnamTime.Now.AddYears(-1),
            "2y" => VietnamTime.Now.AddYears(-2), "5y" => VietnamTime.Now.AddYears(-5),
            _ => VietnamTime.Now.AddMonths(-6),
        };
        var displayFromKey = $"{displayFrom.Year:0000}-{displayFrom.Month:00}-01";

        var isCoffee = product == "coffee";

        // Query ALL monthly totals (from beginning of time) per warehouse
        // So running total is correct
        var monthlyNet = new Dictionary<(string wh, string sortKey, string label), decimal>();

        foreach (var wh in WHs)
        {
            var cs = _cfg.GetConnectionString(wh.ConnKey)!;
            try
            {
                await using var cn = new SqlConnection(cs); await cn.OpenAsync();

                // Monthly imports (all time) — use actual weight, not QuyChuan
                foreach (var t in (isCoffee ? wh.CoffeeImport : wh.PepperImport))
                {
                    var tbl = t.Split('|')[0];
                    var wCol = t.Split('|')[1]; // TrongLuong or TrongLuongHang
                    await using var cmd = cn.CreateCommand();
                    cmd.CommandText = $@"SELECT FORMAT(DATEFROMPARTS(YEAR(ngayNhap),MONTH(ngayNhap),1),'yyyy-MM-dd'),
                        FORMAT(DATEFROMPARTS(YEAR(ngayNhap),MONTH(ngayNhap),1),'MM/yyyy'),
                        ISNULL(SUM({wCol}),0)
                        FROM {tbl} WITH(NOLOCK)
                        GROUP BY DATEFROMPARTS(YEAR(ngayNhap),MONTH(ngayNhap),1) ORDER BY 1";
                    cmd.CommandTimeout = 15;
                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        var key = (wh.Name, r.GetString(0), r.GetString(1));
                        monthlyNet[key] = monthlyNet.GetValueOrDefault(key) + r.GetDecimal(2);
                    }
                }

                // Monthly exports (subtract) — use TrongLuongHang
                foreach (var t in (isCoffee ? wh.CoffeeExport : wh.PepperExport))
                {
                    await using var cmd = cn.CreateCommand();
                    cmd.CommandText = $@"SELECT FORMAT(DATEFROMPARTS(YEAR(ngayXuat),MONTH(ngayXuat),1),'yyyy-MM-dd'),
                        FORMAT(DATEFROMPARTS(YEAR(ngayXuat),MONTH(ngayXuat),1),'MM/yyyy'),
                        ISNULL(SUM(TrongLuongHang),0)
                        FROM {t} WITH(NOLOCK)
                        GROUP BY DATEFROMPARTS(YEAR(ngayXuat),MONTH(ngayXuat),1) ORDER BY 1";
                    cmd.CommandTimeout = 15;
                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        var key = (wh.Name, r.GetString(0), r.GetString(1));
                        monthlyNet[key] = monthlyNet.GetValueOrDefault(key) - r.GetDecimal(2);
                    }
                }
            }
            catch { }
        }

        // Build running total per warehouse, only output months within display range
        var allMonths = monthlyNet.Keys.Select(k => k.sortKey).Distinct().OrderBy(s => s).ToList();
        var whNames = WHs.Select(w => w.Name).ToList();
        var result = new List<TrendPoint>();

        foreach (var wh in whNames)
        {
            decimal running = 0;
            foreach (var month in allMonths)
            {
                // Sum all entries for this warehouse+month
                var net = monthlyNet.Where(kv => kv.Key.wh == wh && kv.Key.sortKey == month).Sum(kv => kv.Value);
                running += net;

                // Only add to output if within display range
                if (string.Compare(month, displayFromKey, StringComparison.Ordinal) >= 0)
                {
                    var label = monthlyNet.Keys.FirstOrDefault(k => k.wh == wh && k.sortKey == month).label
                                ?? $"{month[5..7]}/{month[..4]}";
                    result.Add(new TrendPoint { Label = label, SortKey = month, Warehouse = wh, Kg = running });
                }
            }
        }

        var sorted = result.OrderBy(p => p.SortKey).ToList();
        _cache.Set(cacheKey, (IReadOnlyList<TrendPoint>)sorted, TimeSpan.FromMinutes(5));
        return sorted;
    }

    // ── SQL helpers ──────────────────────────────────────────────────────────
    private static async Task<(decimal tl, decimal tb, decimal qc, decimal monthKg, long monthTien, decimal todayKg, long todayTien, DateTime? lastDate)>
        QImport(SqlConnection cn, string table, string weightCol, DateTime today, DateTime ms)
    {
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $@"SELECT
            ISNULL(SUM({weightCol}),0), ISNULL(SUM(TrongLuongTruBi),0), ISNULL(SUM(QuyChuan),0),
            ISNULL(SUM(CASE WHEN ngayNhap>=@ms THEN {weightCol} ELSE 0 END),0),
            ISNULL(SUM(CASE WHEN ngayNhap>=@ms THEN CAST(ThanhTien AS bigint) ELSE 0 END),0),
            ISNULL(SUM(CASE WHEN ngayNhap>=@td AND ngayNhap<@tmr THEN {weightCol} ELSE 0 END),0),
            ISNULL(SUM(CASE WHEN ngayNhap>=@td AND ngayNhap<@tmr THEN CAST(ThanhTien AS bigint) ELSE 0 END),0),
            MAX(ngayNhap)
            FROM {table} WITH(NOLOCK)";
        cmd.CommandTimeout = 15;
        cmd.Parameters.AddWithValue("@ms", ms);
        cmd.Parameters.AddWithValue("@td", today);
        cmd.Parameters.AddWithValue("@tmr", today.AddDays(1));
        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
            return (r.GetDecimal(0), r.GetDecimal(1), r.GetDecimal(2), r.GetDecimal(3), r.GetInt64(4), r.GetDecimal(5), r.GetInt64(6), r.IsDBNull(7) ? null : r.GetDateTime(7));
        return default;
    }

    private static async Task<(decimal tl, decimal tb, decimal qc, decimal monthKg, decimal todayKg, DateTime? lastDate)>
        QExport(SqlConnection cn, string table, DateTime today, DateTime ms)
    {
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $@"SELECT
            ISNULL(SUM(TrongLuongHang),0), ISNULL(SUM(TrongLuongTruBi),0), ISNULL(SUM(QuyChuan),0),
            ISNULL(SUM(CASE WHEN ngayXuat>=@ms THEN TrongLuongHang ELSE 0 END),0),
            ISNULL(SUM(CASE WHEN ngayXuat>=@td AND ngayXuat<@tmr THEN TrongLuongHang ELSE 0 END),0),
            MAX(ngayXuat)
            FROM {table} WITH(NOLOCK)";
        cmd.CommandTimeout = 15;
        cmd.Parameters.AddWithValue("@ms", ms);
        cmd.Parameters.AddWithValue("@td", today);
        cmd.Parameters.AddWithValue("@tmr", today.AddDays(1));
        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
            return (r.GetDecimal(0), r.GetDecimal(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4), r.IsDBNull(5) ? null : r.GetDateTime(5));
        return default;
    }
}
