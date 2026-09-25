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

    /// <summary>Catálogo de precios conciliados: Admin, Jefatura y Facturación.</summary>
    public static bool CanManagePrecios(UserRole role) =>
        role is UserRole.Admin or UserRole.Jefatura or UserRole.Facturacion;

    public static bool CanManageUsers(UserRole role) =>
        role is UserRole.Admin;

    public static bool CanViewReports(UserRole role) =>
        role is UserRole.Admin or UserRole.Jefatura;

    public static bool SeesAllQueues(UserRole role) =>
        role is UserRole.Admin or UserRole.Jefatura;

    public static bool CanAccessChannel(UserRole role, EmailChannel channel) =>
        role switch
        {
            UserRole.Admin => true,
            UserRole.Cronicos => channel == EmailChannel.Cronicos,
            _ => channel == EmailChannel.General
        };
}
