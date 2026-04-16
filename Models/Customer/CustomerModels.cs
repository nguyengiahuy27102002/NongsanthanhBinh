namespace DuongVanDung.WebApp.Models.Customer;

// ─── Khách sỉ ────────────────────────────────────────────────────────────────
public sealed class KhachHangRecord
{
    public int CustomerID { get; set; }
    public string TenKhachHang { get; set; } = string.Empty;
    public string GioiTinh { get; set; } = string.Empty;
    public DateTime? NgaySinh { get; set; }
    public string SDT { get; set; } = string.Empty;
    public string CCCD { get; set; } = string.Empty;
    public DateTime? NgayCap { get; set; }
    public string DiaChi { get; set; } = string.Empty;
    public DateTime? NgayTaoTaiKhoan { get; set; }
    public string GhiChu { get; set; } = string.Empty;
    // thống kê tổng hợp
    public long TongChiCaPhe { get; set; }
    public long TongChiTieu { get; set; }
    public int SoPhieuCaPhe { get; set; }
    public int SoPhieuTieu { get; set; }
}

// ─── Khách lẻ ────────────────────────────────────────────────────────────────
public sealed class KhachLeRecord
{
    public string CustomerID { get; set; } = string.Empty;
    public string TenKhachHang { get; set; } = string.Empty;
    public string SDT { get; set; } = string.Empty;
    public string CCCD { get; set; } = string.Empty;
    public string DiaChi { get; set; } = string.Empty;
    public DateTime? NgaySinh { get; set; }
    public DateTime? NgayTao { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

// ─── Nhập hôm nay ────────────────────────────────────────────────────────────
public sealed class TodayEntryRow
{
    public string    TenKhachHang { get; set; } = string.Empty;
    public string    GioiTinh     { get; set; } = string.Empty;
    public string    SDT          { get; set; } = string.Empty;
    public string    CCCD         { get; set; } = string.Empty;
    public string    DiaChi       { get; set; } = string.Empty;
    public DateTime? NgaySinh     { get; set; }
    public DateTime? NgayCap      { get; set; }
    public string    Kho          { get; set; } = string.Empty;   // "Dương Văn Dũng" | "Thông Đào" | "Ymoal"
    public string    SanPham      { get; set; } = string.Empty;   // "Cà phê" | "Tiêu"
    public string    LoaiNhap     { get; set; } = string.Empty;   // "Lẻ" | "Sỉ"
    public DateTime  NgayNhap     { get; set; }
    public decimal   QuyChuan     { get; set; }
    public long      ThanhTien    { get; set; }
    public string    PhuongThuc   { get; set; } = string.Empty;   // "CK" | "TM" | ""
    public int       OrderID      { get; set; }
}

public sealed class TodayEntryViewModel
{
    public IReadOnlyList<TodayEntryRow> Rows     { get; set; } = Array.Empty<TodayEntryRow>();
    public string FilterKho      { get; set; } = "all";
    public string FilterSanPham  { get; set; } = "all";
    // tổng hợp
    public decimal TongKgCaPhe   { get; set; }
    public decimal TongKgTieu    { get; set; }
    public long    TongTienCaPhe { get; set; }
    public long    TongTienTieu  { get; set; }
    public int     SoPhieu       => Rows.Count;
    // helper
    public long    TongTien      => TongTienCaPhe + TongTienTieu;
    public decimal TongKg        => TongKgCaPhe + TongKgTieu;
}

// ─── Danh sách khách hàng ────────────────────────────────────────────────────
public sealed class CustomerListViewModel
{
    public IReadOnlyList<KhachHangRecord> Customers { get; set; } = Array.Empty<KhachHangRecord>();
    public int Total { get; set; }
    public string Search { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
}

// ─── Chi tiết khách hàng ────────────────────────────────────────────────────
public sealed class TransactionRow
{
    public DateTime NgayNhap  { get; set; }
    public decimal  QuyChuan  { get; set; }
    public long     ThanhTien { get; set; }
    public string   PhuongThuc { get; set; } = string.Empty;
    public string   Kho       { get; set; } = string.Empty;
    public string   LoaiNhap  { get; set; } = string.Empty;
    public string   SanPham   { get; set; } = string.Empty;
}

public sealed class CustomerDetailViewModel
{
    public KhachHangRecord Customer { get; set; } = new();
    public IReadOnlyList<TransactionRow> Transactions { get; set; } = Array.Empty<TransactionRow>();
    public long    TongTienCaPhe => Transactions.Where(t => t.SanPham == "Cà phê").Sum(t => t.ThanhTien);
    public long    TongTienTieu  => Transactions.Where(t => t.SanPham == "Tiêu").Sum(t => t.ThanhTien);
    public decimal TongKgCaPhe   => Transactions.Where(t => t.SanPham == "Cà phê").Sum(t => t.QuyChuan);
    public decimal TongKgTieu    => Transactions.Where(t => t.SanPham == "Tiêu").Sum(t => t.QuyChuan);
    public int     SoPhieuCaPhe  => Transactions.Count(t => t.SanPham == "Cà phê");
    public int     SoPhieuTieu   => Transactions.Count(t => t.SanPham == "Tiêu");
    public long    TongTien      => TongTienCaPhe + TongTienTieu;
    public int     TotalPhieu    => Transactions.Count;
}

// ─── Tìm trùng ──────────────────────────────────────────────────────────────
public sealed class DuplicateGroup
{
    public string LoaiTrung { get; set; } = string.Empty; // "SDT" | "CCCD"
    public string GiaTri    { get; set; } = string.Empty;
    public IReadOnlyList<KhachHangRecord> Customers { get; set; } = Array.Empty<KhachHangRecord>();
}

public sealed class DuplicateListViewModel
{
    public IReadOnlyList<DuplicateGroup> Groups { get; set; } = Array.Empty<DuplicateGroup>();
    public int TotalGroups    => Groups.Count;
    public int TotalCustomers => Groups.Sum(g => g.Customers.Count);
}

// ─── Tìm khách hàng ──────────────────────────────────────────────────────────
public sealed class CustomerSearchViewModel
{
    public string Keyword { get; set; } = string.Empty;
    public string SearchBy { get; set; } = "ten"; // "ten" | "sdt" | "cccd" | "diachi"
    public IReadOnlyList<KhachHangRecord> Results { get; set; } = Array.Empty<KhachHangRecord>();
    public bool HasSearched { get; set; }
}

// ─── Báo cáo phân tích ───────────────────────────────────────────────────────
public sealed class CustomerAnalysisViewModel
{
    public IReadOnlyList<TopCustomerRow> TopCaPhe { get; set; } = Array.Empty<TopCustomerRow>();
    public IReadOnlyList<TopCustomerRow> TopTieu { get; set; } = Array.Empty<TopCustomerRow>();
    public IReadOnlyList<MonthlyTrend> MonthlyCaPhe { get; set; } = Array.Empty<MonthlyTrend>();
    public IReadOnlyList<MonthlyTrend> MonthlyTieu { get; set; } = Array.Empty<MonthlyTrend>();
    public IReadOnlyList<RegionStat> RegionStats { get; set; } = Array.Empty<RegionStat>();
    public int TongKhachSi { get; set; }
    public int TongKhachLe { get; set; }
    public int KhachMoiThangNay { get; set; }
}

public sealed class TopCustomerRow
{
    public string TenKhachHang { get; set; } = string.Empty;
    public string CustomerID { get; set; } = string.Empty;
    public long TongTien { get; set; }
    public decimal TongKg { get; set; }
    public int SoPhieu { get; set; }
}

public sealed class MonthlyTrend
{
    public int Nam { get; set; }
    public int Thang { get; set; }
    public int SoPhieu { get; set; }
    public decimal TongKg { get; set; }
    public string Label => $"T{Thang}/{Nam}";
}

public sealed class RegionStat
{
    public string KhuVuc { get; set; } = string.Empty;
    public int SoKhach { get; set; }
    public decimal TongKg { get; set; }
    public decimal TongKgCaPhe { get; set; }
    public decimal TongKgTieu { get; set; }
}

// ─── Báo cáo tài chính ───────────────────────────────────────────────────────
public sealed class CustomerFinancialViewModel
{
    public IReadOnlyList<FinancialCustomerRow> TopChiCaPhe { get; set; } = Array.Empty<FinancialCustomerRow>();
    public IReadOnlyList<FinancialCustomerRow> TopChiTieu { get; set; } = Array.Empty<FinancialCustomerRow>();
    public IReadOnlyList<ChuyenKhoanRow> RecentChuyenKhoan { get; set; } = Array.Empty<ChuyenKhoanRow>();
    public IReadOnlyList<PaymentMethodStat> PaymentStats { get; set; } = Array.Empty<PaymentMethodStat>();
    public long TongChiThangNay { get; set; }
    public long TongChuyen { get; set; }
    public int SoGiaoDich { get; set; }
}

public sealed class FinancialCustomerRow
{
    public string TenKhachHang { get; set; } = string.Empty;
    public string Kho { get; set; } = string.Empty;
    public long TongTien { get; set; }
    public int SoPhieu { get; set; }
    public decimal TongKg { get; set; }
}

public sealed class ChuyenKhoanRow
{
    public int OrderID { get; set; }
    public string TenKhachHang { get; set; } = string.Empty;
    public string LoaiHang { get; set; } = string.Empty;
    public decimal SoLuong { get; set; }
    public decimal ThanhTien { get; set; }
    public string TinhTrang { get; set; } = string.Empty;
    public DateTime NgayTao { get; set; }
    public string NguoiDeNghi { get; set; } = string.Empty;
    public string TenNganHang { get; set; } = string.Empty;
}

public sealed class PaymentMethodStat
{
    public string PhuongThuc { get; set; } = string.Empty;
    public long TongTien { get; set; }
    public int SoLan { get; set; }
}

// ─── Báo cáo hành vi ─────────────────────────────────────────────────────────
public sealed class CustomerBehaviorViewModel
{
    public IReadOnlyList<FrequencyRow> TanSuatMuaHang { get; set; } = Array.Empty<FrequencyRow>();
    public IReadOnlyList<ProductPreferenceRow> SoThichSanPham { get; set; } = Array.Empty<ProductPreferenceRow>();
    public IReadOnlyList<DayOfWeekStat> GiaoDichTheoNgay { get; set; } = Array.Empty<DayOfWeekStat>();
    public IReadOnlyList<HourStat> GiaoDichTheoGio { get; set; } = Array.Empty<HourStat>();
    public IReadOnlyList<ReturnCustomerRow> KhachQuayLai { get; set; } = Array.Empty<ReturnCustomerRow>();
}

public sealed class FrequencyRow
{
    public string NhomTanSuat { get; set; } = string.Empty; // "1-5", "6-20", "21+"
    public int SoKhach { get; set; }
    public string MauSac { get; set; } = string.Empty;
}

public sealed class ProductPreferenceRow
{
    public string TenKhachHang { get; set; } = string.Empty;
    public int SoPhieuCaPhe { get; set; }
    public int SoPhieuTieu { get; set; }
    public string UuTien => SoPhieuCaPhe >= SoPhieuTieu ? "Cà phê" : "Tiêu";
}

public sealed class DayOfWeekStat
{
    public string Ngay { get; set; } = string.Empty;
    public int SoGiaoDich { get; set; }
}

public sealed class HourStat
{
    public int Gio { get; set; }
    public int SoGiaoDich { get; set; }
}

public sealed class ReturnCustomerRow
{
    public string TenKhachHang { get; set; } = string.Empty;
    public int TongLanMua { get; set; }
    public DateTime LanDauMua { get; set; }
    public DateTime LanCuoiMua { get; set; }
    public int SoNgayHoatDong => (int)(LanCuoiMua - LanDauMua).TotalDays + 1;
}
