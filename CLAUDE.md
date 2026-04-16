# CLAUDE.md — Nông Sản Thanh Bình Operations Platform

## 1. Project Overview
Enterprise web application for an agricultural trading company (coffee & pepper). ASP.NET Core 8.0 MVC with Razor Views, SQL Server on AWS RDS, deployed on Linux via Nginx reverse proxy.

**Tech stack**: C# / ASP.NET Core 8.0 / Razor Views / Chart.js / Bootstrap 5 / SQL Server / ClosedXML / OpenAI API

## 2. Business Context
The company buys coffee and pepper from farmers/traders across multiple warehouses, then sells to large distributors. The software must support daily operations: recording imports/exports, tracking inventory, analyzing quality, managing costs, and providing AI-powered insights.

**3 warehouses**: Dương Văn Dũng (main), Thông Đào (satellite), Ymoal (support)

## 3. Core Domain Rules — MUST FOLLOW

### 3.1 Coffee Quality Rules
| Metric | Neutral | Below Neutral | Above Neutral |
|--------|---------|---------------|---------------|
| **Moisture (DoAm)** | 15% | Company profits (no deduction) | Customer's product deducted |
| **Impurity (TapChat)** | 1% | Company profits | Customer's product deducted |

### 3.2 Pepper Quality Rules
| Metric | Neutral | Below Neutral | Above Neutral |
|--------|---------|---------------|---------------|
| **Moisture (DoAm)** | 15% | Add to customer (bonus down to 12.5% floor; below 12.5% = company profits) | Customer deducted |
| **Impurity (TapChat)** | 1% | Company profits | Customer deducted |
| **DEM (Dem)** | 500 | Deducted (low quality, small beans) | Added to customer (high quality) |

**CRITICAL**: Coffee and pepper have DIFFERENT formulas. Never use the same calculation for both.

### 3.3 Inventory Calculation
```
Inventory at month N = SUM(all imports from beginning to end of month N) - SUM(all exports from beginning to end of month N)
```
This applies separately to: TrongLuong/TrongLuongHang, TrongLuongTruBi, QuyChuan.

Import sources = retail (lẻ) + wholesale (sỉ) tables combined.

### 3.4 Cost Calculation
```
Total purchase cost = SUM(ThanhTien from retail imports) + SUM(ThanhTien from wholesale imports)
```
**Exception for Dương Văn Dũng warehouse**: Must ADD the `chi` table (external debt payments).
```
DVD total cost = import ThanhTien + chi.ThanhTien
```

## 4. Database Structure — CRITICAL REFERENCE

### 4.1 Database: `nguyengiahuy` (Main — DVD + Thông Đào)

#### Dương Văn Dũng (DVD) — Coffee Import
| Table | Type | Weight Column | Date Column | Has DiaChi |
|-------|------|---------------|-------------|------------|
| `duongvandung` | Retail (lẻ) | `TrongLuong` | `ngayNhap` (smalldatetime) | Yes |
| `nhapCaPheSi` | Wholesale (sỉ) | `TrongLuongHang` | `ngayNhap` (smalldatetime) | Yes |

#### DVD — Pepper Import
| Table | Type | Weight Column | Has DEM |
|-------|------|---------------|---------|
| `xntTieu` | Retail (lẻ) | `TrongLuong` | Yes |
| `nhapTieuKhachSi` | Wholesale (sỉ) | `TrongLuongHang` | Yes |

#### DVD — Exports
| Table | Product | Date Column | Name Column |
|-------|---------|-------------|-------------|
| `xuatCaPhe` | Coffee | `ngayXuat` | `TenTaiXe` |
| `xuatTieu` | Pepper | `ngayXuat` | `TenTaiXe` |

#### DVD — Expenses
| Table | Purpose |
|-------|---------|
| `chi` | External debt payments — columns: `ngayChi`, `NguonChi`, `NguoiChi`, `QuyChuan`, `GiaCaPhe`, `ThanhTien`, `chiID` |

#### Thông Đào
| Table | Product | Type | Weight Column |
|-------|---------|------|---------------|
| `NhapCaPheThongDao` | Coffee | Wholesale | `TrongLuongHang` |
| `NhapTieuThongDao` | Pepper | Wholesale | `TrongLuongHang` |
| `XuatCaPheThongDao` | Coffee export | — | `TrongLuongHang` |
| `XuatTieuThongDao` | Pepper export | — | `TrongLuongHang` |

