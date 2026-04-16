using DuongVanDung.WebApp.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using DuongVanDung.WebApp.Models.Auth;

namespace DuongVanDung.WebApp.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardData> GetDashboardDataAsync();
}

// ── Internal data structures ─────────────────────────────────────────────────
public sealed class WarehouseStats
{
    public string Name = "";
    public decimal CoffeeTotalKg, CoffeeTotalQc, CoffeeTodayKg, CoffeeExportKg;
    public long    CoffeeTotalTien, CoffeeTodayTien, CoffeeCK, CoffeeTM;
    public decimal PepperTotalKg, PepperTotalQc, PepperTodayKg, PepperExportKg;
    public long    PepperTotalTien, PepperTodayTien, PepperCK, PepperTM;
}

public sealed class DailyPoint { public DateTime Date; public double CoffeeKg, PepperKg; }

public sealed class DashboardData
{
    public List<WarehouseStats> Warehouses = new();
    public int CoffePrice, PepperPrice;
    public List<DailyPoint>[] DailyByWarehouse = Array.Empty<List<DailyPoint>>();
}

public sealed class DashboardService : IDashboardService
{
    // ── Table definitions per warehouse ──────────────────────────────────────
    private sealed record WH(string Name, string ConnKey, bool IsYmoal,
        string[] CoffeeImport, string[] PepperImport,
        string[] CoffeeExport, string[] PepperExport);

    private static readonly WH[] Warehouses =
    {
        new("Dương Văn Dũng", "DefaultConnection", false,
            new[] { "duongvandung|TrongLuong", "nhapCaPheSi|TrongLuongHang" },
            new[] { "xntTieu|TrongLuong", "nhapTieuKhachSi|TrongLuongHang" },
            new[] { "xuatCaPhe" }, new[] { "xuatTieu" }),
        new("Thông Đào", "DefaultConnection", false,
            new[] { "NhapCaPheThongDao|TrongLuongHang" },
            new[] { "NhapTieuThongDao|TrongLuongHang" },
            new[] { "XuatCaPheThongDao" }, new[] { "XuatTieuThongDao" }),
        new("Ymoal", "KhoYmoal", true,
            new[] { "NhapCaPheLe|TrongLuong", "NhapCaPheSi|TrongLuongHang" },
            new[] { "NhapTieuLe|TrongLuong", "NhapTieuSi|TrongLuongHang" },
            new[] { "XuatCaPhe" }, new[] { "XuatTieu" }),
    };

    private readonly IConfiguration _cfg;
    private readonly IMemoryCache _cache;

    public DashboardService(IConfiguration cfg, IMemoryCache cache)
    {
        _cfg = cfg; _cache = cache;
    }

    public async Task<DashboardData> GetDashboardDataAsync()
    {
        if (_cache.TryGetValue("dashboard_data", out DashboardData? cached) && cached != null)
            return cached;

        var data = new DashboardData();
        var today = VietnamTime.Today;
        var day7ago = today.AddDays(-6);

        var tasks = Warehouses.Select(async wh =>
        {
            var connStr = _cfg.GetConnectionString(wh.ConnKey)!;
            var stats = new WarehouseStats { Name = wh.Name };
            var daily = new List<DailyPoint>();

            try
            {
                await using var cn = new SqlConnection(connStr);
                await cn.OpenAsync();

                // Coffee imports: total + today
                foreach (var tbl in wh.CoffeeImport)
                {
                    var parts = tbl.Split('|');
                    var table = parts[0]; var weightCol = parts[1];
                    var (totalKg, totalQc, totalTien, todayKg, todayTien, ck, tm) = await QueryImport(cn, table, weightCol, today);
                    stats.CoffeeTotalKg += totalKg; stats.CoffeeTotalQc += totalQc;
                    stats.CoffeeTotalTien += totalTien; stats.CoffeeTodayKg += todayKg;
                    stats.CoffeeTodayTien += todayTien; stats.CoffeeCK += ck; stats.CoffeeTM += tm;
                }
                // Pepper imports
                foreach (var tbl in wh.PepperImport)
                {
                    var parts = tbl.Split('|');
                    var (totalKg, totalQc, totalTien, todayKg, todayTien, ck, tm) = await QueryImport(cn, parts[0], parts[1], today);
                    stats.PepperTotalKg += totalKg; stats.PepperTotalQc += totalQc;
                    stats.PepperTotalTien += totalTien; stats.PepperTodayKg += todayKg;
                    stats.PepperTodayTien += todayTien; stats.PepperCK += ck; stats.PepperTM += tm;
                }
                // Coffee exports
                foreach (var tbl in wh.CoffeeExport)
                    stats.CoffeeExportKg += await QueryExportTotal(cn, tbl);
                // Pepper exports
                foreach (var tbl in wh.PepperExport)
                    stats.PepperExportKg += await QueryExportTotal(cn, tbl);

                // DVD extra: chi table (external debt payments)
                if (wh.Name == "Dương Văn Dũng" && !wh.IsYmoal)
                {
                    try
                    {
                        await using var chiCmd = cn.CreateCommand();
                        chiCmd.CommandText = "SELECT ISNULL(SUM(CAST(ThanhTien AS bigint)),0) FROM chi WITH(NOLOCK)";
                        chiCmd.CommandTimeout = 10;
                        var chiTotal = (long)(await chiCmd.ExecuteScalarAsync() ?? 0L);
                        stats.CoffeeTotalTien += chiTotal; // chi is mainly coffee debt payments
                    }
                    catch { }
                }

                // Daily 7 days for chart
                daily = await QueryDaily7(cn, wh, day7ago, today);
            }
            catch { }

            return (stats, daily);
        }).ToList();

        var results = await Task.WhenAll(tasks);
        foreach (var (stats, daily) in results)
        {
            data.Warehouses.Add(stats);
        }
        data.DailyByWarehouse = results.Select(r => r.daily).ToArray();

        // Prices from Gia table
        try
        {
            var mainConn = _cfg.GetConnectionString("DefaultConnection")!;
            await using var cn = new SqlConnection(mainConn);
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT TOP 1 caPhe, tieu FROM Gia ORDER BY oderID DESC";
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                data.CoffePrice = r.GetInt32(0);
                data.PepperPrice = r.GetInt32(1);
            }
        }
        catch { }

