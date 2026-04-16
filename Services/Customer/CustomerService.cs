using System.Data;
using Microsoft.Data.SqlClient;
using DuongVanDung.WebApp.Models.Customer;

namespace DuongVanDung.WebApp.Services.Customer;

public interface ICustomerService
{
    Task<CustomerListViewModel> GetListAsync(string search, int page, int pageSize);
    Task<CustomerDetailViewModel?> GetDetailAsync(int customerId);
    Task<DuplicateListViewModel> FindDuplicatesAsync();
    Task<bool> MergeCustomersAsync(int keepId, int deleteId);
    Task<bool> DeleteCustomerAsync(int id);
    Task<CustomerSearchViewModel> SearchAsync(string keyword, string searchBy);
    Task<CustomerAnalysisViewModel> GetAnalysisAsync();
    Task<CustomerFinancialViewModel> GetFinancialAsync();
    Task<CustomerBehaviorViewModel> GetBehaviorAsync();
    Task<TodayEntryViewModel> GetTodayEntriesAsync(string filterKho, string filterSanPham);
}

public sealed class CustomerService : ICustomerService
{
    private readonly string _connStr;
    private readonly string _connStrYmoal;

    public CustomerService(IConfiguration cfg)
    {
        _connStr      = cfg.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Missing DefaultConnection");
        _connStrYmoal = cfg.GetConnectionString("KhoYmoal")         ?? throw new InvalidOperationException("Missing KhoYmoal");
    }

    private SqlConnection Open()      => new(_connStr);
    private SqlConnection OpenYmoal() => new(_connStrYmoal);

    // ─────────────────────────────────────────────────────────────────────────
    // DANH SÁCH
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<CustomerListViewModel> GetListAsync(string search, int page, int pageSize)
    {
        await using var cn = Open();
        await cn.OpenAsync();

        var vm = new CustomerListViewModel { Search = search, Page = page, PageSize = pageSize };
        var offset = (page - 1) * pageSize;
        var like = $"%{search}%";
        var hasSearch = !string.IsNullOrWhiteSpace(search);

        // Count
        await using var cntCmd = cn.CreateCommand();
        cntCmd.CommandText = hasSearch
            ? "SELECT COUNT(*) FROM KhachHang WHERE TenKhachHang LIKE @s OR SDT LIKE @s OR CCCD LIKE @s OR DiaChi LIKE @s"
            : "SELECT COUNT(*) FROM KhachHang";
        if (hasSearch) cntCmd.Parameters.AddWithValue("@s", like);
        vm.Total = (int)(await cntCmd.ExecuteScalarAsync() ?? 0);

        // Data
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
            SELECT k.CustomerID, k.TenKhachHang, k.GioiTinh, k.NgaySinh,
                   k.SDT, k.CCCD, k.NgayCap, k.DiaChi,
                   k.NgayTaoTaiKhoan, k.GhiChu,
                   ISNULL(cf.TongTien,0) AS TongChiCaPhe, ISNULL(cf.SoPhieu,0) AS SoPhieuCaPhe,
                   ISNULL(ti.TongTien,0) AS TongChiTieu,  ISNULL(ti.SoPhieu,0) AS SoPhieuTieu
            FROM KhachHang k
            LEFT JOIN (
                SELECT CustomerID, SUM(CAST(ThanhTien AS bigint)) AS TongTien, COUNT(*) AS SoPhieu
                FROM nhapCaPheSi GROUP BY CustomerID
            ) cf ON cf.CustomerID = CAST(k.CustomerID AS nvarchar)
            LEFT JOIN (
                SELECT CustomerID, SUM(CAST(ThanhTien AS bigint)) AS TongTien, COUNT(*) AS SoPhieu
                FROM nhapTieuKhachSi GROUP BY CustomerID
            ) ti ON ti.CustomerID = CAST(k.CustomerID AS nvarchar)
            " + (hasSearch ? "WHERE k.TenKhachHang LIKE @s OR k.SDT LIKE @s OR k.CCCD LIKE @s OR k.DiaChi LIKE @s" : "") + @"
            ORDER BY k.NgayTaoTaiKhoan DESC
            OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY";

        if (hasSearch) cmd.Parameters.AddWithValue("@s", like);
        cmd.Parameters.AddWithValue("@offset", offset);
        cmd.Parameters.AddWithValue("@size", pageSize);

        var list = new List<KhachHangRecord>();
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(MapKhachSi(rdr));
        vm.Customers = list;

        return vm;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CHI TIẾT
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<CustomerDetailViewModel?> GetDetailAsync(int customerId)
    {
        await using var cn = Open();
        await cn.OpenAsync();

        // Thông tin khách hàng
        KhachHangRecord? customer;
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT k.CustomerID, k.TenKhachHang, k.GioiTinh, k.NgaySinh,
                       k.SDT, k.CCCD, k.NgayCap, k.DiaChi,
                       k.NgayTaoTaiKhoan, k.GhiChu,
                       ISNULL(cf.TongTien,0), ISNULL(cf.SoPhieu,0),
                       ISNULL(ti.TongTien,0), ISNULL(ti.SoPhieu,0)
                FROM KhachHang k
                LEFT JOIN (
                    SELECT CustomerID, SUM(CAST(ThanhTien AS bigint)) AS TongTien, COUNT(*) AS SoPhieu
                    FROM nhapCaPheSi GROUP BY CustomerID
                ) cf ON cf.CustomerID = CAST(k.CustomerID AS nvarchar)
                LEFT JOIN (
                    SELECT CustomerID, SUM(CAST(ThanhTien AS bigint)) AS TongTien, COUNT(*) AS SoPhieu
                    FROM nhapTieuKhachSi GROUP BY CustomerID
                ) ti ON ti.CustomerID = CAST(k.CustomerID AS nvarchar)
                WHERE k.CustomerID = @id";
            cmd.Parameters.AddWithValue("@id", customerId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;
            customer = MapKhachSi(r);
        }

        var transactions = new List<TransactionRow>();
        var idStr = customerId.ToString();

        // Cà phê sỉ (nhapCaPheSi by CustomerID)
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"SELECT ngayNhap, ISNULL(QuyChuan,0), CAST(ISNULL(ThanhTien,0) AS bigint), ISNULL(ChuyenKhoan,'')
                FROM nhapCaPheSi WHERE CustomerID = @id ORDER BY ngayNhap DESC";
            cmd.Parameters.AddWithValue("@id", idStr);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                transactions.Add(new TransactionRow { NgayNhap = r.GetDateTime(0), QuyChuan = r.GetDecimal(1), ThanhTien = r.GetInt64(2), PhuongThuc = r.GetString(3), Kho = "Dương Văn Dũng", LoaiNhap = "Sỉ", SanPham = "Cà phê" });
        }

        // Tiêu sỉ (nhapTieuKhachSi by CustomerID)
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"SELECT ngayNhap, ISNULL(QuyChuan,0), CAST(ISNULL(ThanhTien,0) AS bigint), ISNULL(ChuyenKhoan,'')
                FROM nhapTieuKhachSi WHERE CustomerID = @id ORDER BY ngayNhap DESC";
            cmd.Parameters.AddWithValue("@id", idStr);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                transactions.Add(new TransactionRow { NgayNhap = r.GetDateTime(0), QuyChuan = r.GetDecimal(1), ThanhTien = r.GetInt64(2), PhuongThuc = r.GetString(3), Kho = "Dương Văn Dũng", LoaiNhap = "Sỉ", SanPham = "Tiêu" });
        }

