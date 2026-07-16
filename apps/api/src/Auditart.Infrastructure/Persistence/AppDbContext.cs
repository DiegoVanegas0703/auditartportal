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

    IQueryable<User> Application.Abstractions.IAppDbContext.Users => Users;
    IQueryable<AuditService> Application.Abstractions.IAppDbContext.AuditServices => AuditServices;
    IQueryable<IncomingEmail> Application.Abstractions.IAppDbContext.IncomingEmails => IncomingEmails;
    IQueryable<ServiceAttachment> Application.Abstractions.IAppDbContext.ServiceAttachments => ServiceAttachments;
    IQueryable<RefreshToken> Application.Abstractions.IAppDbContext.RefreshTokens => RefreshTokens;

    public new void Add<T>(T entity) where T : class => Set<T>().Add(entity);
    public new void Remove<T>(T entity) where T : class => Set<T>().Remove(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
