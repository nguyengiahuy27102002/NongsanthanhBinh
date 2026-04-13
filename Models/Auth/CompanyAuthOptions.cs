namespace DuongVanDung.WebApp.Models.Auth;

public sealed class CompanyAuthOptions
{
    public const string SectionName = "CompanyAuth";

    public List<CompanyUserRecord> Users { get; set; } = new();
}
