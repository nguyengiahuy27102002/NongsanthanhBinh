using Microsoft.Data.SqlClient;
using DuongVanDung.WebApp.Models.Product;

namespace DuongVanDung.WebApp.Services.Product;

public interface IProductService
{
    Task<TonKhoViewModel>       GetTonKhoAsync();
    Task<StockMovementViewModel> GetStockMovementAsync(int days = 30);
    Task<QualityViewModel>      GetQualityAsync();
}

public sealed class ProductService : IProductService
{
    private readonly string _connStr;
    private readonly string _connStrYmoal;

    public ProductService(IConfiguration cfg)
    {
        _connStr      = cfg.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException();
        _connStrYmoal = cfg.GetConnectionString("KhoYmoal")         ?? throw new InvalidOperationException();
    }

    private SqlConnection Open()      => new(_connStr);
    private SqlConnection OpenYmoal() => new(_connStrYmoal);

    // ─────────────────────────────────────────────────────────────────────────
    // TỒN KHO
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<TonKhoViewModel> GetTonKhoAsync()
    {
        var rows = new List<TonKhoRow>();
        var vm   = new TonKhoViewModel();

        await using (var cn = Open())
        {
            await cn.OpenAsync();

            // --- nguyengiahuy nhập ---
            var nhapSql = @"
                SELECT 'Dương Văn Dũng' AS Kho, N'Cà phê' AS SP, ISNULL(SUM(TrongLuong),0) FROM duongvandung
                UNION ALL
                SELECT 'Dương Văn Dũng', N'Cà phê', ISNULL(SUM(TrongLuongHang),0) FROM nhapCaPheSi
                UNION ALL
                SELECT 'Dương Văn Dũng', N'Tiêu',   ISNULL(SUM(TrongLuong),0) FROM xntTieu
                UNION ALL
                SELECT 'Dương Văn Dũng', N'Tiêu',   ISNULL(SUM(TrongLuongHang),0) FROM nhapTieuKhachSi
                UNION ALL
                SELECT N'Thông Đào',     N'Cà phê', ISNULL(SUM(TrongLuongHang),0) FROM NhapCaPheThongDao
                UNION ALL
                SELECT N'Thông Đào',     N'Tiêu',   ISNULL(SUM(TrongLuongHang),0) FROM NhapTieuThongDao";

            // DVD cà phê nhập
            var dvdCFNhap = 0m; var dvdTNhap = 0m;
            var tdCFNhap  = 0m; var tdTNhap  = 0m;

            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = nhapSql;
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var kho = r.GetString(0); var sp = r.GetString(1);
                    var kg  = r.GetDecimal(2);
                    if (kho.Contains("Dũng") && sp.Contains("phê")) dvdCFNhap += kg;
                    else if (kho.Contains("Dũng") && sp == "Tiêu")  dvdTNhap  += kg;
                    else if (kho.Contains("Đào")  && sp.Contains("phê")) tdCFNhap  += kg;
                    else if (kho.Contains("Đào")  && sp == "Tiêu")  tdTNhap   += kg;
                }
            }

            // --- nguyengiahuy xuất ---
            var xuatSql = @"
                SELECT 'Dương Văn Dũng', N'Cà phê', ISNULL(SUM(TrongLuongHang),0) FROM xuatCaPhe
                UNION ALL
                SELECT 'Dương Văn Dũng', N'Tiêu',   ISNULL(SUM(TrongLuongHang),0) FROM xuatTieu
                UNION ALL
                SELECT N'Thông Đào',     N'Cà phê', ISNULL(SUM(TrongLuongHang),0) FROM XuatCaPheThongDao
                UNION ALL
                SELECT N'Thông Đào',     N'Tiêu',   ISNULL(SUM(TrongLuongHang),0) FROM XuatTieuThongDao";

            var dvdCFXuat = 0m; var dvdTXuat = 0m;
            var tdCFXuat  = 0m; var tdTXuat  = 0m;

