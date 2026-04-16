using DuongVanDung.WebApp.Helpers;
using DuongVanDung.WebApp.Models.Data;
using DuongVanDung.WebApp.Services.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DuongVanDung.WebApp.Controllers;

[Authorize]
public class DataController : Controller
{
    private readonly IDataService _svc;
    public DataController(IDataService svc) => _svc = svc;

    // ── Trang chọn kho ────────────────────────────────────────────────────────
    public IActionResult Index() => View();

    // ── Bảng dữ liệu có filter + phân trang ──────────────────────────────────
    public async Task<IActionResult> Table(
        string kho   = "dvd",
        string bang  = "caphe-le",
        string? dateFrom = null,
        string? dateTo   = null,
        long minAmount   = 0,
        int page         = 1)
    {
        var filter = new DataFilter
        {
            Kho       = kho,
            Bang      = bang,
            DateFrom  = DateTime.TryParse(dateFrom, out var df) ? df : null,
            DateTo    = DateTime.TryParse(dateTo,   out var dt) ? dt : null,
            MinAmount = minAmount > 0 ? minAmount : null,
            Page      = Math.Max(1, page)
        };

        var vm = await _svc.GetTableAsync(filter);
        return View(vm);
    }

    // ── Xuất Excel ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> ExportExcel(
        string kho   = "dvd",
        string bang  = "caphe-le",
        string? dateFrom = null,
        string? dateTo   = null,
        long minAmount   = 0)
    {
        var filter = new DataFilter
        {
            Kho       = kho,
            Bang      = bang,
            DateFrom  = DateTime.TryParse(dateFrom, out var df) ? df : null,
            DateTo    = DateTime.TryParse(dateTo,   out var dt) ? dt : null,
            MinAmount = minAmount > 0 ? minAmount : null
        };

        var (khoDisplay, bangDisplay, hasDem, isXuat, rows) = await _svc.GetAllRowsAsync(filter);

        var nameLabel = isXuat ? "Tên tài xế" : "Tên khách hàng";
        var addrLabel = isXuat ? "Điểm xuất hàng" : "Địa chỉ";
        var dateLabel = isXuat ? "Ngày xuất" : "Ngày nhập";

        var headerList = new List<string>
        {
            "STT", dateLabel, nameLabel, "Số điện thoại", addrLabel,
            "Số lượng (kg)", "Độ ẩm", "Tạp chất"
        };
        if (hasDem) headerList.Add("Đếm");
        headerList.AddRange(new[] { "TL trừ bì (kg)", "Số bao", "Quy chuẩn (kg)" });
        if (!isXuat) headerList.Add("Thành tiền (đ)");
        if (!isXuat) headerList.Add("Thanh toán");

        var dataRows = rows.Select((r, i) =>
        {
            var cols = new List<string>
            {
                (i + 1).ToString(),
                r.NgayNhap.ToString("dd/MM/yyyy"),
                r.TenKhachHang,
                r.SDT,
                r.DiaChi,
                r.SoLuong.ToString("0.##"),
                r.DoAm.ToString("0.##"),
                r.TapChat.ToString("0.##")
            };
            if (hasDem) cols.Add(r.Dem?.ToString("0.##") ?? "");
            cols.Add(r.TrongLuongTruBi.ToString("0.##"));
            cols.Add(r.SoBao.ToString());
            cols.Add(r.QuyChuan.ToString("0.##"));

            if (!isXuat)
            {
                cols.Add(r.ThanhTien.ToString("#,0"));
                var ck = r.PhuongThuc.Trim().ToLowerInvariant();
                cols.Add(ck == "yes" || ck == "có" || ck == "ck" ? "Chuyển khoản" : "Tiền mặt");
            }

            return cols.ToArray();
        });

        var bytes = ExcelHelper.Build($"{khoDisplay} - {bangDisplay}", headerList.ToArray(), dataRows);
        var fileName = $"{kho}-{bang}-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
