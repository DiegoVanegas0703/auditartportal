using Auditart.Application.Abstractions;
using Auditart.Application.Auth;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Application.Users;

public sealed record UserListItemDto(
    Guid Id,
    string Name,
    string Email,
    UserRole Role,
    AuditQueue? DefaultQueue,
    bool IsActive,
    bool MustChangePassword,
    string? TemporaryPassword = null);

public sealed record CreateUserRequest(
    string Name,
    string Email,
    UserRole Role,
    AuditQueue? DefaultQueue,
    string? TempPassword);

public sealed record UpdateUserRequest(
    string Name,
    string Email,
    UserRole Role,
    AuditQueue? DefaultQueue);

public sealed class UserAdminService
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly AuthService _auth;

    public UserAdminService(IAppDbContext db, IPasswordHasher hasher, AuthService auth)
    {
        _db = db;
        _hasher = hasher;
        _auth = auth;
    }

    public async Task<IReadOnlyList<UserListItemDto>> ListAllAsync(CancellationToken ct = default)
    {
        return await _db.Users
            .OrderBy(u => u.Name)
            .Select(u => new UserListItemDto(
                u.Id,
                u.Name,
                u.Email,
                u.Role,
                u.DefaultQueue,
                u.IsActive,
                u.MustChangePassword,
                null))
            .ToListAsync(ct);
    }

    public async Task<UserListItemDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new InvalidOperationException($"Ya existe un usuario con el email {email}.");

        var tempPassword = string.IsNullOrWhiteSpace(request.TempPassword)
            ? PasswordPolicy.GenerateTemporary()
            : request.TempPassword.Trim();
        PasswordPolicy.EnsureValid(tempPassword);

        var user = User.Create(request.Name, email, request.Role, request.DefaultQueue);
        user.SetPassword(_hasher.Hash(tempPassword), mustChangePassword: true);
        _db.Add(user);
        await _db.SaveChangesAsync(ct);

        return new UserListItemDto(
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.DefaultQueue,
            user.IsActive,
            user.MustChangePassword,
            tempPassword);
    }

    public async Task<UserListItemDto> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("Usuario no encontrado.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Id != userId && u.Email == email, ct))
            throw new InvalidOperationException($"Ya existe un usuario con el email {email}.");

        user.UpdateProfile(request.Name, email, request.Role, request.DefaultQueue);
        await _db.SaveChangesAsync(ct);

        return new UserListItemDto(
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.DefaultQueue,
            user.IsActive,
            user.MustChangePassword);
    }

    public async Task DeactivateUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("Usuario no encontrado.");
        user.Deactivate();
        await _db.SaveChangesAsync(ct);
        await _auth.RevokeAllRefreshTokensAsync(userId, ct);
    }
}
