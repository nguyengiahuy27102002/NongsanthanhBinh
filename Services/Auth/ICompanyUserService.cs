using DuongVanDung.WebApp.Models.Auth;

namespace DuongVanDung.WebApp.Services.Auth;

public interface ICompanyUserService
{
    CompanyUserRecord? ValidateCredentials(string username, string password);
}
