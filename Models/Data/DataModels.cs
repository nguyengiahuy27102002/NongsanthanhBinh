namespace DuongVanDung.WebApp.Models.Data;

public sealed class DataFilter
{
    public string  Kho       { get; set; } = "";
    public string  Bang      { get; set; } = "";
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo   { get; set; }
    public long?   MinAmount  { get; set; }   // null = tất cả
    public int     Page       { get; set; } = 1;

    public const int PageSize = 100;

    public string DateFromStr => DateFrom?.ToString("yyyy-MM-dd") ?? "";
    public string DateToStr   => DateTo?.ToString("yyyy-MM-dd") ?? "";
}

public sealed class DataEntryRow
{
    public DateTime NgayNhap       { get; set; }
    public string   TenKhachHang   { get; set; } = "";
    public string   SDT            { get; set; } = "";
    public string   DiaChi         { get; set; } = "";
    public decimal  SoLuong        { get; set; }
    public decimal  DoAm           { get; set; }
    public decimal  TapChat        { get; set; }
    public decimal? Dem            { get; set; }      // chỉ có bên tiêu
    public decimal  TrongLuongTruBi { get; set; }
    public int      SoBao          { get; set; }
    public decimal  QuyChuan       { get; set; }
    public long     ThanhTien      { get; set; }
    public string   PhuongThuc     { get; set; } = "";  // "yes" = CK, khác = TM
}

public sealed class DataTableViewModel
{
    public string  Kho         { get; set; } = "";
    public string  Bang        { get; set; } = "";
    public string  KhoDisplay  { get; set; } = "";
    public string  BangDisplay { get; set; } = "";
    public string  Theme       { get; set; } = "blue"; // "blue" | "green" | "red" | "yellow" | "purple"
    public bool    TableExists { get; set; } = true;
    public bool    ShowDem     { get; set; }            // hiện cột Đếm cho bảng tiêu
    public bool    IsXuat      { get; set; }            // bảng xuất (khác label: Tài xế, Điểm xuất...)

    public IReadOnlyList<DataEntryRow> Rows { get; set; } = Array.Empty<DataEntryRow>();
    public int     Total      { get; set; }
    public int     TotalPages => Total == 0 ? 1 : (int)Math.Ceiling(Total / (double)DataFilter.PageSize);
    public DataFilter Filter  { get; set; } = new();

    // Tổng hợp (chỉ trên trang hiện tại)
    public decimal TongKgPage   => Rows.Sum(r => r.QuyChuan);
    public long    TongTienPage => Rows.Sum(r => r.ThanhTien);

    // Tổng toàn bộ kết quả (sau filter)
    public decimal TongSoLuongAll { get; set; }
    public decimal TongKgAll      { get; set; }
    public long    TongTienAll    { get; set; }
}
