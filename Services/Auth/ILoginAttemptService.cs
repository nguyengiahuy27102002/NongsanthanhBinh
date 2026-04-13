namespace DuongVanDung.WebApp.Services.Auth;

public interface ILoginAttemptService
{
    bool IsLocked(string key, out TimeSpan remaining);

    void RegisterFailure(string key);

    void Reset(string key);
}
