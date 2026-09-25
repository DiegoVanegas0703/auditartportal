using Auditart.Domain.Enums;

namespace Auditart.Domain.Entities;

public static class AuditStatusMachine
{
    private static readonly Dictionary<AuditStatus, AuditStatus[]> Allowed = new()
    {
        [AuditStatus.Rojo] = [AuditStatus.Amarillo],
        [AuditStatus.Amarillo] = [AuditStatus.Azul, AuditStatus.Verde],
        [AuditStatus.Azul] = [AuditStatus.Verde],
        [AuditStatus.Verde] = [AuditStatus.Celeste],
        [AuditStatus.Celeste] = []
    };

    public static bool CanTransition(AuditStatus from, AuditStatus to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);
}