            try
            {
                await using var cmd = cn.CreateCommand();
                cmd.CommandText = xuatSql;
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var kho = r.GetString(0); var sp = r.GetString(1);
                    var kg  = r.GetDecimal(2);
                    if (kho.Contains("Dũng") && sp.Contains("phê")) dvdCFXuat += kg;
                    else if (kho.Contains("Dũng") && sp == "Tiêu")  dvdTXuat  += kg;
                    else if (kho.Contains("Đào")  && sp.Contains("phê")) tdCFXuat  += kg;
                    else if (kho.Contains("Đào")  && sp == "Tiêu")  tdTXuat   += kg;
                }
            }
            catch { /* xuất tables may not exist yet */ }

            rows.Add(new TonKhoRow { Kho = "Dương Văn Dũng", SanPham = "Cà phê", TongNhap = dvdCFNhap, TongXuat = dvdCFXuat });
            rows.Add(new TonKhoRow { Kho = "Dương Văn Dũng", SanPham = "Tiêu",   TongNhap = dvdTNhap,  TongXuat = dvdTXuat  });
            rows.Add(new TonKhoRow { Kho = "Thông Đào",      SanPham = "Cà phê", TongNhap = tdCFNhap,  TongXuat = tdCFXuat  });
            rows.Add(new TonKhoRow { Kho = "Thông Đào",      SanPham = "Tiêu",   TongNhap = tdTNhap,   TongXuat = tdTXuat   });
        }

        // --- KhoYmoal ---
        await using (var cny = OpenYmoal())
        {
            await cny.OpenAsync();
            var ymNhapSql = @"
                SELECT N'Cà phê', ISNULL(SUM(TrongLuong),0) FROM NhapCaPheLe
                UNION ALL SELECT N'Cà phê', ISNULL(SUM(TrongLuongHang),0) FROM NhapCaPheSi
                UNION ALL SELECT N'Tiêu',   ISNULL(SUM(TrongLuong),0) FROM NhapTieuLe
                UNION ALL SELECT N'Tiêu',   ISNULL(SUM(TrongLuongHang),0) FROM NhapTieuSi";

            var ymCFNhap = 0m; var ymTNhap = 0m;
            await using (var cmd = cny.CreateCommand())
            {
                cmd.CommandText = ymNhapSql;
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var sp = r.GetString(0); var kg = r.GetDecimal(1);
                    if (sp.Contains("phê")) ymCFNhap += kg; else ymTNhap += kg;
                }
            }

            var ymCFXuat = 0m; var ymTXuat = 0m;
            try
            {
                var ymXuatSql = @"
                    SELECT N'Cà phê', ISNULL(SUM(TrongLuongHang),0) FROM XuatCaPhe
                    UNION ALL SELECT N'Tiêu',   ISNULL(SUM(TrongLuongHang),0) FROM XuatTieu";
                await using var cmd = cny.CreateCommand();
                cmd.CommandText = ymXuatSql;
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var sp = r.GetString(0); var kg = r.GetDecimal(1);
                    if (sp.Contains("phê")) ymCFXuat += kg; else ymTXuat += kg;
                }
            }
            catch { }

            rows.Add(new TonKhoRow { Kho = "Ymoal", SanPham = "Cà phê", TongNhap = ymCFNhap, TongXuat = ymCFXuat });
            rows.Add(new TonKhoRow { Kho = "Ymoal", SanPham = "Tiêu",   TongNhap = ymTNhap,  TongXuat = ymTXuat  });
        }

        vm.Rows = rows;
        vm.TongNhapCaPhe = rows.Where(r => r.SanPham == "Cà phê").Sum(r => r.TongNhap);
        vm.TongXuatCaPhe = rows.Where(r => r.SanPham == "Cà phê").Sum(r => r.TongXuat);
        vm.TongNhapTieu  = rows.Where(r => r.SanPham == "Tiêu").Sum(r => r.TongNhap);
        vm.TongXuatTieu  = rows.Where(r => r.SanPham == "Tiêu").Sum(r => r.TongXuat);
        return vm;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NHẬP – XUẤT – TỒN (30 ngày gần nhất, group theo ngày + kho)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<StockMovementViewModel> GetStockMovementAsync(int days = 30)
    {
        var vm = new StockMovementViewModel { Days = days };
        var caphe = new List<MovementRow>();
        var tieu  = new List<MovementRow>();

        await using var cn = Open();
        await cn.OpenAsync();

        // Nhập cà phê theo ngày
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = $@"
                SELECT CAST(ngayNhap AS date) AS Ngay, 'DVD' AS Kho, SUM(ISNULL(TrongLuong,0)) AS Nhap
                FROM duongvandung
                WHERE ngayNhap >= DATEADD(DAY,-{days},GETDATE())
                GROUP BY CAST(ngayNhap AS date)
                UNION ALL
                SELECT CAST(ngayNhap AS date), 'DVD', SUM(ISNULL(TrongLuongHang,0))
                FROM nhapCaPheSi WHERE ngayNhap >= DATEADD(DAY,-{days},GETDATE())
                GROUP BY CAST(ngayNhap AS date)
                UNION ALL
                SELECT CAST(ngayNhap AS date), N'Thông Đào', SUM(ISNULL(TrongLuongHang,0))
                FROM NhapCaPheThongDao WHERE ngayNhap >= DATEADD(DAY,-{days},GETDATE())
                GROUP BY CAST(ngayNhap AS date)
                ORDER BY Ngay DESC";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                caphe.Add(new MovementRow { Ngay = r.GetDateTime(0), Kho = r.GetString(1), Nhap = r.GetDecimal(2) });
        }

        // Xuất cà phê theo ngày
        try
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $@"
                SELECT CAST(ngayXuat AS date) AS Ngay, 'DVD' AS Kho, SUM(ISNULL(TrongLuongHang,0))
                FROM xuatCaPhe WHERE ngayXuat >= DATEADD(DAY,-{days},GETDATE())
                GROUP BY CAST(ngayXuat AS date)
                UNION ALL
                SELECT CAST(ngayXuat AS date), N'Thông Đào', SUM(ISNULL(TrongLuongHang,0))
                FROM XuatCaPheThongDao WHERE ngayXuat >= DATEADD(DAY,-{days},GETDATE())
                GROUP BY CAST(ngayXuat AS date)
                ORDER BY Ngay DESC";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var ngay = r.GetDateTime(0); var kho = r.GetString(1); var xuat = r.GetDecimal(2);
                var existing = caphe.FirstOrDefault(x => x.Ngay == ngay && x.Kho == kho);
                if (existing != null) existing.Xuat += xuat;
                else caphe.Add(new MovementRow { Ngay = ngay, Kho = kho, Xuat = xuat });
            }
        }
        catch { }

        // Nhập tiêu theo ngày
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = $@"
                SELECT CAST(ngayNhap AS date), 'DVD', SUM(ISNULL(TrongLuong,0))
                FROM xntTieu WHERE ngayNhap >= DATEADD(DAY,-{days},GETDATE())
                GROUP BY CAST(ngayNhap AS date)
                UNION ALL
                SELECT CAST(ngayNhap AS date), 'DVD', SUM(ISNULL(TrongLuongHang,0))
                FROM nhapTieuKhachSi WHERE ngayNhap >= DATEADD(DAY,-{days},GETDATE())
                GROUP BY CAST(ngayNhap AS date)
                UNION ALL
                SELECT CAST(ngayNhap AS date), N'Thông Đào', SUM(ISNULL(TrongLuongHang,0))
                FROM NhapTieuThongDao WHERE ngayNhap >= DATEADD(DAY,-{days},GETDATE())
                GROUP BY CAST(ngayNhap AS date)
                ORDER BY 1 DESC";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                tieu.Add(new MovementRow { Ngay = r.GetDateTime(0), Kho = r.GetString(1), Nhap = r.GetDecimal(2) });
        }

        try
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $@"
                SELECT CAST(ngayXuat AS date), 'DVD', SUM(ISNULL(TrongLuongHang,0))
                FROM xuatTieu WHERE ngayXuat >= DATEADD(DAY,-{days},GETDATE())
                GROUP BY CAST(ngayXuat AS date)
                UNION ALL
                SELECT CAST(ngayXuat AS date), N'Thông Đào', SUM(ISNULL(TrongLuongHang,0))
                FROM XuatTieuThongDao WHERE ngayXuat >= DATEADD(DAY,-{days},GETDATE())
                GROUP BY CAST(ngayXuat AS date)
                ORDER BY 1 DESC";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var ngay = r.GetDateTime(0); var kho = r.GetString(1); var xuat = r.GetDecimal(2);
                var existing = tieu.FirstOrDefault(x => x.Ngay == ngay && x.Kho == kho);
                if (existing != null) existing.Xuat += xuat;
                else tieu.Add(new MovementRow { Ngay = ngay, Kho = kho, Xuat = xuat });
            }
        }
        catch { }

        vm.CaPhe = caphe.OrderByDescending(x => x.Ngay).ToList();
        vm.Tieu  = tieu.OrderByDescending(x => x.Ngay).ToList();
        return vm;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CHẤT LƯỢNG HÀNG HÓA (DoAm, TapChat)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<QualityViewModel> GetQualityAsync()
    {
        var vm    = new QualityViewModel();
        var caphe = new List<QualityRow>();
        var tieu  = new List<QualityRow>();

        await using var cn = Open();
        await cn.OpenAsync();

        // Cà phê lẻ (duongvandung) — DoAm, TapChat, QuyChuan
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT TenKhachHang, N'Dương Văn Dũng' AS Kho,
                    AVG(CAST(ISNULL(DoAm,0) AS decimal(10,2)))    AS AvgDoAm,
                    AVG(CAST(ISNULL(TapChat,0) AS decimal(10,2))) AS AvgTapChat,
                    SUM(ISNULL(TrongLuong,0)) AS TongKg,
                    COUNT(*) AS SoPhieu
                FROM duongvandung
                GROUP BY TenKhachHang
                HAVING COUNT(*) >= 3
                ORDER BY TongKg DESC";
            try
            {
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    caphe.Add(new QualityRow {
                        TenKhachHang = r.GetString(0), Kho = r.GetString(1),
                        AvgDoAm = r.GetDecimal(2), AvgTapChat = r.GetDecimal(3),
                        TongKg = r.GetDecimal(4), SoPhieu = r.GetInt32(5)
                    });
            }
            catch { }
        }

        // Cà phê sỉ (nhapCaPheSi)
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT TenKhachHang, N'Dương Văn Dũng' AS Kho,
                    AVG(CAST(ISNULL(DoAm,0) AS decimal(10,2))),
                    AVG(CAST(ISNULL(TapChat,0) AS decimal(10,2))),
                    SUM(ISNULL(TrongLuongHang,0)), COUNT(*)
                FROM nhapCaPheSi
                GROUP BY TenKhachHang HAVING COUNT(*) >= 2
                ORDER BY SUM(ISNULL(TrongLuong,0)) DESC";
            try
            {
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    caphe.Add(new QualityRow {
                        TenKhachHang = r.GetString(0), Kho = r.GetString(1),
                        AvgDoAm = r.GetDecimal(2), AvgTapChat = r.GetDecimal(3),
                        TongKg = r.GetDecimal(4), SoPhieu = r.GetInt32(5)
                    });
            }
            catch { }
        }

        // Tiêu lẻ (xntTieu)
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT TenKhachHang, N'Dương Văn Dũng' AS Kho,
                    AVG(CAST(ISNULL(DoAm,0) AS decimal(10,2))),
                    AVG(CAST(ISNULL(TapChat,0) AS decimal(10,2))),
                    SUM(ISNULL(TrongLuong,0)), COUNT(*)
                FROM xntTieu
                GROUP BY TenKhachHang HAVING COUNT(*) >= 2
                ORDER BY SUM(ISNULL(TrongLuong,0)) DESC";
            try
            {
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    tieu.Add(new QualityRow {
                        TenKhachHang = r.GetString(0), Kho = r.GetString(1),
                        AvgDoAm = r.GetDecimal(2), AvgTapChat = r.GetDecimal(3),
                        TongKg = r.GetDecimal(4), SoPhieu = r.GetInt32(5)
                    });
            }
            catch { }
        }

        if (caphe.Count > 0)
        {
            vm.AvgDoAmCaPhe    = caphe.Average(x => x.AvgDoAm);
            vm.AvgTapChatCaPhe = caphe.Average(x => x.AvgTapChat);
        }
        if (tieu.Count > 0)
        {
            vm.AvgDoAmTieu    = tieu.Average(x => x.AvgDoAm);
            vm.AvgTapChatTieu = tieu.Average(x => x.AvgTapChat);
        }

        vm.CaPhe = caphe.OrderByDescending(x => x.TongKg).Take(50).ToList();
        vm.Tieu  = tieu.OrderByDescending(x => x.TongKg).Take(50).ToList();
        return vm;
    }
}
