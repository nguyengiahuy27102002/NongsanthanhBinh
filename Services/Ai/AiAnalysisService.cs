using DuongVanDung.WebApp.Helpers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace DuongVanDung.WebApp.Services.Ai;

public interface IAiAnalysisService
{
    Task<string> AnalyzeTrendAsync(string product, string period);
}

public sealed class AiAnalysisService : IAiAnalysisService
{
    private static readonly (string Wh, string Conn, string Table, string WCol, string Product)[] Tables =
    {
        ("DVD", "DefaultConnection", "duongvandung",      "TrongLuong",     "coffee"),
        ("DVD", "DefaultConnection", "nhapCaPheSi",       "TrongLuongHang", "coffee"),
        ("DVD", "DefaultConnection", "xntTieu",           "TrongLuong",     "pepper"),
        ("DVD", "DefaultConnection", "nhapTieuKhachSi",   "TrongLuongHang", "pepper"),
        ("TD",  "DefaultConnection", "NhapCaPheThongDao", "TrongLuongHang", "coffee"),
        ("TD",  "DefaultConnection", "NhapTieuThongDao",  "TrongLuongHang", "pepper"),
        ("YM",  "KhoYmoal", "NhapCaPheLe",  "TrongLuong",     "coffee"),
        ("YM",  "KhoYmoal", "NhapCaPheSi",  "TrongLuongHang", "coffee"),
        ("YM",  "KhoYmoal", "NhapTieuLe",   "TrongLuong",     "pepper"),
        ("YM",  "KhoYmoal", "NhapTieuSi",   "TrongLuongHang", "pepper"),
    };

    private readonly IConfiguration _cfg;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AiAnalysisService> _log;

    public AiAnalysisService(IConfiguration cfg, IMemoryCache cache, ILogger<AiAnalysisService> log)
    { _cfg = cfg; _cache = cache; _log = log; }

    public async Task<string> AnalyzeTrendAsync(string product, string period)
    {
        var cacheKey = $"ai_{product}_{period}";
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached != null) return cached;

