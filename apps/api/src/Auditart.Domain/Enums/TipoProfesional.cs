namespace Auditart.Domain.Enums;

/// <summary>
/// Modalidad de honorarios en Rojo: define de qué catálogo sale el precio conciliado.
/// Auditor → precios por ART; Especialista → catálogo VALOR PRESTADORES.
/// </summary>
public enum TipoProfesional
{
    Auditor = 0,
    Especialista = 1
}
