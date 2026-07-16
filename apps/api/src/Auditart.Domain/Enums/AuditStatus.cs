namespace Auditart.Domain.Enums;

/// <summary>
/// Máquina de estados operativa (colores del Excel).
/// Transiciones válidas: Rojo → Amarillo → Azul → Verde.
/// </summary>
public enum AuditStatus
{
    Rojo = 0,
    Amarillo = 1,
    Azul = 2,
    Verde = 3
}
