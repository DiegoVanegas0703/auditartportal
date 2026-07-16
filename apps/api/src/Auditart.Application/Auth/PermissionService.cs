using Auditart.Domain.Enums;

namespace Auditart.Application.Auth;

public static class PermissionService
{
    public static bool CanTriage(UserRole role) =>
        role is UserRole.Admin or UserRole.Jefatura;

    public static bool CanOperateBoard(UserRole role) =>
        role is UserRole.Admin or UserRole.Jefatura or UserRole.Operador
            or UserRole.Telemedicina or UserRole.Cronicos;

    public static bool CanBill(UserRole role) =>
        role is UserRole.Admin or UserRole.Facturacion;

    public static bool CanManageUsers(UserRole role) =>
        role is UserRole.Admin;

    public static bool SeesAllQueues(UserRole role) =>
        role is UserRole.Admin or UserRole.Jefatura;
}
