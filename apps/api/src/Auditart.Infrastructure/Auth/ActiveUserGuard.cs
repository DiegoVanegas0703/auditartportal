using Auditart.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Auditart.Infrastructure.Auth;

public sealed class ActiveUserGuard
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(20);
    private readonly IAppDbContext _db;
    private readonly IMemoryCache _cache;

    public ActiveUserGuard(IAppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<bool> IsActiveAsync(Guid userId, CancellationToken ct = default)
    {
        var key = CacheKey(userId);
        if (_cache.TryGetValue(key, out bool cached))
            return cached;

        var active = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.IsActive, ct);
        _cache.Set(key, active, CacheTtl);
        return active;
    }

    public void Invalidate(Guid userId) => _cache.Remove(CacheKey(userId));

    private static string CacheKey(Guid userId) => $"user-active:{userId:N}";
}
