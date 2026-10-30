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

    /// <summary>Alta manual de pacientes: Admin y Jefatura.</summary>
    public static bool CanCreatePacientes(UserRole role) =>
        role is UserRole.Admin or UserRole.Jefatura;

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

    /// <summary>
    /// Mismos criterios que el listado del tablero: no se puede abrir un caso de otra cola/operador.
    /// </summary>
    public static bool CanViewService(
        UserRole role,
        Guid userId,
        AuditQueue queue,
        AuditStatus status,
        Guid? operadorId)
    {
        if (role is UserRole.Admin or UserRole.Jefatura)
            return true;

        if (role == UserRole.Facturacion)
            return status is AuditStatus.Verde or AuditStatus.Celeste;

        if (role == UserRole.Telemedicina)
            return queue == AuditQueue.Telemedicina;

        if (role == UserRole.Cronicos)
            return queue == AuditQueue.Cronicos;

        if (role == UserRole.Operador)
            return queue == AuditQueue.General && operadorId == userId;

        return false;
    }
}
