using Microsoft.Extensions.Caching.Memory;

namespace DuongVanDung.WebApp.Services.Auth;

public sealed class LoginAttemptService : ILoginAttemptService
{
    private const int FailureLimit = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TrackingDuration = TimeSpan.FromMinutes(20);

    private readonly IMemoryCache _memoryCache;

    public LoginAttemptService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public bool IsLocked(string key, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;

        if (_memoryCache.TryGetValue<LoginAttemptState>(GetLockKey(key), out var state) && state is not null)
        {
            remaining = state.ExpiresAtUtc - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                return true;
            }
        }

        return false;
    }

    public void RegisterFailure(string key)
    {
        LoginAttemptState state = _memoryCache.GetOrCreate(GetFailKey(key), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TrackingDuration;
            return new LoginAttemptState();
        })!;

        state.Count++;

        if (state.Count >= FailureLimit)
        {
            var lockState = new LoginAttemptState
            {
                ExpiresAtUtc = DateTimeOffset.UtcNow.Add(LockDuration)
            };

            _memoryCache.Set(GetLockKey(key), lockState, LockDuration);
            _memoryCache.Remove(GetFailKey(key));
        }
        else
        {
            _memoryCache.Set(GetFailKey(key), state, TrackingDuration);
        }
    }

    public void Reset(string key)
    {
        _memoryCache.Remove(GetFailKey(key));
        _memoryCache.Remove(GetLockKey(key));
    }

    private static string GetFailKey(string key) => $"login-fail:{key}";

    private static string GetLockKey(string key) => $"login-lock:{key}";

    private sealed class LoginAttemptState
    {
        public int Count { get; set; }

        public DateTimeOffset ExpiresAtUtc { get; set; }
    }
}
