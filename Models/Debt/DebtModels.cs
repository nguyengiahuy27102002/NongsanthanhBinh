namespace DuongVanDung.WebApp.Models.Debt;

// ── Filter ───────────────────────────────────────────────────────────────────
public sealed class DebtFilter
{
    public string Product  { get; set; } = "";        // "" = all, "coffee", "pepper"
    public string Branch   { get; set; } = "";        // "" = all
    public string Search   { get; set; } = "";        // search by name/phone
    public string Sort     { get; set; } = "debt";    // "debt", "volume", "name", "recent"
    public int    Page     { get; set; } = 1;
    public const int PageSize = 50;
}

// ── KPI ──────────────────────────────────────────────────────────────────────
public sealed class DebtKpi
{
    public int     TotalCustomers     { get; set; }   // khách có gửi kho hoặc nợ
    public int     CustomersWithGoods { get; set; }   // khách có hàng gửi kho
    public int     CustomersWithDebt  { get; set; }   // khách còn nợ tiền

    public decimal TotalCoffeeKg     { get; set; }    // tổng cà phê gửi kho
    public decimal TotalPepperKg     { get; set; }    // tổng tiêu gửi kho
    public long    TotalDebtAmount   { get; set; }    // tổng tiền nợ (dương = khách nợ, âm = mình nợ khách)
    public long    TotalPrepaid      { get; set; }    // tổng trả trước
}

// ── Một dòng nợ từ bảng noCaPhe/noTieu ───────────────────────────────────────
public sealed class DebtTransaction
{
    public int      DebtId        { get; set; }
    public string   Product       { get; set; } = "";
    public string   Branch        { get; set; } = "";
    public DateTime Date          { get; set; }
    public DateTime? PaymentDate  { get; set; }
    public string   CustomerName  { get; set; } = "";
    public string   Phone         { get; set; } = "";
    public string   Address       { get; set; } = "";
    public decimal  Weight        { get; set; }        // TrongLuongHang
    public decimal  StandardQty   { get; set; }        // QuyChuan
    public decimal  Moisture      { get; set; }
    public decimal  Impurity      { get; set; }
    public decimal? Dem           { get; set; }
    public int      Price         { get; set; }
    public long     Amount        { get; set; }        // ThanhTien
    public long     Prepaid       { get; set; }        // TraTruoc
    public long     Remaining     { get; set; }        // ConNo
    public string   Note          { get; set; } = "";
}

// ── Tổng hợp công nợ theo khách hàng ────────────────────────────────────────
public sealed class CustomerDebtSummary
{
    public string  Name           { get; set; } = "";
    public string  Phone          { get; set; } = "";
    public string  Address        { get; set; } = "";

    // Hàng gửi kho (từ noCaPhe/noTieu)
    public decimal CoffeeInStore  { get; set; }        // kg cà phê gửi kho
    public decimal PepperInStore  { get; set; }        // kg tiêu gửi kho
    public decimal TotalInStore   => CoffeeInStore + PepperInStore;

    // Nợ tiền
    public long    DebtAmount     { get; set; }        // tổng ConNo (dương = khách nợ)
    public long    PrepaidAmount  { get; set; }        // tổng TraTruoc

    // Thống kê giao dịch (từ import tables)
    public int     TotalTransactions { get; set; }
    public decimal TotalImported  { get; set; }        // tổng kg đã nhập
    public long    TotalPaid      { get; set; }        // tổng tiền đã trả
    public DateTime? LastTransaction { get; set; }

    // Số phiếu nợ chi tiết
    public int     DebtRecordCount { get; set; }
}

// ── ViewModel ────────────────────────────────────────────────────────────────
public sealed class DebtViewModel
{
    public DebtFilter Filter { get; set; } = new();
    public DebtKpi Kpi { get; set; } = new();

    public IReadOnlyList<CustomerDebtSummary> Customers { get; set; } = Array.Empty<CustomerDebtSummary>();
    public int TotalCustomers { get; set; }
    public int TotalPages => TotalCustomers == 0 ? 1 : (int)Math.Ceiling(TotalCustomers / (double)DebtFilter.PageSize);

    // Chi tiết cho 1 khách (nếu mở detail)
    public IReadOnlyList<DebtTransaction> DetailTransactions { get; set; } = Array.Empty<DebtTransaction>();

    public IReadOnlyList<string> AvailableBranches { get; set; } = Array.Empty<string>();
}
