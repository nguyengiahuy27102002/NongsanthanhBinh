namespace DuongVanDung.WebApp.Models.Product;

// ─── Tồn kho ─────────────────────────────────────────────────────────────────
public sealed class TonKhoViewModel
{
    public IReadOnlyList<TonKhoRow> Rows { get; set; } = Array.Empty<TonKhoRow>();
    // Tổng hợp toàn hệ thống
    public decimal TongNhapCaPhe { get; set; }
    public decimal TongXuatCaPhe { get; set; }
    public decimal TonCaPhe      => TongNhapCaPhe - TongXuatCaPhe;
    public decimal TongNhapTieu  { get; set; }
    public decimal TongXuatTieu  { get; set; }
    public decimal TonTieu       => TongNhapTieu  - TongXuatTieu;
}

public sealed class TonKhoRow
{
    public string  Kho       { get; set; } = string.Empty;
    public string  SanPham   { get; set; } = string.Empty; // "Cà phê" | "Tiêu"
    public decimal TongNhap  { get; set; }
    public decimal TongXuat  { get; set; }
    public decimal TonKho    => TongNhap - TongXuat;
}

// ─── Nhập – Xuất – Tồn (Stock Movement) ──────────────────────────────────────
public sealed class StockMovementViewModel
{
    public IReadOnlyList<MovementRow> CaPhe { get; set; } = Array.Empty<MovementRow>();
    public IReadOnlyList<MovementRow> Tieu  { get; set; } = Array.Empty<MovementRow>();
    public int Days { get; set; } = 30;
}

public sealed class MovementRow
{
    public DateTime Ngay    { get; set; }
    public string   Kho     { get; set; } = string.Empty;
    public decimal  Nhap    { get; set; }
    public decimal  Xuat    { get; set; }
    public string   Label   => Ngay.ToString("dd/MM");
}

// ─── Chất lượng hàng hóa ─────────────────────────────────────────────────────
public sealed class QualityViewModel
{
    public IReadOnlyList<QualityRow> CaPhe { get; set; } = Array.Empty<QualityRow>();
    public IReadOnlyList<QualityRow> Tieu  { get; set; } = Array.Empty<QualityRow>();
    // Trung bình toàn hệ thống
    public decimal AvgDoAmCaPhe    { get; set; }
    public decimal AvgTapChatCaPhe { get; set; }
    public decimal AvgDoAmTieu     { get; set; }
    public decimal AvgTapChatTieu  { get; set; }
}

public sealed class QualityRow
{
    public string  TenKhachHang { get; set; } = string.Empty;
    public string  Kho          { get; set; } = string.Empty;
    public decimal AvgDoAm      { get; set; }
    public decimal AvgTapChat   { get; set; }
    public decimal TongKg       { get; set; }
    public int     SoPhieu      { get; set; }
}