        _cache.Set("dashboard_data", data, TimeSpan.FromMinutes(3));
        return data;
    }

    // ── Query helpers ────────────────────────────────────────────────────────
    private static async Task<(decimal totalKg, decimal totalQc, long totalTien, decimal todayKg, long todayTien, long ck, long tm)>
        QueryImport(SqlConnection cn, string table, string weightCol, DateTime today)
    {
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $@"
            SELECT
                ISNULL(SUM({weightCol}),0),
                ISNULL(SUM(QuyChuan),0),
                ISNULL(SUM(CAST(ThanhTien AS bigint)),0),
                ISNULL(SUM(CASE WHEN ngayNhap >= @today AND ngayNhap < @tomorrow THEN {weightCol} ELSE 0 END),0),
                ISNULL(SUM(CASE WHEN ngayNhap >= @today AND ngayNhap < @tomorrow THEN CAST(ThanhTien AS bigint) ELSE 0 END),0),
                ISNULL(SUM(CASE WHEN ChuyenKhoan = 'Yes' OR ChuyenKhoan = 'yes' THEN CAST(ThanhTien AS bigint) ELSE 0 END),0),
                ISNULL(SUM(CASE WHEN ChuyenKhoan <> 'Yes' AND ChuyenKhoan <> 'yes' THEN CAST(ThanhTien AS bigint) ELSE 0 END),0)
            FROM {table} WITH(NOLOCK)";
        cmd.CommandTimeout = 15;
        cmd.Parameters.AddWithValue("@today", today);
        cmd.Parameters.AddWithValue("@tomorrow", today.AddDays(1));
        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
            return (r.GetDecimal(0), r.GetDecimal(1), r.GetInt64(2), r.GetDecimal(3), r.GetInt64(4), r.GetInt64(5), r.GetInt64(6));
        return default;
    }

    private static async Task<decimal> QueryExportTotal(SqlConnection cn, string table)
    {
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT ISNULL(SUM(TrongLuongHang),0) FROM {table} WITH(NOLOCK)";
        cmd.CommandTimeout = 15;
        var result = await cmd.ExecuteScalarAsync();
        return result is decimal d ? d : 0;
    }

    private static async Task<List<DailyPoint>> QueryDaily7(SqlConnection cn, WH wh, DateTime from, DateTime to)
    {
        var dict = new Dictionary<DateTime, DailyPoint>();
        for (var d = from; d <= to; d = d.AddDays(1))
            dict[d] = new DailyPoint { Date = d };

        // Coffee daily — use actual weight column
        foreach (var tbl in wh.CoffeeImport)
        {
            var parts = tbl.Split('|');
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT CAST(ngayNhap AS date), ISNULL(SUM({parts[1]}),0) FROM {parts[0]} WITH(NOLOCK) WHERE ngayNhap >= @f AND ngayNhap < @t GROUP BY CAST(ngayNhap AS date)";
            cmd.CommandTimeout = 10;
            cmd.Parameters.AddWithValue("@f", from);
            cmd.Parameters.AddWithValue("@t", to.AddDays(1));
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var date = r.GetDateTime(0).Date;
                if (dict.TryGetValue(date, out var p)) p.CoffeeKg += (double)r.GetDecimal(1);
            }
        }
        // Pepper daily — use actual weight column
        foreach (var tbl in wh.PepperImport)
        {
            var parts = tbl.Split('|');
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT CAST(ngayNhap AS date), ISNULL(SUM({parts[1]}),0) FROM {parts[0]} WITH(NOLOCK) WHERE ngayNhap >= @f AND ngayNhap < @t GROUP BY CAST(ngayNhap AS date)";
            cmd.CommandTimeout = 10;
            cmd.Parameters.AddWithValue("@f", from);
            cmd.Parameters.AddWithValue("@t", to.AddDays(1));
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var date = r.GetDateTime(0).Date;
                if (dict.TryGetValue(date, out var p)) p.PepperKg += (double)r.GetDecimal(1);
            }
        }
        return dict.Values.OrderBy(d => d.Date).ToList();
    }
}
