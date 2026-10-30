using Auditart.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Auditart.Infrastructure.Auth;

public sealed class LoginLockoutService : ILoginLockoutService
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;

    public LoginLockoutService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool IsLocked(string email, string? clientKey, out string? message)
    {
        message = null;
        if (_cache.TryGetValue(LockKey(email), out DateTimeOffset until) && until > DateTimeOffset.UtcNow)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((until - DateTimeOffset.UtcNow).TotalMinutes));
            message = $"Cuenta bloqueada por demasiados intentos. Probá de nuevo en {minutes} min.";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(clientKey)
            && _cache.TryGetValue(IpLockKey(clientKey), out DateTimeOffset ipUntil)
            && ipUntil > DateTimeOffset.UtcNow)
        {
            message = "Demasiados intentos desde esta red. Esperá unos minutos.";
            return true;
        }

        return false;
    }

    public void RegisterFailure(string email, string? clientKey)
    {
        var failures = Increment(FailKey(email), Window);
        if (failures >= MaxFailures)
            _cache.Set(LockKey(email), DateTimeOffset.UtcNow.Add(LockDuration), LockDuration);

        if (string.IsNullOrWhiteSpace(clientKey)) return;
        var ipFailures = Increment(IpFailKey(clientKey), Window);
        if (ipFailures >= 25)
            _cache.Set(IpLockKey(clientKey), DateTimeOffset.UtcNow.Add(LockDuration), LockDuration);
    }

    public void RegisterSuccess(string email, string? clientKey)
    {
        _cache.Remove(FailKey(email));
        _cache.Remove(LockKey(email));
        if (!string.IsNullOrWhiteSpace(clientKey))
            _cache.Remove(IpFailKey(clientKey));
    }

    private int Increment(string key, TimeSpan ttl)
    {
        var count = _cache.TryGetValue(key, out int current) ? current + 1 : 1;
        _cache.Set(key, count, ttl);
        return count;
    }

    private static string FailKey(string email) => $"login:fail:{Normalize(email)}";
    private static string LockKey(string email) => $"login:lock:{Normalize(email)}";
    private static string IpFailKey(string ip) => $"login:ip-fail:{ip}";
    private static string IpLockKey(string ip) => $"login:ip-lock:{ip}";
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
