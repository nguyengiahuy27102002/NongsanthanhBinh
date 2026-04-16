using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using DuongVanDung.WebApp.Models.Debt;

namespace DuongVanDung.WebApp.Services.Debt;

public interface IDebtService
{
    Task<DebtViewModel> GetDebtOverviewAsync(DebtFilter filter);
    Task<IReadOnlyList<DebtTransaction>> GetCustomerDetailAsync(string phone, string product);
}

public sealed class DebtService : IDebtService
{
    // ── Debt tables ──────────────────────────────────────────────────────────
    private sealed record DebtTable(string SqlTable, string Product, string Branch, bool HasDem);

    private static readonly DebtTable[] MainDbDebt =
    {
        new("noCaPhe", "Cà phê", "Dương Văn Dũng", false),
        new("noTieu",  "Tiêu",   "Dương Văn Dũng", true),
    };
    private static readonly DebtTable[] YmoalDbDebt =
    {
        new("noCaPhe", "Cà phê", "Ymoal", false),
        new("noTieu",  "Tiêu",   "Ymoal", true),
    };

    // ── Import tables for customer activity summary ──────────────────────────
    private sealed record ImportTable(string SqlTable, string Branch, string Product, string WeightCol);

    private static readonly ImportTable[] MainDbImports =
    {
        new("duongvandung",      "Dương Văn Dũng", "Cà phê", "TrongLuong"),
        new("nhapCaPheSi",       "Dương Văn Dũng", "Cà phê", "TrongLuongHang"),
        new("xntTieu",           "Dương Văn Dũng", "Tiêu",   "TrongLuong"),
        new("nhapTieuKhachSi",   "Dương Văn Dũng", "Tiêu",   "TrongLuongHang"),
        new("NhapCaPheThongDao", "Thông Đào",      "Cà phê", "TrongLuongHang"),
        new("NhapTieuThongDao",  "Thông Đào",      "Tiêu",   "TrongLuongHang"),
    };
    private static readonly ImportTable[] YmoalDbImports =
    {
        new("NhapCaPheLe",  "Ymoal", "Cà phê", "TrongLuong"),
        new("NhapCaPheSi",  "Ymoal", "Cà phê", "TrongLuongHang"),
        new("NhapTieuLe",   "Ymoal", "Tiêu",   "TrongLuong"),
        new("NhapTieuSi",   "Ymoal", "Tiêu",   "TrongLuongHang"),
    };

    private readonly string _connMain;
    private readonly string _connYmoal;
    private readonly IMemoryCache _cache;

    public DebtService(IConfiguration cfg, IMemoryCache cache)
    {
        _connMain  = cfg.GetConnectionString("DefaultConnection")!;
        _connYmoal = cfg.GetConnectionString("KhoYmoal")!;
        _cache     = cache;
    }

