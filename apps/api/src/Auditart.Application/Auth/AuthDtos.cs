using Auditart.Domain.Enums;

namespace Auditart.Application.Auth;

public sealed record AuthUserDto(
    Guid Id,
    string Name,
    string Email,
    UserRole Role,
    AuditQueue? DefaultQueue,
    bool MustChangePassword,
    AuthPermissionsDto Permissions);

public sealed record AuthPermissionsDto(
    bool Triage,
    bool OperationalBoard,
    bool Billing,
    bool AllQueues,
    bool ManageUsers,
    bool Reports,
    bool Precios);

public sealed record AuthTokensDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    AuthUserDto User);

public sealed record LoginRequest(string Email, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record RefreshRequest(string RefreshToken);
