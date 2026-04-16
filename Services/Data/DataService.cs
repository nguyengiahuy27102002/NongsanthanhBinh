using Microsoft.Data.SqlClient;
using DuongVanDung.WebApp.Models.Data;

namespace DuongVanDung.WebApp.Services.Data;

public interface IDataService
{
    Task<DataTableViewModel> GetTableAsync(DataFilter filter);
    Task<(string khoDisplay, string bangDisplay, bool hasDem, bool isXuat, IReadOnlyList<DataEntryRow> rows)> GetAllRowsAsync(DataFilter filter);
}

public sealed class DataService : IDataService
{
    // ── Loại bảng ────────────────────────────────────────────────────────────
    private enum TableType
    {
        NhapLe,   // Nhập lẻ: TrongLuong, BaoThuong+BaoQuay, ngayNhap, TenKhachHang, DiaChi
        NhapSi,   // Nhập sỉ: TrongLuongHang, BaoThuong+BaoQuay, ngayNhap, TenKhachHang, DiaChi
        Xuat      // Xuất:    TrongLuongHang, SoBao, ngayXuat, TenTaiXe, DiaDiemXuatHang, không có ThanhTien/ChuyenKhoan
    }

    // ── Config mỗi bảng ──────────────────────────────────────────────────────
    private sealed record TableConfig(
        string    SqlTable,
        bool      IsYmoal,
        TableType Type,
        string    SdtCol,
        bool      HasDiaChi,
        bool      HasDem,
        string    KhoDisplay,
        string    BangDisplay,
        string    Theme
    );

    private static readonly Dictionary<(string kho, string bang), TableConfig> _configs = new()
    {
        // ── Dương Văn Dũng (nguyengiahuy) ────────────────────────────────────
        [("dvd","caphe-le")]    = new("duongvandung",      false, TableType.NhapLe, "Sdt", true,  false, "Dương Văn Dũng", "Nhập cà phê lẻ",  "blue"),
        [("dvd","caphe-si")]    = new("nhapCaPheSi",       false, TableType.NhapSi, "Sdt", true,  false, "Dương Văn Dũng", "Nhập cà phê sỉ",  "blue"),
        [("dvd","tieu-le")]     = new("xntTieu",           false, TableType.NhapLe, "Sdt", true,  true,  "Dương Văn Dũng", "Nhập tiêu lẻ",    "green"),
        [("dvd","tieu-si")]     = new("nhapTieuKhachSi",   false, TableType.NhapSi, "Sdt", true,  true,  "Dương Văn Dũng", "Nhập tiêu sỉ",    "green"),
        [("dvd","xuat-caphe")]  = new("xuatCaPhe",         false, TableType.Xuat,   "Sdt", true,  false, "Dương Văn Dũng", "Xuất cà phê",     "red"),
        [("dvd","xuat-tieu")]   = new("xuatTieu",          false, TableType.Xuat,   "Sdt", true,  true,  "Dương Văn Dũng", "Xuất tiêu",       "red"),

        // ── Thông Đào (nguyengiahuy) ─────────────────────────────────────────
        [("thongdao","caphe")]       = new("NhapCaPheThongDao", false, TableType.NhapSi, "Sdt", true,  false, "Thông Đào", "Nhập cà phê",    "blue"),
        [("thongdao","tieu")]        = new("NhapTieuThongDao",  false, TableType.NhapSi, "Sdt", true,  true,  "Thông Đào", "Nhập tiêu",      "green"),
        [("thongdao","xuat-caphe")]  = new("XuatCaPheThongDao", false, TableType.Xuat,   "Sdt", true,  false, "Thông Đào", "Xuất cà phê",    "red"),
        [("thongdao","xuat-tieu")]   = new("XuatTieuThongDao",  false, TableType.Xuat,   "Sdt", true,  true,  "Thông Đào", "Xuất tiêu",      "red"),

        // ── Ymoal (KhoYmoal) ─────────────────────────────────────────────────
        [("ymoal","caphe-le")]   = new("NhapCaPheLe",  true, TableType.NhapLe, "Sdt", true,  false, "Ymoal", "Nhập cà phê lẻ",  "yellow"),
        [("ymoal","caphe-si")]   = new("NhapCaPheSi",  true, TableType.NhapSi, "Sdt", false, false, "Ymoal", "Nhập cà phê sỉ",  "yellow"),
        [("ymoal","tieu-le")]    = new("NhapTieuLe",   true, TableType.NhapLe, "Sdt", true,  true,  "Ymoal", "Nhập tiêu lẻ",    "green"),
        [("ymoal","tieu-si")]    = new("NhapTieuSi",   true, TableType.NhapSi, "Sdt", true,  true,  "Ymoal", "Nhập tiêu sỉ",    "green"),
        [("ymoal","xuat-caphe")] = new("XuatCaPhe",    true, TableType.Xuat,   "Sdt", true,  false, "Ymoal", "Xuất cà phê",     "red"),
        [("ymoal","xuat-tieu")]  = new("XuatTieu",     true, TableType.Xuat,   "Sdt", true,  true,  "Ymoal", "Xuất tiêu",       "red"),

        // ── Cư Jut (nguyengiahuy — bảng chưa tạo) ───────────────────────────
        [("cujut","caphe")]       = new("NhapCaPheCuJut",  false, TableType.NhapSi, "Sdt", true,  false, "Cư Jut", "Nhập cà phê",    "purple"),
        [("cujut","tieu")]        = new("NhapTieuCuJut",   false, TableType.NhapSi, "Sdt", true,  true,  "Cư Jut", "Nhập tiêu",      "purple"),
        [("cujut","xuat-caphe")]  = new("XuatCaPheCuJut",  false, TableType.Xuat,   "Sdt", true,  false, "Cư Jut", "Xuất cà phê",    "red"),
        [("cujut","xuat-tieu")]   = new("XuatTieuCuJut",   false, TableType.Xuat,   "Sdt", true,  true,  "Cư Jut", "Xuất tiêu",      "red"),

        // ── Hòa Phú (nguyengiahuy — bảng chưa tạo) ──────────────────────────
        [("hoaphu","caphe")]       = new("NhapCaPheHoaPhu", false, TableType.NhapSi, "Sdt", true,  false, "Hòa Phú", "Nhập cà phê",   "green"),
        [("hoaphu","tieu")]        = new("NhapTieuHoaPhu",  false, TableType.NhapSi, "Sdt", true,  true,  "Hòa Phú", "Nhập tiêu",     "green"),
        [("hoaphu","xuat-caphe")]  = new("XuatCaPheHoaPhu", false, TableType.Xuat,   "Sdt", true,  false, "Hòa Phú", "Xuất cà phê",   "red"),
        [("hoaphu","xuat-tieu")]   = new("XuatTieuHoaPhu",  false, TableType.Xuat,   "Sdt", true,  true,  "Hòa Phú", "Xuất tiêu",     "red"),
    };

