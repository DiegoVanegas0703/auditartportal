using Auditart.Domain.Common;

namespace Auditart.Domain.Entities;

public class AiAssistant : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? SystemPrompt { get; private set; }
    public string? ExternalGemUrl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int SortOrder { get; private set; }

    private AiAssistant() { }

    public static AiAssistant Create(
        string name,
        string? description,
        string? systemPrompt,
        string? externalGemUrl,
        int sortOrder = 0)
    {
        return new AiAssistant
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            SystemPrompt = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt.Trim(),
            ExternalGemUrl = string.IsNullOrWhiteSpace(externalGemUrl) ? null : externalGemUrl.Trim(),
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    public void Update(
        string name,
        string? description,
        string? systemPrompt,
        string? externalGemUrl,
        bool isActive,
        int sortOrder)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        SystemPrompt = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt.Trim();
        ExternalGemUrl = string.IsNullOrWhiteSpace(externalGemUrl) ? null : externalGemUrl.Trim();
        IsActive = isActive;
        SortOrder = sortOrder;
        Touch();
    }
}
