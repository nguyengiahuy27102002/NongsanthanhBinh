using DuongVanDung.WebApp.Helpers;
using DuongVanDung.WebApp.Services.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DuongVanDung.WebApp.Controllers;

[Authorize]
public class CustomerController : Controller
{
    private readonly ICustomerService _svc;
    public CustomerController(ICustomerService svc) => _svc = svc;

    public async Task<IActionResult> Index(string search = "", int page = 1)
    {
        var vm = await _svc.GetListAsync(search, page, 50);
        return View(vm);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var vm = await _svc.GetDetailAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    public async Task<IActionResult> Duplicates()
    {
        var vm = await _svc.FindDuplicatesAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MergeCustomers(int keepId, int deleteId)
    {
        await _svc.MergeCustomersAsync(keepId, deleteId);
        TempData["Success"] = $"Đã gộp khách hàng #{deleteId} vào #{keepId}.";
        return RedirectToAction(nameof(Duplicates));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        await _svc.DeleteCustomerAsync(id);
        TempData["Success"] = $"Đã xóa khách hàng #{id}.";
        return RedirectToAction(nameof(Duplicates));
    }

    public async Task<IActionResult> Search(string keyword = "", string searchBy = "ten")
    {
        var vm = await _svc.SearchAsync(keyword, searchBy);
        return View(vm);
    }

    public async Task<IActionResult> Analysis()
    {
        var vm = await _svc.GetAnalysisAsync();
        return View(vm);
    }

    public async Task<IActionResult> Financial()
    {
        var vm = await _svc.GetFinancialAsync();
        return View(vm);
    }

    public async Task<IActionResult> Behavior()
    {
        var vm = await _svc.GetBehaviorAsync();
        return View(vm);
    }

    public async Task<IActionResult> TodayEntries(string filterKho = "all", string filterSanPham = "all")
    {
        var vm = await _svc.GetTodayEntriesAsync(filterKho, filterSanPham);
        return View(vm);
    }

    // ── Xuất Excel: Danh sách khách hàng ─────────────────────────────────────
    public async Task<IActionResult> ExportCustomers(string search = "")
    {
        var vm = await _svc.GetListAsync(search, 1, 9999);
        var headers = new[] { "STT", "ID", "Tên khách hàng", "Giới tính", "Ngày sinh", "SĐT", "CCCD", "Ngày cấp", "Địa chỉ", "Phiếu cà phê", "Phiếu tiêu", "Tổng tiền cà (đ)", "Tổng tiền tiêu (đ)", "Ngày tạo" };
        var rows = vm.Customers.Select((k, i) => new[]
        {
            (i+1).ToString(),
            k.CustomerID.ToString(),
            k.TenKhachHang,
            k.GioiTinh,
            k.NgaySinh.HasValue ? k.NgaySinh.Value.ToString("dd/MM/yyyy") : "",
            k.SDT,
            k.CCCD,
            k.NgayCap.HasValue ? k.NgayCap.Value.ToString("dd/MM/yyyy") : "",
            k.DiaChi,
            k.SoPhieuCaPhe.ToString(),
            k.SoPhieuTieu.ToString(),
            k.TongChiCaPhe.ToString("#,0"),
            k.TongChiTieu.ToString("#,0"),
            k.NgayTaoTaiKhoan.HasValue ? k.NgayTaoTaiKhoan.Value.ToString("dd/MM/yyyy") : ""
        });
        var bytes = ExcelHelper.Build("Khách hàng", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"khachhang-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    // ── Xuất Excel: Nhập hôm nay ─────────────────────────────────────────────
    public async Task<IActionResult> ExportTodayEntries(string filterKho = "all", string filterSanPham = "all")
    {
        var vm = await _svc.GetTodayEntriesAsync(filterKho, filterSanPham);
        var headers = new[] { "STT", "Ngày nhập", "Kho", "Sản phẩm", "Loại", "Tên", "Giới tính", "SĐT", "CCCD", "Địa chỉ", "Ngày sinh", "Ngày cấp CCCD", "Số lượng (kg)", "Thành tiền (đ)", "Thanh toán" };
        var rows = vm.Rows.Select((r, i) => new[]
        {
            (i+1).ToString(),
            r.NgayNhap.ToString("dd/MM/yyyy HH:mm"),
            r.Kho,
            r.SanPham,
            r.LoaiNhap,
            r.TenKhachHang,
            r.GioiTinh,
            r.SDT,
            r.CCCD,
            r.DiaChi,
            r.NgaySinh.HasValue ? r.NgaySinh.Value.ToString("dd/MM/yyyy") : "",
            r.NgayCap.HasValue ? r.NgayCap.Value.ToString("dd/MM/yyyy") : "",
            r.QuyChuan.ToString("0.##"),
            r.ThanhTien.ToString("#,0"),
            r.PhuongThuc
        });
        var bytes = ExcelHelper.Build("Nhập hôm nay", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"nhap-homnay-{DateTime.Now:yyyyMMdd}.xlsx");
    }
}
