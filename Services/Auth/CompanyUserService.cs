using DuongVanDung.WebApp.Models.Auth;
using Microsoft.Extensions.Options;

namespace DuongVanDung.WebApp.Services.Auth;

public sealed class CompanyUserService : ICompanyUserService
{
    private readonly CompanyAuthOptions _options;
    private readonly IPasswordHashService _passwordHashService;

    public CompanyUserService(
        IOptions<CompanyAuthOptions> options,
        IPasswordHashService passwordHashService)
    {
        _options = options.Value;
        _passwordHashService = passwordHashService;
    }

    public CompanyUserRecord? ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        CompanyUserRecord? user = _options.Users.FirstOrDefault(x =>
            string.Equals(x.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));

        if (user is null)
        {
            return null;
        }

        bool isValid = _passwordHashService.Verify(password, user.PasswordSalt, user.PasswordHash);
        return isValid ? user : null;
    }
}
