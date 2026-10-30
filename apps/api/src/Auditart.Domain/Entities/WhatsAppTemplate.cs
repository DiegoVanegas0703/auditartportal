using Auditart.Domain.Common;

namespace Auditart.Domain.Entities;

/// <summary>Plantilla editable de aviso de turno por WhatsApp (turno presencial o telemedicina).</summary>
public class WhatsAppTemplate : Entity
{
    public const string KeyTurno = "turno";
    public const string KeyTelemedicina = "telemedicina";

    public string Key { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    private WhatsAppTemplate() { }

    public static WhatsAppTemplate Create(string key, string body)
    {
        var entity = new WhatsAppTemplate();
        entity.Key = (key ?? string.Empty).Trim().ToLowerInvariant();
        entity.SetBody(body);
        return entity;
    }

    public void SetBody(string body)
    {
        Body = (body ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(Body))
            throw new Domain.Exceptions.DomainException("La plantilla no puede quedar vacía.");
        Touch();
    }
}
