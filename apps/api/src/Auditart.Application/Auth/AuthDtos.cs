using Auditart.Domain.Enums;

namespace Auditart.Application.Auth;

public sealed record AuthUserDto(
    Guid Id,
    string Name,
    string Email,
    UserRole Role,
    AuditQueue? DefaultQueue,
    AuthPermissionsDto Permissions);

public sealed record AuthPermissionsDto(
    bool Triage,
    bool OperationalBoard,
    bool Billing,
    bool AllQueues,
    bool ManageUsers);

public sealed record AuthTokensDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    AuthUserDto User);

public sealed record GoogleLoginRequest(string IdToken);
public sealed record RefreshRequest(string RefreshToken);
