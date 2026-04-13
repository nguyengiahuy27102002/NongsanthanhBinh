namespace DuongVanDung.WebApp.Services.Auth;

public interface IPasswordHashService
{
    bool Verify(string password, string saltBase64, string expectedHashBase64);
}