#### Other tables in nguyengiahuy
| Table | Purpose |
|-------|---------|
| `KhachHang` | Customer master (wholesale) |
| `DanhSachKhachLe` | Customer master (retail) |
| `Gia` | Daily price history: `oderID`, `caPhe`, `tieu` (latest = highest oderID) |
| `noCaPhe` | Coffee debt/consignment records (currently empty) |
| `noTieu` | Pepper debt/consignment records (currently empty) |
| `ChuyenKhoan` | Bank transfer records |
| `TaiKhoan` | User accounts |

### 4.2 Database: `KhoYmoal` (Ymoal warehouse)

| Table | Product | Type | Weight Column | Has DEM |
|-------|---------|------|---------------|---------|
| `NhapCaPheLe` | Coffee | Retail | `TrongLuong` | No |
| `NhapCaPheSi` | Coffee | Wholesale | `TrongLuongHang` | No |
| `NhapTieuLe` | Pepper | Retail | `TrongLuong` | Yes |
| `NhapTieuSi` | Pepper | Wholesale | `TrongLuongHang` | Yes |
| `XuatCaPhe` | Coffee export | — | `TrongLuongHang` | No |
| `XuatTieu` | Pepper export | — | `TrongLuongHang` | Yes |

### 4.3 Column Name Mapping
Import tables have DIFFERENT column names depending on retail vs wholesale:

| Concept | Retail (lẻ) Column | Wholesale (sỉ) Column | Export Column |
|---------|--------------------|-----------------------|---------------|
| Raw weight | `TrongLuong` | `TrongLuongHang` | `TrongLuongHang` |
| Net weight | `TrongLuongTruBi` | `TrongLuongTruBi` | `TrongLuongTruBi` |
| Standard weight | `QuyChuan` | `QuyChuan` | `QuyChuan` |
| Date | `ngayNhap` | `ngayNhap` | `ngayXuat` |
| Customer name | `TenKhachHang` | `TenKhachHang` | `TenTaiXe` |
| Address | `DiaChi` | `DiaChi` (some missing) | `DiaDiemXuatHang` |
| Phone | `Sdt` | `Sdt` | `Sdt` |
| Amount | `ThanhTien` | `ThanhTien` | *(not available)* |
| Payment | `ChuyenKhoan` | `ChuyenKhoan` | *(not available)* |
| Order ID | `orderID` | `orderID` | `xuatID` |
| Moisture | `DoAm` | `DoAm` | `DoAm` |
| Impurity | `TapChat` | `TapChat` | `TapChat` |
| DEM (pepper only) | `Dem` | `Dem` | `Dem` |

**Payment logic**: `ChuyenKhoan = 'Yes'` → Bank transfer. Otherwise → Cash.

**ThanhTien data types vary**: `bigint` in some tables, `int` in others. Always CAST to bigint: `CAST(ISNULL(ThanhTien,0) AS bigint)`.

### 4.4 Price column mapping (pepper)
- Retail pepper: `GiaTieu` and `GiaMoi` → use `COALESCE(NULLIF(GiaMoi,0), GiaTieu, 0)`
- Wholesale pepper: `GiaCaPhe` (confusing name) and `GiaMoi` → use `COALESCE(NULLIF(GiaMoi,0), GiaCaPhe, 0)`
- Coffee (all): `GiaCaPhe`

## 5. Engineering Principles

### Performance
- **MemoryCache** on all service methods (3-15 min TTL)
- **WITH(NOLOCK)** on read queries (reporting data, eventual consistency OK)
- **Avoid CAST on date columns in WHERE** — use `ngayNhap >= @from AND ngayNhap < @toNextDay` instead of `CAST(ngayNhap AS date)`
- **Parallel DB queries** — use `Task.WhenAll` for independent queries across databases
- **Database indexes** exist on `ngayNhap` / `ngayXuat` for all tables

### SQL Safety
- Never use string interpolation for user input in SQL. Use parameterized queries.
- Table names from config (not user input) are safe for interpolation.
- Always use `ISNULL()` for nullable columns.

### Architecture
```
Controller → Service (business logic + SQL) → SQL Server
                ↓
            IMemoryCache (3-15 min)
                ↓
            ViewModel → Razor View + Chart.js
```