        if (!string.IsNullOrEmpty(customer.SDT))
        {
            // Cà phê lẻ (duongvandung by SDT)
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"SELECT ngayNhap, ISNULL(QuyChuan,0), CAST(ISNULL(ThanhTien,0) AS bigint), ISNULL(ChuyenKhoan,'')
                    FROM duongvandung WHERE Sdt = @sdt ORDER BY ngayNhap DESC";
                cmd.Parameters.AddWithValue("@sdt", customer.SDT);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    transactions.Add(new TransactionRow { NgayNhap = r.GetDateTime(0), QuyChuan = r.GetDecimal(1), ThanhTien = r.GetInt64(2), PhuongThuc = r.GetString(3), Kho = "Dương Văn Dũng", LoaiNhap = "Lẻ", SanPham = "Cà phê" });
            }

            // Tiêu lẻ (xntTieu by SDT)
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"SELECT ngayNhap, ISNULL(QuyChuan,0), CAST(ISNULL(ThanhTien,0) AS bigint), ISNULL(ChuyenKhoan,'')
                    FROM xntTieu WHERE Sdt = @sdt ORDER BY ngayNhap DESC";
                cmd.Parameters.AddWithValue("@sdt", customer.SDT);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    transactions.Add(new TransactionRow { NgayNhap = r.GetDateTime(0), QuyChuan = r.GetDecimal(1), ThanhTien = r.GetInt64(2), PhuongThuc = r.GetString(3), Kho = "Dương Văn Dũng", LoaiNhap = "Lẻ", SanPham = "Tiêu" });
            }

            // Cà phê Thông Đào
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"SELECT ngayNhap, ISNULL(QuyChuan,0), CAST(ISNULL(ThanhTien,0) AS bigint), ISNULL(ChuyenKhoan,'')
                    FROM NhapCaPheThongDao WHERE Sdt = @sdt ORDER BY ngayNhap DESC";
                cmd.Parameters.AddWithValue("@sdt", customer.SDT);
                try {
                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                        transactions.Add(new TransactionRow { NgayNhap = r.GetDateTime(0), QuyChuan = r.GetDecimal(1), ThanhTien = r.GetInt64(2), PhuongThuc = r.GetString(3), Kho = "Thông Đào", LoaiNhap = "Sỉ", SanPham = "Cà phê" });
                } catch { /* bảng có thể không tồn tại */ }
            }

            // Tiêu Thông Đào
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"SELECT ngayNhap, ISNULL(QuyChuan,0), CAST(ISNULL(ThanhTien,0) AS bigint), ISNULL(ChuyenKhoan,'')
                    FROM NhapTieuThongDao WHERE Sdt = @sdt ORDER BY ngayNhap DESC";
                cmd.Parameters.AddWithValue("@sdt", customer.SDT);
                try {
                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                        transactions.Add(new TransactionRow { NgayNhap = r.GetDateTime(0), QuyChuan = r.GetDecimal(1), ThanhTien = r.GetInt64(2), PhuongThuc = r.GetString(3), Kho = "Thông Đào", LoaiNhap = "Sỉ", SanPham = "Tiêu" });
                } catch { /* bảng có thể không tồn tại */ }
            }
        }

        transactions.Sort((a, b) => b.NgayNhap.CompareTo(a.NgayNhap));
        return new CustomerDetailViewModel { Customer = customer, Transactions = transactions };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TÌM TRÙNG
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<DuplicateListViewModel> FindDuplicatesAsync()
    {
        await using var cn = Open();
        await cn.OpenAsync();

        var groups = new List<DuplicateGroup>();

        // Trùng SDT
        var dupSdts = new List<string>();
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT SDT FROM KhachHang WHERE SDT IS NOT NULL AND SDT <> '' GROUP BY SDT HAVING COUNT(*) > 1";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) dupSdts.Add(r.GetString(0));
        }
        foreach (var sdt in dupSdts)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = BaseSelectKhachHang() + " WHERE k.SDT = @val ORDER BY k.NgayTaoTaiKhoan";
            cmd.Parameters.AddWithValue("@val", sdt);
            var customers = await ReadKhachHangList(cmd);
            groups.Add(new DuplicateGroup { LoaiTrung = "SDT", GiaTri = sdt, Customers = customers });
        }

        // Trùng CCCD
        var dupCccds = new List<string>();
        await using (var cmd2 = cn.CreateCommand())
        {
            cmd2.CommandText = "SELECT CCCD FROM KhachHang WHERE CCCD IS NOT NULL AND CCCD <> '' GROUP BY CCCD HAVING COUNT(*) > 1";
            await using var r = await cmd2.ExecuteReaderAsync();
            while (await r.ReadAsync()) dupCccds.Add(r.GetString(0));
        }
        foreach (var cccd in dupCccds)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = BaseSelectKhachHang() + " WHERE k.CCCD = @val ORDER BY k.NgayTaoTaiKhoan";
            cmd.Parameters.AddWithValue("@val", cccd);
            var customers = await ReadKhachHangList(cmd);
            groups.Add(new DuplicateGroup { LoaiTrung = "CCCD", GiaTri = cccd, Customers = customers });
        }

        return new DuplicateListViewModel { Groups = groups };

        static string BaseSelectKhachHang() => @"
            SELECT k.CustomerID, k.TenKhachHang, k.GioiTinh, k.NgaySinh,
                   k.SDT, k.CCCD, k.NgayCap, k.DiaChi, k.NgayTaoTaiKhoan, k.GhiChu,
                   ISNULL(cf.TongTien,0), ISNULL(cf.SoPhieu,0),
                   ISNULL(ti.TongTien,0), ISNULL(ti.SoPhieu,0)
            FROM KhachHang k
            LEFT JOIN (SELECT CustomerID, SUM(CAST(ThanhTien AS bigint)) AS TongTien, COUNT(*) AS SoPhieu FROM nhapCaPheSi GROUP BY CustomerID) cf ON cf.CustomerID = CAST(k.CustomerID AS nvarchar)
            LEFT JOIN (SELECT CustomerID, SUM(CAST(ThanhTien AS bigint)) AS TongTien, COUNT(*) AS SoPhieu FROM nhapTieuKhachSi GROUP BY CustomerID) ti ON ti.CustomerID = CAST(k.CustomerID AS nvarchar)";

        static async Task<List<KhachHangRecord>> ReadKhachHangList(SqlCommand cmd)
        {
            var list = new List<KhachHangRecord>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(MapKhachSi(r));
            return list;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GỘP / XÓA
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<bool> MergeCustomersAsync(int keepId, int deleteId)
    {
        try
        {
            await using var cn = Open();
            await cn.OpenAsync();
            await using var tx = cn.BeginTransaction();

            // Chuyển phiếu sỉ sang ID giữ lại
            foreach (var tbl in new[] { "nhapCaPheSi", "nhapTieuKhachSi" })
            {
                await using var cmd = cn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = $"UPDATE {tbl} SET CustomerID = @keep WHERE CustomerID = @del";
                cmd.Parameters.AddWithValue("@keep", keepId.ToString());
                cmd.Parameters.AddWithValue("@del", deleteId.ToString());
                await cmd.ExecuteNonQueryAsync();
            }

            // Xóa bản ghi trùng
            await using var delCmd = cn.CreateCommand();
            delCmd.Transaction = tx;
            delCmd.CommandText = "DELETE FROM KhachHang WHERE CustomerID = @id";
            delCmd.Parameters.AddWithValue("@id", deleteId);
            await delCmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteCustomerAsync(int id)
    {
        try
        {
            await using var cn = Open();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM KhachHang WHERE CustomerID = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch { return false; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TÌM KIẾM
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<CustomerSearchViewModel> SearchAsync(string keyword, string searchBy)
    {
        var vm = new CustomerSearchViewModel { Keyword = keyword, SearchBy = searchBy, HasSearched = !string.IsNullOrWhiteSpace(keyword) };
        if (!vm.HasSearched) return vm;

        await using var cn = Open();
        await cn.OpenAsync();

        var col = searchBy switch
        {
            "sdt"    => "k.SDT",
            "cccd"   => "k.CCCD",
            "diachi" => "k.DiaChi",
            _        => "k.TenKhachHang"
        };

        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $@"
            SELECT TOP 100
                k.CustomerID, k.TenKhachHang, k.GioiTinh, k.NgaySinh,
                k.SDT, k.CCCD, k.NgayCap, k.DiaChi,
                k.NgayTaoTaiKhoan, k.GhiChu,
                ISNULL(cf.TongTien,0) AS TongChiCaPhe, ISNULL(cf.SoPhieu,0) AS SoPhieuCaPhe,
                ISNULL(ti.TongTien,0) AS TongChiTieu,  ISNULL(ti.SoPhieu,0) AS SoPhieuTieu
            FROM KhachHang k
            LEFT JOIN (
                SELECT CustomerID, SUM(CAST(ThanhTien AS bigint)) AS TongTien, COUNT(*) AS SoPhieu
                FROM nhapCaPheSi GROUP BY CustomerID
            ) cf ON cf.CustomerID = CAST(k.CustomerID AS nvarchar)
            LEFT JOIN (
                SELECT CustomerID, SUM(CAST(ThanhTien AS bigint)) AS TongTien, COUNT(*) AS SoPhieu
                FROM nhapTieuKhachSi GROUP BY CustomerID
            ) ti ON ti.CustomerID = CAST(k.CustomerID AS nvarchar)
            WHERE {col} LIKE @kw
            ORDER BY k.TenKhachHang";
        cmd.Parameters.AddWithValue("@kw", $"%{keyword}%");

        var list = new List<KhachHangRecord>();
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync()) list.Add(MapKhachSi(rdr));
        vm.Results = list;
        return vm;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BÁO CÁO PHÂN TÍCH
    // ─────────────────────────────────────────────────────────────────────────
    // ── SQL helper: region CASE expression ─────────────────────────────────
    private const string RegionCase = @"
        CASE
            WHEN DiaChi LIKE N'%Ea Tiêu%' OR DiaChi LIKE N'%ea tiêu%' OR DiaChi LIKE N'%B Tiêu%' OR DiaChi LIKE N'%Buôn Tiêu%' OR DiaChi LIKE N'%b tiêu%' THEN N'Ea Tiêu'
            WHEN DiaChi LIKE N'%Ea Ktur%' OR DiaChi LIKE N'%ea ktur%' THEN N'Ea Ktur'
            WHEN DiaChi LIKE N'%Hòa Thắng%' OR DiaChi LIKE N'%hòa thắng%' THEN N'Hòa Thắng'
            WHEN DiaChi LIKE N'%Ea Kao%' OR DiaChi LIKE N'%ea kao%' OR DiaChi LIKE N'%Cao Thắng%' OR DiaChi LIKE N'%cao thắng%' THEN N'Ea Kao'
            WHEN DiaChi LIKE N'%Buôn Jung%' OR DiaChi LIKE N'%B Jung%' OR DiaChi LIKE N'%b jung%' OR DiaChi LIKE N'%buôn jung%' THEN N'Buôn Jung'
            WHEN DiaChi LIKE N'%Dray Bhang%' OR DiaChi LIKE N'%dray bhang%' OR DiaChi LIKE N'%Lộ 13%' OR DiaChi LIKE N'%lộ 13%' THEN N'Dray Bhang'
            WHEN DiaChi LIKE N'%Ehu%' OR DiaChi LIKE N'%ehu%' OR DiaChi LIKE N'%EaHu%' THEN N'Ehu'
            WHEN DiaChi LIKE N'%Ea Bông%' OR DiaChi LIKE N'%ea bông%' THEN N'Ea Bông'
            WHEN DiaChi LIKE N'%Lak%' OR DiaChi LIKE N'%lak%' THEN N'Lak'
            WHEN DiaChi LIKE N'%Trung Hòa%' OR DiaChi LIKE N'%trung hòa%' OR DiaChi LIKE N'%Tân Hòa%' OR DiaChi LIKE N'%tân hòa%' OR DiaChi = N'TH' OR DiaChi = N'th' THEN N'Trung Hòa'
            WHEN DiaChi = N'VD' OR DiaChi LIKE N'vd%' OR DiaChi LIKE N'VD%' THEN N'Vùng Dương'
            WHEN DiaChi LIKE N'%19/8%' THEN N'19/8'
            ELSE N'Khác'
        END";

    public async Task<CustomerAnalysisViewModel> GetAnalysisAsync()
    {
        await using var cn = Open();
        await cn.OpenAsync();
        var vm = new CustomerAnalysisViewModel();

        // Tổng khách
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM KhachHang";
            vm.TongKhachSi = (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM DanhSachKhachLe";
            vm.TongKhachLe = (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM KhachHang WHERE NgayTaoTaiKhoan >= DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1)";
            vm.KhachMoiThangNay = (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }

        // Top cà phê — dựa trên TRỌNG LƯỢNG (gộp lẻ + sỉ)
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT TOP 15 TenKhachHang, '' AS CustomerID,
                    SUM(TongTien) AS TongTien, SUM(TongKg) AS TongKg, SUM(SoPhieu) AS SoPhieu
                FROM (
                    SELECT TenKhachHang, SUM(CAST(ISNULL(ThanhTien,0) AS bigint)) AS TongTien, SUM(ISNULL(TrongLuong,0)) AS TongKg, COUNT(*) AS SoPhieu FROM duongvandung GROUP BY TenKhachHang
                    UNION ALL
                    SELECT TenKhachHang, SUM(CAST(ISNULL(ThanhTien,0) AS bigint)), SUM(ISNULL(TrongLuongHang,0)), COUNT(*) FROM nhapCaPheSi GROUP BY TenKhachHang
                ) x
                GROUP BY TenKhachHang
                ORDER BY TongKg DESC";
            vm.TopCaPhe = await ReadTopCustomers(cmd);
        }

        // Top tiêu — dựa trên TRỌNG LƯỢNG (gộp lẻ + sỉ)
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT TOP 15 TenKhachHang, '' AS CustomerID,
                    SUM(TongTien) AS TongTien, SUM(TongKg) AS TongKg, SUM(SoPhieu) AS SoPhieu
                FROM (
                    SELECT TenKhachHang, SUM(CAST(ISNULL(ThanhTien,0) AS bigint)) AS TongTien, SUM(ISNULL(TrongLuong,0)) AS TongKg, COUNT(*) AS SoPhieu FROM xntTieu GROUP BY TenKhachHang
                    UNION ALL
                    SELECT TenKhachHang, SUM(CAST(ISNULL(ThanhTien,0) AS bigint)), SUM(ISNULL(TrongLuongHang,0)), COUNT(*) FROM nhapTieuKhachSi GROUP BY TenKhachHang
                ) x
                GROUP BY TenKhachHang
                ORDER BY TongKg DESC";
            vm.TopTieu = await ReadTopCustomers(cmd);
        }

        // Xu hướng 12 tháng — CÀ PHÊ (gộp lẻ + sỉ)
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT Nam, Thang, SUM(SoPhieu), SUM(TongKg) FROM (
                    SELECT YEAR(ngayNhap) AS Nam, MONTH(ngayNhap) AS Thang, COUNT(*) AS SoPhieu, SUM(ISNULL(TrongLuong,0)) AS TongKg
                    FROM duongvandung WHERE ngayNhap >= DATEADD(MONTH,-11,DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1))
                    GROUP BY YEAR(ngayNhap), MONTH(ngayNhap)
                    UNION ALL
                    SELECT YEAR(ngayNhap), MONTH(ngayNhap), COUNT(*), SUM(ISNULL(TrongLuongHang,0))
                    FROM nhapCaPheSi WHERE ngayNhap >= DATEADD(MONTH,-11,DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1))
                    GROUP BY YEAR(ngayNhap), MONTH(ngayNhap)
                ) x GROUP BY Nam, Thang ORDER BY Nam, Thang";
            vm.MonthlyCaPhe = await ReadTrends(cmd);
        }

        // Xu hướng 12 tháng — TIÊU (gộp lẻ + sỉ)
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT Nam, Thang, SUM(SoPhieu), SUM(TongKg) FROM (
                    SELECT YEAR(ngayNhap) AS Nam, MONTH(ngayNhap) AS Thang, COUNT(*) AS SoPhieu, SUM(ISNULL(TrongLuong,0)) AS TongKg
                    FROM xntTieu WHERE ngayNhap >= DATEADD(MONTH,-11,DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1))
                    GROUP BY YEAR(ngayNhap), MONTH(ngayNhap)
                    UNION ALL
                    SELECT YEAR(ngayNhap), MONTH(ngayNhap), COUNT(*), SUM(ISNULL(TrongLuongHang,0))
                    FROM nhapTieuKhachSi WHERE ngayNhap >= DATEADD(MONTH,-11,DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1))
                    GROUP BY YEAR(ngayNhap), MONTH(ngayNhap)
                ) x GROUP BY Nam, Thang ORDER BY Nam, Thang";
            vm.MonthlyTieu = await ReadTrends(cmd);
        }

        // Phân bổ khu vực — dựa trên TRỌNG LƯỢNG thực tế từ bảng nhập (không chỉ đếm khách)
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = $@"
                SELECT KhuVuc, SUM(KgCaPhe) AS KgCaPhe, SUM(KgTieu) AS KgTieu, COUNT(DISTINCT TenKhachHang) AS SoKhach
                FROM (
                    SELECT TenKhachHang, {RegionCase} AS KhuVuc, ISNULL(QuyChuan,0) AS KgCaPhe, 0 AS KgTieu FROM duongvandung WHERE DiaChi IS NOT NULL AND DiaChi <> ''
                    UNION ALL
                    SELECT TenKhachHang, {RegionCase}, ISNULL(QuyChuan,0), 0 FROM nhapCaPheSi WHERE DiaChi IS NOT NULL AND DiaChi <> ''
                    UNION ALL
                    SELECT TenKhachHang, {RegionCase}, 0, ISNULL(QuyChuan,0) FROM xntTieu WHERE DiaChi IS NOT NULL AND DiaChi <> ''
                    UNION ALL
                    SELECT TenKhachHang, {RegionCase}, 0, ISNULL(QuyChuan,0) FROM nhapTieuKhachSi WHERE DiaChi IS NOT NULL AND DiaChi <> ''
                ) x
                GROUP BY KhuVuc
                ORDER BY (SUM(KgCaPhe) + SUM(KgTieu)) DESC";
            var list = new List<RegionStat>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new RegionStat
                {
                    KhuVuc      = r.GetString(0),
                    TongKgCaPhe = r.IsDBNull(1) ? 0 : r.GetDecimal(1),
                    TongKgTieu  = r.IsDBNull(2) ? 0 : r.GetDecimal(2),
                    TongKg      = (r.IsDBNull(1) ? 0 : r.GetDecimal(1)) + (r.IsDBNull(2) ? 0 : r.GetDecimal(2)),
                    SoKhach     = r.GetInt32(3),
                });
            vm.RegionStats = list;
        }

        return vm;
    }

    private static async Task<List<TopCustomerRow>> ReadTopCustomers(SqlCommand cmd)
    {
        var list = new List<TopCustomerRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new TopCustomerRow
            {
                TenKhachHang = r.GetString(0),
                CustomerID   = r.IsDBNull(1) ? "" : r.GetString(1),
                TongTien     = r.IsDBNull(2) ? 0 : r.GetInt64(2),
                TongKg       = r.IsDBNull(3) ? 0 : r.GetDecimal(3),
                SoPhieu      = r.GetInt32(4)
            });
        return list;
    }

    private static async Task<List<MonthlyTrend>> ReadTrends(SqlCommand cmd)
    {
        var list = new List<MonthlyTrend>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new MonthlyTrend { Nam = r.GetInt32(0), Thang = r.GetInt32(1), SoPhieu = r.GetInt32(2), TongKg = r.IsDBNull(3) ? 0 : r.GetDecimal(3) });
        return list;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BÁO CÁO TÀI CHÍNH
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<CustomerFinancialViewModel> GetFinancialAsync()
    {
        await using var cn = Open();
        await cn.OpenAsync();
        var vm = new CustomerFinancialViewModel();

        // Tổng chi tháng này
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT ISNULL(SUM(CAST(ThanhTien AS bigint)),0)
                FROM chi
                WHERE ngayChi >= DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1)";
            vm.TongChiThangNay = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        }

        // Tổng chuyển khoản
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT ISNULL(SUM(CAST(ThanhTien AS bigint)),0), COUNT(*) FROM ChuyenKhoan";
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync()) { vm.TongChuyen = r.IsDBNull(0) ? 0 : r.GetInt64(0); vm.SoGiaoDich = r.GetInt32(1); }
        }

        // Top chi cà phê
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT TOP 15 TenKhachHang, N'DVD' AS Kho,
                    SUM(CAST(ThanhTien AS bigint)) AS TongTien, COUNT(*) AS SoPhieu, SUM(TrongLuongHang) AS TongKg
                FROM nhapCaPheSi
                GROUP BY TenKhachHang ORDER BY TongTien DESC";
            var list = new List<FinancialCustomerRow>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new FinancialCustomerRow { TenKhachHang = r.GetString(0), Kho = r.GetString(1), TongTien = r.GetInt64(2), SoPhieu = r.GetInt32(3), TongKg = r.IsDBNull(4) ? 0 : r.GetDecimal(4) });
            vm.TopChiCaPhe = list;
        }

        // Top chi tiêu
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT TOP 15 TenKhachHang, N'DVD' AS Kho,
                    SUM(CAST(ThanhTien AS bigint)) AS TongTien, COUNT(*) AS SoPhieu, SUM(TrongLuongHang) AS TongKg
                FROM nhapTieuKhachSi
                GROUP BY TenKhachHang ORDER BY TongTien DESC";
            var list = new List<FinancialCustomerRow>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new FinancialCustomerRow { TenKhachHang = r.GetString(0), Kho = r.GetString(1), TongTien = r.GetInt64(2), SoPhieu = r.GetInt32(3), TongKg = r.IsDBNull(4) ? 0 : r.GetDecimal(4) });
            vm.TopChiTieu = list;
        }

        // Chuyển khoản gần nhất
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT TOP 30 orderID, TenKhachHang, ISNULL(LoaiHang,''), SoLuong, ThanhTien,
                    ISNULL(TinhTrang,''), NgayTao, ISNULL(NguoiDeNghi,''), ISNULL(TenNganHang,'')
                FROM ChuyenKhoan ORDER BY NgayTao DESC";
            var list = new List<ChuyenKhoanRow>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new ChuyenKhoanRow
                {
                    OrderID       = r.GetInt32(0),
                    TenKhachHang  = r.GetString(1),
                    LoaiHang      = r.GetString(2),
                    SoLuong       = r.IsDBNull(3) ? 0 : r.GetDecimal(3),
                    ThanhTien     = r.IsDBNull(4) ? 0 : r.GetDecimal(4),
                    TinhTrang     = r.GetString(5),
                    NgayTao       = r.GetDateTime(6),
                    NguoiDeNghi   = r.GetString(7),
                    TenNganHang   = r.GetString(8)
                });
            vm.RecentChuyenKhoan = list;
        }

        return vm;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BÁO CÁO HÀNH VI
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<CustomerBehaviorViewModel> GetBehaviorAsync()
    {
        await using var cn = Open();
        await cn.OpenAsync();
        var vm = new CustomerBehaviorViewModel();

        // Tần suất mua hàng (coffee)
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT
                    CASE WHEN SoPhieu <= 5 THEN '1-5 lần' WHEN SoPhieu <= 20 THEN '6-20 lần' WHEN SoPhieu <= 50 THEN '21-50 lần' ELSE '50+ lần' END AS Nhom,
                    COUNT(*) AS SoKhach
                FROM (SELECT TenKhachHang, COUNT(*) AS SoPhieu FROM nhapCaPheSi GROUP BY TenKhachHang) t
                GROUP BY CASE WHEN SoPhieu <= 5 THEN '1-5 lần' WHEN SoPhieu <= 20 THEN '6-20 lần' WHEN SoPhieu <= 50 THEN '21-50 lần' ELSE '50+ lần' END";
            var colors = new[] { "#3b82f6", "#10b981", "#f59e0b", "#8b5cf6" };
            var list = new List<FrequencyRow>();
            await using var r = await cmd.ExecuteReaderAsync();
            int i = 0;
            while (await r.ReadAsync())
                list.Add(new FrequencyRow { NhomTanSuat = r.GetString(0), SoKhach = r.GetInt32(1), MauSac = colors[i++ % colors.Length] });
            vm.TanSuatMuaHang = list;
        }

        // Giao dịch theo ngày trong tuần
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT DATENAME(WEEKDAY, ngayNhap) AS NgayTuan, COUNT(*) AS SoGD
                FROM nhapCaPheSi
                WHERE ngayNhap >= DATEADD(MONTH,-3,GETDATE())
                GROUP BY DATENAME(WEEKDAY, ngayNhap), DATEPART(WEEKDAY, ngayNhap)
                ORDER BY DATEPART(WEEKDAY, ngayNhap)";
            var list = new List<DayOfWeekStat>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new DayOfWeekStat { Ngay = r.GetString(0), SoGiaoDich = r.GetInt32(1) });
            vm.GiaoDichTheoNgay = list;
        }

        // Khách quay lại nhiều nhất
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT TOP 15 TenKhachHang, COUNT(*) AS TongLan, MIN(ngayNhap) AS LanDau, MAX(ngayNhap) AS LanCuoi
                FROM nhapCaPheSi GROUP BY TenKhachHang HAVING COUNT(*) > 3
                ORDER BY TongLan DESC";
            var list = new List<ReturnCustomerRow>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new ReturnCustomerRow
                {
                    TenKhachHang = r.GetString(0),
                    TongLanMua   = r.GetInt32(1),
                    LanDauMua    = r.GetDateTime(2),
                    LanCuoiMua   = r.GetDateTime(3)
                });
            vm.KhachQuayLai = list;
        }

        return vm;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NHẬP HÔM NAY
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<TodayEntryViewModel> GetTodayEntriesAsync(string filterKho, string filterSanPham)
    {
        var vm = new TodayEntryViewModel { FilterKho = filterKho, FilterSanPham = filterSanPham };
        var rows = new List<TodayEntryRow>();

        // ── nguyengiahuy ─────────────────────────────────────────────────────
        await using (var cn = Open())
        {
            await cn.OpenAsync();

            // Xây danh sách các (query, kho, sanPham, loai) cần chạy
            var sources = new List<(string sql, string kho, string sanPham, string loai)>();

            bool wantDvd = filterKho == "all" || filterKho == "dvd";
            bool wantTD  = filterKho == "all" || filterKho == "thongdao";
            bool wantCF  = filterSanPham == "all" || filterSanPham == "caphe";
            bool wantT   = filterSanPham == "all" || filterSanPham == "tieu";

            // SQL template: 0=ngayNhap,1=TenKhachHang,2=Sdt,3=DiaChi,4=QuyChuan,
            //              5=ThanhTien,6=PhuongThuc,7=orderID,
            //              8=GioiTinh,9=CCCD,10=NgaySinh,11=NgayCap
            static string WithKH(string tbl, string sdtCol = "Sdt") => $@"
                SELECT d.ngayNhap,
                       ISNULL(d.TenKhachHang,'') AS TenKhachHang,
                       ISNULL(d.{sdtCol},'')     AS Sdt,
                       ISNULL(d.DiaChi,'')        AS DiaChi,
                       ISNULL(d.QuyChuan,0)       AS QuyChuan,
                       CAST(ISNULL(d.ThanhTien,0) AS bigint) AS ThanhTien,
                       ISNULL(d.ChuyenKhoan,'')   AS CK,
                       ISNULL(d.orderID,0)        AS orderID,
                       ISNULL(k.GioiTinh,'')      AS GioiTinh,
                       ISNULL(k.CCCD,'')           AS CCCD,
                       k.NgaySinh,
                       k.NgayCap
                FROM {tbl} d
                LEFT JOIN KhachHang k ON k.SDT = d.{sdtCol}
                WHERE CAST(d.ngayNhap AS date) = CAST(GETDATE() AS date)";

            if (wantDvd && wantCF)
            {
                sources.Add((WithKH("duongvandung"),   "Dương Văn Dũng", "Cà phê", "Lẻ"));
                sources.Add((WithKH("nhapCaPheSi"),    "Dương Văn Dũng", "Cà phê", "Sỉ"));
            }

            if (wantDvd && wantT)
            {
                sources.Add((WithKH("xntTieu"),        "Dương Văn Dũng", "Tiêu", "Lẻ"));
                sources.Add((WithKH("nhapTieuKhachSi"),"Dương Văn Dũng", "Tiêu", "Sỉ"));
            }

            if (wantTD && wantCF)
                sources.Add((WithKH("NhapCaPheThongDao"), "Thông Đào", "Cà phê", "Sỉ"));

            if (wantTD && wantT)
                sources.Add((WithKH("NhapTieuThongDao"),  "Thông Đào", "Tiêu", "Sỉ"));

            foreach (var (sql, kho, sanPham, loai) in sources)
            {
                await using var cmd = cn.CreateCommand();
                cmd.CommandText = sql;
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    rows.Add(new TodayEntryRow
                    {
                        NgayNhap     = r.GetDateTime(0),
                        TenKhachHang = r.GetString(1),
                        SDT          = r.GetString(2).Trim(),
                        DiaChi       = r.GetString(3),
                        QuyChuan     = r.GetDecimal(4),
                        ThanhTien    = r.GetInt64(5),
                        PhuongThuc   = r.GetString(6).Trim(),
                        OrderID      = r.GetInt32(7),
                        GioiTinh     = r.GetString(8),
                        CCCD         = r.GetString(9),
                        NgaySinh     = r.IsDBNull(10) ? null : r.GetDateTime(10),
                        NgayCap      = r.IsDBNull(11) ? null : r.GetDateTime(11),
                        Kho          = kho,
                        SanPham      = sanPham,
                        LoaiNhap     = loai
                    });
            }
        }

        // ── KhoYmoal ─────────────────────────────────────────────────────────
        bool wantYmoal = filterKho == "all" || filterKho == "ymoal";
        if (wantYmoal)
        {
            await using var cny = OpenYmoal();
            await cny.OpenAsync();

            bool wantCF2 = filterSanPham == "all" || filterSanPham == "caphe";
            bool wantT2  = filterSanPham == "all" || filterSanPham == "tieu";

            var ymoalSources = new List<(string sql, string sanPham, string loai)>();

            // Ymoal: mỗi bảng có cấu trúc hơi khác nhau về DiaChi
            // NhapCaPheSi không có cột DiaChi → dùng N'' thay thế
            static string YmoalQ(string tbl, bool hasDiaChi = true, string sdtCol = "Sdt") => $@"
                SELECT ngayNhap,
                       ISNULL(TenKhachHang,'') AS TenKhachHang,
                       ISNULL(CAST({sdtCol} AS nvarchar(30)),'') AS Sdt,
                       {(hasDiaChi ? "ISNULL(DiaChi,'')" : "N''")} AS DiaChi,
                       ISNULL(QuyChuan,0) AS QuyChuan,
                       CAST(ISNULL(ThanhTien,0) AS bigint) AS ThanhTien,
                       ISNULL(ChuyenKhoan,'') AS CK,
                       ISNULL(orderID,0) AS orderID
                FROM {tbl}
                WHERE CAST(ngayNhap AS date) = CAST(GETDATE() AS date)";

            if (wantCF2)
            {
                ymoalSources.Add((YmoalQ("NhapCaPheLe", hasDiaChi: true),  "Cà phê", "Lẻ"));
                ymoalSources.Add((YmoalQ("NhapCaPheSi", hasDiaChi: false), "Cà phê", "Sỉ"));
            }

            if (wantT2)
            {
                ymoalSources.Add((YmoalQ("NhapTieuLe", hasDiaChi: true), "Tiêu", "Lẻ"));
                ymoalSources.Add((YmoalQ("NhapTieuSi", hasDiaChi: true), "Tiêu", "Sỉ"));
            }

            foreach (var (sql, sanPham, loai) in ymoalSources)
            {
                await using var cmd = cny.CreateCommand();
                cmd.CommandText = sql;
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    rows.Add(new TodayEntryRow
                    {
                        NgayNhap     = r.GetDateTime(0),
                        TenKhachHang = r.GetString(1),
                        SDT          = r.GetString(2).Trim(),
                        DiaChi       = r.GetString(3),
                        QuyChuan     = r.GetDecimal(4),
                        ThanhTien    = r.GetInt64(5),
                        PhuongThuc   = r.GetString(6).Trim(),
                        OrderID      = r.GetInt32(7),
                        GioiTinh     = "",
                        CCCD         = "",
                        NgaySinh     = null,
                        NgayCap      = null,
                        Kho          = "Ymoal",
                        SanPham      = sanPham,
                        LoaiNhap     = loai
                    });
            }
        }

        // Sắp xếp theo giờ nhập giảm dần
        rows.Sort((a, b) => b.NgayNhap.CompareTo(a.NgayNhap));
        vm.Rows = rows;

        // Tổng hợp
        vm.TongKgCaPhe   = rows.Where(r => r.SanPham == "Cà phê").Sum(r => r.QuyChuan);
        vm.TongKgTieu    = rows.Where(r => r.SanPham == "Tiêu").Sum(r => r.QuyChuan);
        vm.TongTienCaPhe = rows.Where(r => r.SanPham == "Cà phê").Sum(r => r.ThanhTien);
        vm.TongTienTieu  = rows.Where(r => r.SanPham == "Tiêu").Sum(r => r.ThanhTien);

        return vm;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────
    private static KhachHangRecord MapKhachSi(SqlDataReader r) => new()
    {
        // col: 0=CustomerID, 1=TenKhachHang, 2=GioiTinh, 3=NgaySinh,
        //      4=SDT, 5=CCCD, 6=NgayCap, 7=DiaChi,
        //      8=NgayTaoTaiKhoan, 9=GhiChu,
        //      10=TongChiCaPhe, 11=SoPhieuCaPhe, 12=TongChiTieu, 13=SoPhieuTieu
        CustomerID      = r.GetInt32(0),
        TenKhachHang    = r.IsDBNull(1)  ? "" : r.GetString(1),
        GioiTinh        = r.IsDBNull(2)  ? "" : r.GetString(2),
        NgaySinh        = r.IsDBNull(3)  ? null : r.GetDateTime(3),
        SDT             = r.IsDBNull(4)  ? "" : r.GetString(4),
        CCCD            = r.IsDBNull(5)  ? "" : r.GetString(5),
        NgayCap         = r.IsDBNull(6)  ? null : r.GetDateTime(6),
        DiaChi          = r.IsDBNull(7)  ? "" : r.GetString(7),
        NgayTaoTaiKhoan = r.IsDBNull(8)  ? null : r.GetDateTime(8),
        GhiChu          = r.IsDBNull(9)  ? "" : r.GetString(9),
        TongChiCaPhe    = r.IsDBNull(10) ? 0 : r.GetInt64(10),
        SoPhieuCaPhe    = r.IsDBNull(11) ? 0 : r.GetInt32(11),
        TongChiTieu     = r.IsDBNull(12) ? 0 : r.GetInt64(12),
        SoPhieuTieu     = r.IsDBNull(13) ? 0 : r.GetInt32(13)
    };

    private static KhachLeRecord MapKhachLe(SqlDataReader r) => new()
    {
        // col: 0=CustomerID, 1=TenKhachHang, 2=SDT, 3=CCCD, 4=Diachi,
        //      5=NgaySinh, 6=NgayTao, 7=email, 8=notes
        CustomerID   = r.IsDBNull(0) ? "" : r.GetString(0),
        TenKhachHang = r.IsDBNull(1) ? "" : r.GetString(1),
        SDT          = r.IsDBNull(2) ? "" : r.GetString(2),
        CCCD         = r.IsDBNull(3) ? "" : r.GetString(3),
        DiaChi       = r.IsDBNull(4) ? "" : r.GetString(4),
        NgaySinh     = r.IsDBNull(5) ? null : r.GetDateTime(5),
        NgayTao      = r.IsDBNull(6) ? null : r.GetDateTime(6),
        Email        = r.IsDBNull(7) ? "" : r.GetString(7),
        Notes        = r.IsDBNull(8) ? "" : r.GetString(8)
    };
}
