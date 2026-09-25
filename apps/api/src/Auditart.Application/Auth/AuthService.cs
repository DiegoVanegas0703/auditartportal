using Auditart.Application.Abstractions;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Application.Auth;

public class AuthService
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;

    public AuthService(IAppDbContext db, IPasswordHasher hasher, IJwtTokenService jwt)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<AuthTokensDto> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new UnauthorizedAccessException("Email y contraseña son obligatorios.");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            throw new UnauthorizedAccessException("Credenciales inválidas.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Usuario inactivo.");

        if (!_hasher.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Credenciales inválidas.");

        user.RecordLogin();
        return await IssueTokensAsync(user, ct);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
            throw new InvalidOperationException("La nueva contraseña debe tener al menos 4 caracteres.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedAccessException("Usuario no encontrado.");

        if (string.IsNullOrEmpty(user.PasswordHash) || !_hasher.Verify(currentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("La contraseña actual no es correcta.");

        user.ChangePassword(_hasher.Hash(newPassword));
        await _db.SaveChangesAsync(ct);
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
        user.MustChangePassword,
        new AuthPermissionsDto(
            PermissionService.CanTriage(user.Role),
            PermissionService.CanOperateBoard(user.Role),
            PermissionService.CanBill(user.Role),
            PermissionService.SeesAllQueues(user.Role),
            PermissionService.CanManageUsers(user.Role),
            PermissionService.CanViewReports(user.Role),
            PermissionService.CanManagePrecios(user.Role)));
}
