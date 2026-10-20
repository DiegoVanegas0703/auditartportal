using Auditart.Domain.Enums;

namespace Auditart.Domain.Entities;

public static class AuditStatusMachine
{
    private static readonly Dictionary<AuditStatus, AuditStatus[]> Allowed = new()
    {
        [AuditStatus.Rojo] = [AuditStatus.Amarillo],
        // Adelante y atrás (devolver estado cuando hace falta corregir).
        [AuditStatus.Amarillo] = [AuditStatus.Azul, AuditStatus.Verde, AuditStatus.Rojo],
        [AuditStatus.Azul] = [AuditStatus.Verde, AuditStatus.Amarillo],
        [AuditStatus.Verde] = [AuditStatus.Celeste, AuditStatus.Azul, AuditStatus.Amarillo],
        [AuditStatus.Celeste] = [AuditStatus.Verde],
    };

    public static bool CanTransition(AuditStatus from, AuditStatus to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public static bool IsBackward(AuditStatus from, AuditStatus to) =>
        Order(to) < Order(from);

    private static int Order(AuditStatus s) => s switch
    {
        AuditStatus.Rojo => 0,
        AuditStatus.Amarillo => 1,
        AuditStatus.Azul => 2,
        AuditStatus.Verde => 3,
        AuditStatus.Celeste => 4,
        _ => -1
    };
}