    private readonly string _connStr;
    private readonly string _connStrYmoal;

    public DataService(IConfiguration cfg)
    {
        _connStr      = cfg.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Missing DefaultConnection");
        _connStrYmoal = cfg.GetConnectionString("KhoYmoal")         ?? throw new InvalidOperationException("Missing KhoYmoal");
    }

    // ── Public API ───────────────────────────────────────────────────────────
    public async Task<DataTableViewModel> GetTableAsync(DataFilter filter)
    {
        var vm = new DataTableViewModel { Kho = filter.Kho, Bang = filter.Bang, Filter = filter };

        if (!_configs.TryGetValue((filter.Kho, filter.Bang), out var cfg))
        {
            vm.TableExists = false;
            vm.KhoDisplay  = filter.Kho;
            vm.BangDisplay = filter.Bang;
            return vm;
        }

        vm.KhoDisplay  = cfg.KhoDisplay;
        vm.BangDisplay = cfg.BangDisplay;
        vm.Theme       = cfg.Theme;
        vm.ShowDem     = cfg.HasDem;
        vm.IsXuat      = cfg.Type == TableType.Xuat;

        var connStr = cfg.IsYmoal ? _connStrYmoal : _connStr;

        try
        {
            await using var cn = new SqlConnection(connStr);
            await cn.OpenAsync();

            if (!await TableExistsAsync(cn, cfg.SqlTable))
            {
                vm.TableExists = false;
                return vm;
            }

            var dateCol   = DateCol(cfg);
            var weightCol = WeightCol(cfg);
            var (where, parms) = BuildWhere(filter, dateCol, cfg.Type);

            // Tổng hợp toàn bộ (sau filter)
            await using (var cmd = cn.CreateCommand())
            {
                var thanhTienSum = cfg.Type == TableType.Xuat
                    ? "0"
                    : "ISNULL(SUM(CAST(ThanhTien AS bigint)),0)";

                cmd.CommandText = $@"SELECT COUNT(*),
                    ISNULL(SUM({weightCol}),0),
                    ISNULL(SUM(QuyChuan),0),
                    {thanhTienSum}
                    FROM {cfg.SqlTable} {where}";
                foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v);
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    vm.Total          = r.GetInt32(0);
                    vm.TongSoLuongAll = r.IsDBNull(1) ? 0 : r.GetDecimal(1);
                    vm.TongKgAll      = r.IsDBNull(2) ? 0 : r.GetDecimal(2);
                    vm.TongTienAll    = r.IsDBNull(3) ? 0 : r.GetInt64(3);
                }
            }

