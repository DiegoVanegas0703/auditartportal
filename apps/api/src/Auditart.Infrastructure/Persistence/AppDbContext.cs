using Auditart.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Infrastructure.Persistence;

public class AppDbContext : DbContext, Application.Abstractions.IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditService> AuditServices => Set<AuditService>();
    public DbSet<IncomingEmail> IncomingEmails => Set<IncomingEmail>();
    public DbSet<ServiceAttachment> ServiceAttachments => Set<ServiceAttachment>();
    public DbSet<ServiceStatusHistory> ServiceStatusHistories => Set<ServiceStatusHistory>();
    public DbSet<SlaRule> SlaRules => Set<SlaRule>();
    public DbSet<InAppAlert> InAppAlerts => Set<InAppAlert>();
    public DbSet<Prestador> Prestadores => Set<Prestador>();
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<PrecioCatalogo> PreciosCatalogo => Set<PrecioCatalogo>();
    public DbSet<EmailRequest> EmailRequests => Set<EmailRequest>();
    public DbSet<EmailThread> EmailThreads => Set<EmailThread>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<EmailAttachment> EmailAttachments => Set<EmailAttachment>();
    public DbSet<EmailSendOutbox> EmailSendOutboxItems => Set<EmailSendOutbox>();
    public DbSet<GmailSyncState> GmailSyncStates => Set<GmailSyncState>();

    IQueryable<User> Application.Abstractions.IAppDbContext.Users => Users;
    IQueryable<AuditService> Application.Abstractions.IAppDbContext.AuditServices => AuditServices;
    IQueryable<IncomingEmail> Application.Abstractions.IAppDbContext.IncomingEmails => IncomingEmails;
    IQueryable<ServiceAttachment> Application.Abstractions.IAppDbContext.ServiceAttachments => ServiceAttachments;
    IQueryable<ServiceStatusHistory> Application.Abstractions.IAppDbContext.ServiceStatusHistories => ServiceStatusHistories;
    IQueryable<SlaRule> Application.Abstractions.IAppDbContext.SlaRules => SlaRules;
    IQueryable<InAppAlert> Application.Abstractions.IAppDbContext.InAppAlerts => InAppAlerts;
    IQueryable<Prestador> Application.Abstractions.IAppDbContext.Prestadores => Prestadores;
    IQueryable<Paciente> Application.Abstractions.IAppDbContext.Pacientes => Pacientes;
    IQueryable<PrecioCatalogo> Application.Abstractions.IAppDbContext.PreciosCatalogo => PreciosCatalogo;
    IQueryable<RefreshToken> Application.Abstractions.IAppDbContext.RefreshTokens => RefreshTokens;
    IQueryable<EmailRequest> Application.Abstractions.IAppDbContext.EmailRequests => EmailRequests;
    IQueryable<EmailThread> Application.Abstractions.IAppDbContext.EmailThreads => EmailThreads;
    IQueryable<EmailMessage> Application.Abstractions.IAppDbContext.EmailMessages => EmailMessages;
    IQueryable<EmailAttachment> Application.Abstractions.IAppDbContext.EmailAttachments => EmailAttachments;
    IQueryable<EmailSendOutbox> Application.Abstractions.IAppDbContext.EmailSendOutbox => EmailSendOutboxItems;
    IQueryable<GmailSyncState> Application.Abstractions.IAppDbContext.GmailSyncStates => GmailSyncStates;

    public new void Add<T>(T entity) where T : class => Set<T>().Add(entity);
    public new void Remove<T>(T entity) where T : class => Set<T>().Remove(entity);
    public void ClearChanges() => ChangeTracker.Clear();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Todas las entidades generan Guid en dominio. Si EF cree que el Id es
        // store-generated, los hijos agregados a colecciones de padres tracked
        // quedan en Modified y SaveChanges falla con DbUpdateConcurrencyException.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var id = entityType.FindProperty(nameof(Domain.Common.Entity.Id));
            if (id?.ClrType == typeof(Guid))
                id.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        }

        base.OnModelCreating(modelBuilder);
    }
}
