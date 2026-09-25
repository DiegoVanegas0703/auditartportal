using Auditart.Application.Abstractions;
using Auditart.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Auditart.Infrastructure.Gmail;

public sealed class GmailChannelRegistry : IGmailChannelRegistry
{
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<EmailChannel, Lazy<IGmailInboxService>> _inboxes = new();
    private readonly Dictionary<EmailChannel, GmailChannelOptions> _options = new();

    public GmailChannelRegistry(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory;
        foreach (EmailChannel channel in Enum.GetValues<EmailChannel>())
            _options[channel] = ResolveChannelOptions(channel);
    }

    public IGmailInboxService GetInbox(EmailChannel channel)
    {
        if (!_inboxes.TryGetValue(channel, out var lazy))
        {
            lazy = new Lazy<IGmailInboxService>(() => CreateInbox(channel));
            _inboxes[channel] = lazy;
        }

        return lazy.Value;
    }

    public string GetMailbox(EmailChannel channel) =>
        _options[channel].Mailbox.Trim().ToLowerInvariant();

    public IReadOnlyList<EmailChannel> GetEnabledChannels() =>
        _options.Where(pair => pair.Value.Enabled).Select(pair => pair.Key).ToArray();

    private IGmailInboxService CreateInbox(EmailChannel channel)
    {
        var options = _options[channel];
        if (!options.Enabled)
            return new StubGmailInboxService(_loggerFactory.CreateLogger<StubGmailInboxService>());

        var gmailOptions = new GmailOptions
        {
            Enabled = true,
            Mailbox = options.Mailbox,
            ClientId = options.ClientId ?? string.Empty,
            ClientSecret = options.ClientSecret ?? string.Empty,
            RefreshToken = options.RefreshToken ?? string.Empty,
            SearchQuery = options.SearchQuery,
            MaxResults = options.MaxResults,
            MaxAttachmentBytes = options.MaxAttachmentBytes
        };

        return new GmailInboxService(Microsoft.Extensions.Options.Options.Create(gmailOptions));
    }

    private GmailChannelOptions ResolveChannelOptions(EmailChannel channel)
    {
        var root = _configuration.GetSection(GmailOptions.SectionName);
        var section = root.GetSection(channel.ToString());
        var mailbox = section["Mailbox"];
        // Gmail__Mailbox (root) manda en canal General — evita que appsettings General:Mailbox pise el .env
        if (channel == EmailChannel.General)
        {
            var rootMailbox = root["Mailbox"];
            if (!string.IsNullOrWhiteSpace(rootMailbox))
                mailbox = rootMailbox;
        }

        if (string.IsNullOrWhiteSpace(mailbox))
        {
            mailbox = channel == EmailChannel.General
                ? "info@auditart.com.ar"
                : "cronicos@auditart.local";
        }

        var refreshToken = section["RefreshToken"] ?? root["RefreshToken"];
        var sectionEnabled = section.GetValue<bool?>("Enabled");
        var rootEnabled = root.GetValue<bool>("Enabled");
        var enabled = sectionEnabled ?? (channel == EmailChannel.General && rootEnabled);

        if (channel == EmailChannel.Cronicos && string.IsNullOrWhiteSpace(refreshToken))
            enabled = false;

        return new GmailChannelOptions
        {
            Mailbox = mailbox,
            Enabled = enabled,
            ClientId = section["ClientId"] ?? root["ClientId"],
            ClientSecret = section["ClientSecret"] ?? root["ClientSecret"],
            RefreshToken = refreshToken,
            SearchQuery = section["SearchQuery"] ?? root["SearchQuery"] ?? "is:unread in:inbox",
            MaxResults = section.GetValue<int?>("MaxResults") ?? root.GetValue("MaxResults", 50),
            MaxAttachmentBytes = section.GetValue<long?>("MaxAttachmentBytes")
                ?? root.GetValue("MaxAttachmentBytes", 25L * 1024 * 1024)
        };
    }
}
