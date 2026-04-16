using DuongVanDung.WebApp.Models.Auth;
using DuongVanDung.WebApp.Services.Auth;
using DuongVanDung.WebApp.Services.Customer;
using DuongVanDung.WebApp.Services.Data;
using DuongVanDung.WebApp.Services.Product;
using DuongVanDung.WebApp.Services.Report;
using DuongVanDung.WebApp.Services.Ai;
using DuongVanDung.WebApp.Services.Dashboard;
using DuongVanDung.WebApp.Services.Warehouse;
using DuongVanDung.WebApp.Services.Debt;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CompanyAuthOptions>(
    builder.Configuration.GetSection(CompanyAuthOptions.SectionName));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IPasswordHashService, PasswordHashService>();
builder.Services.AddScoped<ICompanyUserService, CompanyUserService>();
builder.Services.AddSingleton<ILoginAttemptService, LoginAttemptService>();
builder.Services.AddScoped<DatabaseAuthSeeder>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "dvd.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // SameAsRequest: hoạt động cả HTTP lẫn HTTPS
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IDataService, DataService>();
builder.Services.AddScoped<IAiAnalysisService, AiAnalysisService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<IQualityReportService, QualityReportService>();
builder.Services.AddScoped<IDebtService, DebtService>();
builder.Services.AddAuthorization();
builder.Services.AddResponseCompression(opts => opts.EnableForHttps = true);

// Locale Việt Nam: định dạng số, ngày tháng, timezone
var viVN = new CultureInfo("vi-VN");
builder.Services.Configure<RequestLocalizationOptions>(opts =>
{
    opts.DefaultRequestCulture = new RequestCulture(viVN);
    opts.SupportedCultures      = new[] { viVN };
    opts.SupportedUICultures    = new[] { viVN };
});

// Antiforgery hoạt động cả HTTP lẫn HTTPS
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite     = SameSiteMode.Lax;
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // HSTS tắt vì chưa có SSL — bật lại sau khi cài certbot
    // app.UseHsts();
}

app.UseResponseCompression();

// Đọc header từ Nginx (X-Forwarded-For, X-Forwarded-Proto)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// Chỉ redirect HTTPS khi đã có SSL
if (!app.Environment.IsDevelopment())
{
    // Bỏ comment dòng dưới khi đã cài SSL (certbot)
    // app.UseHttpsRedirection();
}

// Cache static files 30 ngày (CSS, JS, ảnh)
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=2592000";
    }
});

app.UseRequestLocalization();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Endpoint test — truy cập /ping để kiểm tra app đang chạy
app.MapGet("/ping", () => $"OK — {DateTime.Now:dd/MM/yyyy HH:mm:ss}").AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Tạo bảng TaiKhoan và seed tài khoản mặc định (chỉ chạy lần đầu)
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseAuthSeeder>();
    await seeder.SeedAsync();
}

app.Run();
