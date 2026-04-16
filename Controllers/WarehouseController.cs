using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DuongVanDung.WebApp.Services.Warehouse;
using DuongVanDung.WebApp.Services.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DuongVanDung.WebApp.Controllers;

[Authorize]
public class WarehouseController : Controller
{
    private readonly IWarehouseService _svc;
    private readonly IAiAnalysisService _ai;
    private readonly IConfiguration _cfg;
    private readonly ILogger<WarehouseController> _log;

    public WarehouseController(IWarehouseService svc, IAiAnalysisService ai, IConfiguration cfg, ILogger<WarehouseController> log)
    { _svc = svc; _ai = ai; _cfg = cfg; _log = log; }

    public async Task<IActionResult> Index()
    {
        var vm = await _svc.GetOverviewAsync();
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> TrendData(string product = "coffee", string period = "6m")
    {
        var data = await _svc.GetTrendAsync(product, period);
        return Json(data);
    }

    [HttpGet]
    public async Task<IActionResult> InventoryTrend(string product = "coffee", string period = "6m")
    {
        var data = await _svc.GetInventoryTrendAsync(product, period);
        return Json(data);
    }

    // ── AI Analysis: query DB → build prompt → call OpenAI ───────────────────
    [HttpGet]
    public async Task<IActionResult> AiAnalysis(string product = "coffee", string period = "6m")
    {
        try
        {
            var result = await _ai.AnalyzeTrendAsync(product, period);
            if (result.StartsWith("Lỗi"))
                return Json(new { error = result });
            return Json(new { analysis = result });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "AiAnalysis endpoint error");
            return Json(new { error = $"Server error: {ex.Message}" });
        }
    }

    // ── Test endpoint: no auth needed, just tests OpenAI connection ──────────
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> AiTest()
    {
        var apiKey = _cfg["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            return Json(new { error = "No API key configured" });

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var jsonOpts = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var body = JsonSerializer.Serialize(new
            {
                model = "gpt-4o-mini",
                messages = new object[]
                {
                    new { role = "user", content = "Thoi tiet hom nay o Dak Lak, Viet Nam the nao? Tra loi ngan 3 dong bang tieng Viet." }
                },
                max_tokens = 150
            }, jsonOpts);

            _log.LogInformation("AiTest: sending request...");
            var resp = await http.PostAsync("https://api.openai.com/v1/chat/completions",
                new StringContent(body, Encoding.UTF8, "application/json"));
            var respBody = await resp.Content.ReadAsStringAsync();
            _log.LogInformation("AiTest: status {S}, {L} chars", resp.StatusCode, respBody.Length);

            if (!resp.IsSuccessStatusCode)
                return Json(new { error = $"OpenAI {(int)resp.StatusCode}", detail = respBody[..Math.Min(300, respBody.Length)] });

            using var doc = JsonDocument.Parse(respBody);
            var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return Json(new { status = "OK", analysis = text });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "AiTest failed");
            return Json(new { error = ex.GetType().Name, message = ex.Message });
        }
    }
}