    public async Task<DebtViewModel> GetDebtOverviewAsync(DebtFilter filter)
    {
        var cacheKey = $"debt_{filter.Product}_{filter.Search}_{filter.Sort}_{filter.Page}";
        if (_cache.TryGetValue(cacheKey, out DebtViewModel? cached) && cached != null)
            return cached;

        var result = await BuildDebtViewAsync(filter);
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(3));
        return result;
    }

    private async Task<DebtViewModel> BuildDebtViewAsync(DebtFilter filter)
    {
        // Fetch debt records + customer import summaries in parallel
        var t1 = FetchDebtRowsAsync(_connMain,  MainDbDebt);
        var t2 = FetchDebtRowsAsync(_connYmoal, YmoalDbDebt);
        var t3 = FetchCustomerSummaryAsync(_connMain,  MainDbImports);
        var t4 = FetchCustomerSummaryAsync(_connYmoal, YmoalDbImports);
        await Task.WhenAll(t1, t2, t3, t4);

        var allDebts = new List<DebtTransaction>();
        allDebts.AddRange(t1.Result); allDebts.AddRange(t2.Result);

        var allActivity = new List<CustomerActivity>();
        allActivity.AddRange(t3.Result); allActivity.AddRange(t4.Result);

        // Build customer summary: merge debt records + import activity
        var customerMap = new Dictionary<string, CustomerDebtSummary>(StringComparer.OrdinalIgnoreCase);

        // 1) From debt tables
        foreach (var d in allDebts)
        {
            var key = NormalizeKey(d.CustomerName, d.Phone);
            if (!customerMap.TryGetValue(key, out var c))
            {
                c = new CustomerDebtSummary { Name = d.CustomerName, Phone = d.Phone, Address = d.Address };
                customerMap[key] = c;
            }
            if (d.Product == "Cà phê") c.CoffeeInStore += d.StandardQty;
            else c.PepperInStore += d.StandardQty;
            c.DebtAmount += d.Remaining;
            c.PrepaidAmount += d.Prepaid;
            c.DebtRecordCount++;
            if (!c.LastTransaction.HasValue || d.Date > c.LastTransaction) c.LastTransaction = d.Date;
        }

        // 2) Enrich with import activity
        foreach (var a in allActivity)
        {
            var key = NormalizeKey(a.Name, a.Phone);
            if (!customerMap.TryGetValue(key, out var c))
            {
                c = new CustomerDebtSummary { Name = a.Name, Phone = a.Phone, Address = a.Address };
                customerMap[key] = c;
            }
            c.TotalTransactions += a.Transactions;
            c.TotalImported += a.TotalQty;
            c.TotalPaid += a.TotalAmount;
            if (string.IsNullOrEmpty(c.Address) && !string.IsNullOrEmpty(a.Address)) c.Address = a.Address;
            if (!c.LastTransaction.HasValue || a.LastDate > c.LastTransaction) c.LastTransaction = a.LastDate;
        }

        var allCustomers = customerMap.Values.ToList();

        // Apply filters
        var filtered = allCustomers.AsEnumerable();
        if (!string.IsNullOrEmpty(filter.Search))
        {
            var s = filter.Search.ToLowerInvariant();
            filtered = filtered.Where(c =>
                c.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.Phone.Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        if (filter.Product == "coffee")
            filtered = filtered.Where(c => c.CoffeeInStore > 0 || c.DebtRecordCount > 0);
        else if (filter.Product == "pepper")
            filtered = filtered.Where(c => c.PepperInStore > 0 || c.DebtRecordCount > 0);

        // Sort
        filtered = filter.Sort switch
        {
            "volume" => filtered.OrderByDescending(c => c.TotalInStore),
            "name"   => filtered.OrderBy(c => c.Name),
            "recent" => filtered.OrderByDescending(c => c.LastTransaction),
            _        => filtered.OrderByDescending(c => Math.Abs(c.DebtAmount)).ThenByDescending(c => c.TotalInStore),
        };

        var total = filtered.Count();
        var page = filtered.Skip((filter.Page - 1) * DebtFilter.PageSize).Take(DebtFilter.PageSize).ToList();

        // KPIs
        var withGoods = allCustomers.Where(c => c.TotalInStore > 0).ToList();
        var withDebt = allCustomers.Where(c => c.DebtAmount != 0).ToList();

        return new DebtViewModel
        {
            Filter = filter,
            Kpi = new DebtKpi
            {
                TotalCustomers     = allCustomers.Count(c => c.TotalInStore > 0 || c.DebtAmount != 0),
                CustomersWithGoods = withGoods.Count,
                CustomersWithDebt  = withDebt.Count,
                TotalCoffeeKg      = allCustomers.Sum(c => c.CoffeeInStore),
                TotalPepperKg      = allCustomers.Sum(c => c.PepperInStore),
                TotalDebtAmount    = allCustomers.Sum(c => c.DebtAmount),
                TotalPrepaid       = allCustomers.Sum(c => c.PrepaidAmount),
            },
            Customers = page,
            TotalCustomers = total,
            AvailableBranches = new[] { "Dương Văn Dũng", "Thông Đào", "Ymoal" },
        };
    }

    public async Task<IReadOnlyList<DebtTransaction>> GetCustomerDetailAsync(string phone, string product)
    {
        var all = new List<DebtTransaction>();
        all.AddRange(await FetchDebtRowsAsync(_connMain, MainDbDebt));
        all.AddRange(await FetchDebtRowsAsync(_connYmoal, YmoalDbDebt));

        return all.Where(d =>
            d.Phone.Replace(" ", "") == phone.Replace(" ", "") &&
            (string.IsNullOrEmpty(product) || d.Product == product))
            .OrderByDescending(d => d.Date)
            .ToList();
    }

    // ── Fetch from noCaPhe / noTieu ──────────────────────────────────────────
    private async Task<List<DebtTransaction>> FetchDebtRowsAsync(string connStr, DebtTable[] tables)
    {
        var rows = new List<DebtTransaction>();
        try
        {
            await using var cn = new SqlConnection(connStr);
            await cn.OpenAsync();

            foreach (var t in tables)
            {
                var dem = t.HasDem ? "ISNULL(Dem,0)" : "CAST(NULL AS decimal(18,2))";
                var price = t.HasDem ? "COALESCE(NULLIF(GiaMoi,0),GiaCaPhe,0)" : "ISNULL(GiaCaPhe,0)";

                var sql = $@"SELECT debtID, ngayNo, ISNULL(TenKhachHang,N''), ISNULL(Sdt,''),
                    ISNULL(DiaChi,N''), ISNULL(TrongLuongHang,0), ISNULL(QuyChuan,0),
                    ISNULL(DoAm,0), ISNULL(TapChat,0), {dem},
                    {price}, ISNULL(ThanhTien,0), ISNULL(TraTruoc,0), ISNULL(ConNo,0),
                    ngayThanhToan, ISNULL(note,N'')
                    FROM {t.SqlTable}";

                await using var cmd = cn.CreateCommand();
                cmd.CommandText = sql; cmd.CommandTimeout = 15;
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    rows.Add(new DebtTransaction
                    {
                        DebtId       = r.GetInt32(0),
                        Date         = r.IsDBNull(1) ? DateTime.MinValue : r.GetDateTime(1),
                        CustomerName = r.GetString(2).Trim(),
                        Phone        = r.IsDBNull(3) ? "" : r.GetString(3).Trim(),
                        Address      = r.GetString(4).Trim(),
                        Weight       = r.GetDecimal(5),
                        StandardQty  = r.GetDecimal(6),
                        Moisture     = r.GetDecimal(7),
                        Impurity     = r.GetDecimal(8),
                        Dem          = r.IsDBNull(9) ? null : r.GetDecimal(9),
                        Price        = r.GetInt32(10),
                        Amount       = r.GetInt32(11),
                        Prepaid      = r.GetInt32(12),
                        Remaining    = r.GetInt32(13),
                        PaymentDate  = r.IsDBNull(14) ? null : r.GetDateTime(14),
                        Note         = r.GetString(15).Trim(),
                        Product      = t.Product,
                        Branch       = t.Branch,
                    });
                }
            }
        }
        catch { }
        return rows;
    }

    // ── Customer activity from import tables ─────────────────────────────────
    private sealed class CustomerActivity
    {
        public string Name = "", Phone = "", Address = "";
        public int Transactions;
        public decimal TotalQty;
        public long TotalAmount;
        public DateTime LastDate;
    }

    private async Task<List<CustomerActivity>> FetchCustomerSummaryAsync(string connStr, ImportTable[] tables)
    {
        var result = new List<CustomerActivity>();
        var parts = new List<string>();

        foreach (var t in tables)
        {
            parts.Add($@"SELECT ISNULL(TenKhachHang,N''), ISNULL(CAST(Sdt AS nvarchar(30)),''),
                ISNULL(DiaChi,N''), ISNULL(QuyChuan,0), CAST(ISNULL(ThanhTien,0) AS bigint), ngayNhap
                FROM {t.SqlTable}");
        }

        if (parts.Count == 0) return result;

        try
        {
            await using var cn = new SqlConnection(connStr);
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $@"SELECT Name, Phone, MAX(Addr) AS Addr,
                COUNT(*) AS Txn, SUM(Qty) AS TotalQty, SUM(Amt) AS TotalAmt, MAX(Dt) AS LastDt
                FROM (
                    {string.Join("\nUNION ALL\n", parts.Select(p => $"SELECT x.* FROM ({p}) AS x(Name,Phone,Addr,Qty,Amt,Dt)"))}
                ) AS Raw
                WHERE Name <> N'' AND Name IS NOT NULL
                GROUP BY Name, Phone";
            cmd.CommandTimeout = 30;

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new CustomerActivity
                {
                    Name = r.GetString(0).Trim(),
                    Phone = r.IsDBNull(1) ? "" : r.GetString(1).Trim(),
                    Address = r.IsDBNull(2) ? "" : r.GetString(2).Trim(),
                    Transactions = r.GetInt32(3),
                    TotalQty = r.GetDecimal(4),
                    TotalAmount = r.GetInt64(5),
                    LastDate = r.GetDateTime(6),
                });
            }
        }
        catch { }
        return result;
    }

    private static string NormalizeKey(string name, string phone)
    {
        var n = name.Trim().ToLowerInvariant();
        var p = phone.Replace(" ", "").Replace("-", "");
        return string.IsNullOrEmpty(p) ? n : $"{n}|{p}";
    }
}
