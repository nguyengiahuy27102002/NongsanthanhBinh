namespace DuongVanDung.WebApp.Models.Auth;

public sealed class CompanyUserRecord
{
    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordSalt { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string[] Roles { get; set; } = Array.Empty<string>();
}