        var apiKey = _cfg["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey)) return "Loi: Chua cau hinh OpenAI ApiKey.";

        // Step 1: Query data
        _log.LogInformation("AI: querying data for {P}/{Per}", product, period);
        var data = await QueryDataAsync(product, period);
        if (string.IsNullOrWhiteSpace(data)) return "Loi: Khong co du lieu.";
        _log.LogInformation("AI: got {Len} chars data", data.Length);

        // Step 2: Call OpenAI
        var prompt = BuildPrompt(product, period, data);
        _log.LogInformation("AI: calling OpenAI, prompt {Len} chars", prompt.Length);
        var result = await CallOpenAIAsync(apiKey, prompt);
        _log.LogInformation("AI: result {Len} chars, starts with: {Start}", result.Length, result[..Math.Min(50, result.Length)]);

        if (!result.StartsWith("Loi"))
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(15));
        return result;
    }

    private async Task<string> QueryDataAsync(string product, string period)
    {
        var fromDate = period switch
        {
            "1m" => VietnamTime.Now.AddDays(-30), "3m" => VietnamTime.Now.AddMonths(-3),
            "6m" => VietnamTime.Now.AddMonths(-6), "1y" => VietnamTime.Now.AddYears(-1),
            "2y" => VietnamTime.Now.AddYears(-2), "5y" => VietnamTime.Now.AddYears(-5),
            _ => VietnamTime.Now.AddMonths(-6),
        };

        var sb = new StringBuilder();
        var relevant = Tables.Where(t => t.Product == product).ToList();

        foreach (var connGroup in relevant.GroupBy(t => t.Conn))
        {
            var connStr = _cfg.GetConnectionString(connGroup.Key);
            if (string.IsNullOrEmpty(connStr)) continue;

            try
            {
                await using var cn = new SqlConnection(connStr);
                cn.Open(); // sync open for reliability

                foreach (var t in connGroup)
                {
                    using var cmd = cn.CreateCommand();
                    cmd.CommandText = $@"SELECT
                        FORMAT(DATEFROMPARTS(YEAR(ngayNhap),MONTH(ngayNhap),1),'yyyy-MM') AS M,
                        COUNT(*) AS Cnt,
                        ISNULL(SUM({t.WCol}),0) AS Kg,
                        ISNULL(AVG(DoAm),0) AS Am,
                        ISNULL(AVG(TapChat),0) AS Tap
                        FROM {t.Table} WITH(NOLOCK)
                        WHERE ngayNhap >= '{fromDate:yyyy-MM-dd}'
                        GROUP BY DATEFROMPARTS(YEAR(ngayNhap),MONTH(ngayNhap),1)
                        ORDER BY 1";
                    cmd.CommandTimeout = 10;

                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        sb.AppendLine($"{t.Wh}|{t.Table}|{r.GetString(0)}|{r.GetInt32(1)} phieu|{r.GetDecimal(2):#,0}kg|am {r.GetDecimal(3):0.#}%|tap {r.GetDecimal(4):0.##}%");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning("AI query error {Conn}: {Err}", connGroup.Key, ex.Message);
                sb.AppendLine($"(DB error: {ex.Message})");
            }
        }

        return sb.ToString();
    }

    private static string BuildPrompt(string product, string period, string data)
    {
        var pn = product == "coffee" ? "ca phe" : "tieu";
        var pr = period switch { "1m" => "1 thang", "3m" => "3 thang", "6m" => "6 thang", "1y" => "1 nam", "2y" => "2 nam", "5y" => "5 nam", _ => "6 thang" };

        return $@"Ban la chuyen gia tai chinh cap cao (CFO/hedge fund analyst) phan tich du lieu nong san.

Du lieu nhap {pn} ({pr} gan nhat), format: Kho|Bang|Thang|So phieu|Kg|Do am|Tap chat
DVD = Duong Van Dung (kho chinh), TD = Thong Dao, YM = Ymoal

{data}

Quy tac chat luong: {pn} am 15% = neutral (duoi = cong ty loi, tren = tru khach). Tap 1% = neutral.{(product == "pepper" ? " DEM 500 = neutral (tren = tot)." : "")}

Phan tich theo 3 phan:

**1. KEY INSIGHTS**
- 5-7 insight quan trong nhat
- bien dong, trend, co hoi

**2. RISKS & WARNINGS**
- rui ro chinh (ton kho, chat luong, tai chinh, van hanh)
- canh bao som

**3. ACTION PLAN**
- hanh dong cu the: nhap/ban o dau, chuyen hang giua kho nao, kiem tra gi

Tra loi bang tieng Viet, ngan gon, bullet points. Khong mo ta lai data. Chi insight va action.";
    }

    private async Task<string> CallOpenAIAsync(string apiKey, string prompt)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var jo = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var body = JsonSerializer.Serialize(new
            {
                model = "gpt-4o-mini",
                messages = new object[]
                {
                    new { role = "system", content = "Chuyen gia phan tich nong san cap CFO. Ngan gon, thuc te, tieng Viet." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 1500,
                temperature = 0.7
            }, jo);

            _log.LogInformation("AI: POST to OpenAI, body {Len} chars", body.Length);

            var resp = await http.PostAsync("https://api.openai.com/v1/chat/completions",
                new StringContent(body, Encoding.UTF8, "application/json"));
            var respText = await resp.Content.ReadAsStringAsync();

            _log.LogInformation("AI: OpenAI status {S}, response {Len} chars", resp.StatusCode, respText.Length);

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("AI: OpenAI error body: {B}", respText[..Math.Min(300, respText.Length)]);
                return $"Loi: OpenAI tra ve {(int)resp.StatusCode}. Chi tiet: {respText[..Math.Min(200, respText.Length)]}";
            }

            using var doc = JsonDocument.Parse(respText);
            var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return text ?? "Loi: Khong co phan hoi.";
        }
        catch (TaskCanceledException)
        {
            return "Loi: OpenAI timeout (>30s). Thu lai.";
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "AI: OpenAI call exception");
            return $"Loi: {ex.GetType().Name} - {ex.Message}";
        }
    }
}
