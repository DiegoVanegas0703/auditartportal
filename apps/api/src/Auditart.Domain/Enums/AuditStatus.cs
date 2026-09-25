namespace Auditart.Domain.Enums;

/// <summary>
/// Flujo operativo: Rojo → Amarillo → Azul → Verde → Celeste.
/// Amarillo puede ir a Verde si la autorización ya está cargada.
/// </summary>
public enum AuditStatus
{
    Rojo = 0,
    Amarillo = 1,
    Azul = 2,
    Verde = 3,
    /// <summary>Pago realizado; prestación cerrada.</summary>
    Celeste = 4
}