            // Dữ liệu phân trang
            var offset = (filter.Page - 1) * DataFilter.PageSize;
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = BuildSelect(cfg, where)
                    + $" ORDER BY {dateCol} DESC OFFSET {offset} ROWS FETCH NEXT {DataFilter.PageSize} ROWS ONLY";
                foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v);
                var list = new List<DataEntryRow>();
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) list.Add(MapRow(r, cfg.HasDem));
                vm.Rows = list;
            }
        }
        catch
        {
            vm.TableExists = false;
        }

        return vm;
    }

    public async Task<(string khoDisplay, string bangDisplay, bool hasDem, bool isXuat, IReadOnlyList<DataEntryRow> rows)> GetAllRowsAsync(DataFilter filter)
    {
        if (!_configs.TryGetValue((filter.Kho, filter.Bang), out var cfg))
            return (filter.Kho, filter.Bang, false, false, Array.Empty<DataEntryRow>());

        var connStr = cfg.IsYmoal ? _connStrYmoal : _connStr;
        var rows = new List<DataEntryRow>();

        try
        {
            await using var cn = new SqlConnection(connStr);
            await cn.OpenAsync();

            if (!await TableExistsAsync(cn, cfg.SqlTable))
                return (cfg.KhoDisplay, cfg.BangDisplay, cfg.HasDem, cfg.Type == TableType.Xuat, rows);

            var dateCol = DateCol(cfg);
            var (where, parms) = BuildWhere(filter, dateCol, cfg.Type);

            await using var cmd = cn.CreateCommand();
            cmd.CommandText = BuildSelect(cfg, where) + $" ORDER BY {dateCol} DESC";
            foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) rows.Add(MapRow(r, cfg.HasDem));
        }
        catch { /* trả về rỗng */ }

        return (cfg.KhoDisplay, cfg.BangDisplay, cfg.HasDem, cfg.Type == TableType.Xuat, rows);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static string DateCol(TableConfig cfg) =>
        cfg.Type == TableType.Xuat ? "ngayXuat" : "ngayNhap";

    private static string WeightCol(TableConfig cfg) =>
        cfg.Type == TableType.NhapLe ? "TrongLuong" : "TrongLuongHang";

    private static async Task<bool> TableExistsAsync(SqlConnection cn, string tableName)
    {
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM sys.tables WHERE name = @t AND is_ms_shipped = 0";
        cmd.Parameters.AddWithValue("@t", tableName);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0) > 0;
    }

    private static string BuildSelect(TableConfig cfg, string where)
    {
        var dem = cfg.HasDem ? "ISNULL(Dem,0)" : "NULL AS Dem";

        return cfg.Type switch
        {
            TableType.NhapLe => BuildSelectNhapLe(cfg, where, dem),
            TableType.NhapSi => BuildSelectNhapSi(cfg, where, dem),
            TableType.Xuat   => BuildSelectXuat(cfg, where, dem),
            _                => throw new ArgumentOutOfRangeException()
        };
    }

    private static string BuildSelectNhapLe(TableConfig cfg, string where, string dem)
    {
        var diaChi = cfg.HasDiaChi ? "ISNULL(DiaChi,'')" : "N'' AS DiaChi";
        return $@"SELECT ngayNhap,
                ISNULL(TenKhachHang,'')                         AS TenKhachHang,
                ISNULL(CAST({cfg.SdtCol} AS nvarchar(30)),'')   AS Sdt,
                {diaChi},
                ISNULL(TrongLuong,0)                            AS SoLuong,
                ISNULL(DoAm,0)                                  AS DoAm,
                ISNULL(TapChat,0)                               AS TapChat,
                {dem},
                ISNULL(TrongLuongTruBi,0)                       AS TrongLuongTruBi,
                (ISNULL(BaoThuong,0) + ISNULL(BaoQuay,0))       AS SoBao,
                ISNULL(QuyChuan,0)                              AS QuyChuan,
                CAST(ISNULL(ThanhTien,0) AS bigint)             AS ThanhTien,
                ISNULL(ChuyenKhoan,'')                          AS CK
                FROM {cfg.SqlTable} {where}";
    }

    private static string BuildSelectNhapSi(TableConfig cfg, string where, string dem)
    {
        var diaChi = cfg.HasDiaChi ? "ISNULL(DiaChi,'')" : "N'' AS DiaChi";
        return $@"SELECT ngayNhap,
                ISNULL(TenKhachHang,'')                         AS TenKhachHang,
                ISNULL(CAST({cfg.SdtCol} AS nvarchar(30)),'')   AS Sdt,
                {diaChi},
                ISNULL(TrongLuongHang,0)                        AS SoLuong,
                ISNULL(DoAm,0)                                  AS DoAm,
                ISNULL(TapChat,0)                               AS TapChat,
                {dem},
                ISNULL(TrongLuongTruBi,0)                       AS TrongLuongTruBi,
                (ISNULL(BaoThuong,0) + ISNULL(BaoQuay,0))       AS SoBao,
                ISNULL(QuyChuan,0)                              AS QuyChuan,
                CAST(ISNULL(ThanhTien,0) AS bigint)             AS ThanhTien,
                ISNULL(ChuyenKhoan,'')                          AS CK
                FROM {cfg.SqlTable} {where}";
    }

    private static string BuildSelectXuat(TableConfig cfg, string where, string dem)
    {
        return $@"SELECT ngayXuat                                    AS ngayNhap,
                ISNULL(TenTaiXe,'')                             AS TenKhachHang,
                ISNULL(CAST({cfg.SdtCol} AS nvarchar(30)),'')   AS Sdt,
                ISNULL(DiaDiemXuatHang,'')                      AS DiaChi,
                ISNULL(TrongLuongHang,0)                        AS SoLuong,
                ISNULL(DoAm,0)                                  AS DoAm,
                ISNULL(TapChat,0)                               AS TapChat,
                {dem},
                ISNULL(TrongLuongTruBi,0)                       AS TrongLuongTruBi,
                ISNULL(SoBao,0)                                 AS SoBao,
                ISNULL(QuyChuan,0)                              AS QuyChuan,
                CAST(0 AS bigint)                               AS ThanhTien,
                N''                                             AS CK
                FROM {cfg.SqlTable} {where}";
    }

    private static (string where, Dictionary<string, object> parms) BuildWhere(DataFilter f, string dateCol, TableType type)
    {
        var parts = new List<string>();
        var parms = new Dictionary<string, object>();

        if (f.DateFrom.HasValue)
        {
            parts.Add($"CAST({dateCol} AS date) >= @df");
            parms["@df"] = f.DateFrom.Value.Date;
        }
        if (f.DateTo.HasValue)
        {
            parts.Add($"CAST({dateCol} AS date) <= @dt");
            parms["@dt"] = f.DateTo.Value.Date;
        }
        if (f.MinAmount.HasValue && f.MinAmount > 0 && type != TableType.Xuat)
        {
            parts.Add("CAST(ThanhTien AS bigint) >= @minamt");
            parms["@minamt"] = f.MinAmount.Value;
        }

        return parts.Count == 0 ? ("", parms) : ("WHERE " + string.Join(" AND ", parts), parms);
    }

    private static DataEntryRow MapRow(SqlDataReader r, bool hasDem) => new()
    {
        NgayNhap        = r.GetDateTime(0),
        TenKhachHang    = r.IsDBNull(1) ? "" : r.GetString(1),
        SDT             = r.IsDBNull(2) ? "" : r.GetString(2).Trim(),
        DiaChi          = r.IsDBNull(3) ? "" : r.GetString(3),
        SoLuong         = r.IsDBNull(4) ? 0 : r.GetDecimal(4),
        DoAm            = r.IsDBNull(5) ? 0 : r.GetDecimal(5),
        TapChat         = r.IsDBNull(6) ? 0 : r.GetDecimal(6),
        Dem             = hasDem && !r.IsDBNull(7) ? r.GetDecimal(7) : null,
        TrongLuongTruBi = r.IsDBNull(8) ? 0 : r.GetDecimal(8),
        SoBao           = r.IsDBNull(9) ? 0 : r.GetInt32(9),
        QuyChuan        = r.IsDBNull(10) ? 0 : r.GetDecimal(10),
        ThanhTien       = r.IsDBNull(11) ? 0 : r.GetInt64(11),
        PhuongThuc      = r.IsDBNull(12) ? "" : r.GetString(12).Trim()
    };
}
