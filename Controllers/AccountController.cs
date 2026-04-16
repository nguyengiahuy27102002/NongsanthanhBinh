using System.Security.Claims;
using DuongVanDung.WebApp.Models.Auth;
using DuongVanDung.WebApp.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DuongVanDung.WebApp.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly ICompanyUserService _companyUserService;
    private readonly ILoginAttemptService _loginAttemptService;

    public AccountController(
        ICompanyUserService companyUserService,
        ILoginAttemptService loginAttemptService)
    {
        _companyUserService = companyUserService;
        _loginAttemptService = loginAttemptService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string username = model.Username.Trim();
        string clientKey = $"{HttpContext.Connection.RemoteIpAddress}:{username.ToLowerInvariant()}";

        if (_loginAttemptService.IsLocked(clientKey, out TimeSpan remaining))
        {
            ModelState.AddModelError(string.Empty,
                $"Tài khoản đang bị khóa tạm thời. Thử lại sau khoảng {Math.Ceiling(remaining.TotalMinutes)} phút.");
            return View(model);
        }

        CompanyUserRecord? user = _companyUserService.ValidateCredentials(username, model.Password);
        if (user is null)
        {
            _loginAttemptService.RegisterFailure(clientKey);
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
            return View(model);
        }

        _loginAttemptService.Reset(clientKey);
        await _companyUserService.UpdateLastLoginAsync(user.Username);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new("display_name", user.DisplayName)
        };

        foreach (string role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(model.RememberMe ? 24 : 8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
