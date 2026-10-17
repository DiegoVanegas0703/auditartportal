namespace Auditart.Infrastructure.WhatsApp;

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    public bool Enabled { get; set; }
    public string PhoneNumberId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "v21.0";
    public string WebhookVerifyToken { get; set; } = string.Empty;
    /// <summary>Plantilla Meta para avisos de estado (debe estar aprobada).</summary>
    public string StatusTemplateName { get; set; } = "auditart_estado_actualizado";
    public string StatusTemplateLanguage { get; set; } = "es_AR";
    /// <summary>Estados que disparan aviso automático (coma-separados). Ej: Amarillo,Verde</summary>
    public string NotifyOnStatuses { get; set; } = "Amarillo,Verde";
}
