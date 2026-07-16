using Auditart.Application.Abstractions;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Application.Auth;

public class AuthService
{
    private readonly IAppDbContext _db;
    private readonly IGoogleAuthService _google;
    private readonly IJwtTokenService _jwt;

    public AuthService(IAppDbContext db, IGoogleAuthService google, IJwtTokenService jwt)
    {
        _db = db;
        _google = google;
        _jwt = jwt;
    }

    public async Task<AuthTokensDto> LoginWithGoogleAsync(string idToken, CancellationToken ct = default)
    {
        var googleUser = await _google.ValidateIdTokenAsync(idToken, ct);
        if (!googleUser.EmailVerified)
            throw new InvalidOperationException("El email de Google no está verificado.");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == googleUser.Email.ToLowerInvariant(), ct);

        if (user is null)
        {
            // En MVP: usuarios mock pre-seeded. Si no existe, denegar (whitelist).
            throw new UnauthorizedAccessException(
                $"No hay un usuario registrado para {googleUser.Email}. Contactá al administrador.");
        }

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Usuario inactivo.");

        if (string.IsNullOrEmpty(user.GoogleSubjectId))
            user.LinkGoogle(googleUser.SubjectId);

        user.RecordLogin();

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthTokensDto> RefreshAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var hash = _jwt.HashToken(rawRefreshToken);
        var existing = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null || !existing.IsActive || !existing.User.IsActive)
            throw new UnauthorizedAccessException("Refresh token inválido o expirado.");

        var (newRaw, newHash, expires) = _jwt.CreateRefreshToken();
        existing.Revoke(newHash);

        var replacement = RefreshToken.Create(existing.UserId, newHash, expires);
        _db.Add(replacement);
        await _db.SaveChangesAsync(ct);

        var access = _jwt.CreateAccessToken(existing.User);
        return new AuthTokensDto(
            access,
            newRaw,
            DateTime.UtcNow.AddMinutes(30),
            MapUser(existing.User));
    }

    public async Task RevokeRefreshAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var hash = _jwt.HashToken(rawRefreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is null) return;
        existing.Revoke();
        await _db.SaveChangesAsync(ct);
    }

    private async Task<AuthTokensDto> IssueTokensAsync(User user, CancellationToken ct)
    {
        var access = _jwt.CreateAccessToken(user);
        var (raw, hash, expires) = _jwt.CreateRefreshToken();
        _db.Add(RefreshToken.Create(user.Id, hash, expires));
        await _db.SaveChangesAsync(ct);

        return new AuthTokensDto(
            access,
            raw,
            DateTime.UtcNow.AddMinutes(30),
            MapUser(user));
    }

    public static AuthUserDto MapUser(User user) => new(
        user.Id,
        user.Name,
        user.Email,
        user.Role,
        user.DefaultQueue,
        new AuthPermissionsDto(
            PermissionService.CanTriage(user.Role),
            PermissionService.CanOperateBoard(user.Role),
            PermissionService.CanBill(user.Role),
            PermissionService.SeesAllQueues(user.Role),
            PermissionService.CanManageUsers(user.Role)));
}