## 6. Frontend Guidance
- **Design system**: Inter font, CSS variables in site.css, Bootstrap 5 grid
- **Charts**: Chart.js 4.x from CDN (loaded in _ReportPageLayout.cshtml)
- **Style**: Enterprise SaaS — clean, minimal, data-dense but readable
- **Colors**: Blue (#2563eb) DVD, Red (#dc2626) Thông Đào, Amber (#d97706) Ymoal, Coffee (#92400e), Pepper (#166534)
- **Vietnamese UI**: All labels, headers, tooltips in Vietnamese
- **Layout patterns**: KPI cards → Charts → Data tables → Detail sections
- **Responsive**: Desktop-first, tablet support via CSS media queries

## 7. Backend Guidance
- **One service per domain**: DashboardService, WarehouseService, QualityReportService, DebtService, DataService, CustomerService, AiAnalysisService
- **Registered in Program.cs** as `AddScoped`
- **Each service** connects to both databases (DefaultConnection + KhoYmoal)
- **Always combine retail + wholesale** tables for totals
- **Never show only one table** when both exist — always UNION ALL or sum both

## 8. Security
- Cookie authentication (`dvd.auth`, 8-hour expiry)
- Role-based: Admin, Manager, Staff
- `[Authorize]` on all controllers except login
- AntiForgery tokens on forms
- Password hashing with salt
- Connection strings in appsettings.json (should move to user-secrets for production)

## 9. AI Integration
- **OpenAI API** (gpt-4o-mini) configured in `appsettings.json` → `OpenAI:ApiKey`
- **AiAnalysisService**: queries DB → builds structured prompt → calls OpenAI → caches 15 min
- **Prompt pattern**: Data context (from DB) + Analysis framework (3 sections: Key Insights, Risks & Warnings, Action Plan)
- **Output format**: Vietnamese, bullet points, no data description — only insights and actions

## 10. Key Formulas Reference

### Inventory at time T
```sql
-- Coffee inventory for DVD at time T:
(SELECT SUM(TrongLuong) FROM duongvandung WHERE ngayNhap <= T)
+ (SELECT SUM(TrongLuongHang) FROM nhapCaPheSi WHERE ngayNhap <= T)
- (SELECT SUM(TrongLuongHang) FROM xuatCaPhe WHERE ngayXuat <= T)
```
Same pattern for TrongLuongTruBi and QuyChuan.

### Quality Score
```
Score = 50 (baseline)
  + (15 - avgMoisture) × 3        // moisture component
  + (1 - avgImpurity) × 20        // impurity component (if below neutral)
  - (avgImpurity - 1) × 50        // penalty if above neutral
  + (avgDEM - 500) / 50 × 2       // DEM component (pepper only)
Clamp to [0, 100]
```

### Current Price
```sql
SELECT TOP 1 caPhe, tieu FROM Gia ORDER BY oderID DESC
```

## 11. File Structure
```
DuongVanDung.WebApp/
├── Controllers/        DashboardController, DataController, ReportController, WarehouseController, DebtController, CustomerController, AdminController, AccountController
├── Models/
│   ├── Auth/           DashboardViewModel, LoginViewModel
│   ├── Customer/       CustomerModels
│   ├── Data/           DataModels (DataFilter, DataEntryRow, DataTableViewModel)
│   ├── Debt/           DebtModels
│   ├── Report/         QualityReportModels
│   └── Warehouse/      WarehouseModels
├── Services/
│   ├── Ai/             AiAnalysisService (OpenAI integration)
│   ├── Auth/           PasswordHashService, CompanyUserService
│   ├── Customer/       CustomerService
│   ├── Dashboard/      DashboardService
│   ├── Data/           DataService
│   ├── Debt/           DebtService
│   ├── Product/        ProductService
│   ├── Report/         QualityReportService
│   └── Warehouse/      WarehouseService
├── Views/              Razor views per controller + shared layouts
├── wwwroot/css/        site.css (design tokens), customer.css (all component styles)
└── Program.cs          DI registration, auth config, middleware
```

## 12. When Working on This Project
1. **Always verify table names** — retail vs wholesale have different weight columns
2. **Always combine lẻ + sỉ** — never query only one table for totals
3. **DVD expenses** — remember to include `chi` table for cost calculations
4. **Pepper ≠ Coffee** — different quality formulas, different price columns
5. **Two databases** — nguyengiahuy (DVD + Thông Đào) and KhoYmoal (Ymoal)
6. **Export tables** — use `ngayXuat` not `ngayNhap`, `TenTaiXe` not `TenKhachHang`, no ThanhTien/ChuyenKhoan
7. **Cache everything** — DB is remote (AWS Sydney), high latency
8. **Vietnamese UI** — all user-facing text in Vietnamese
