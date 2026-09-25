using Auditart.Application.Abstractions;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Auditart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Auditart.Infrastructure.Persistence;

public static class DbSeeder
{
    private const string DefaultPassword = "Test";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        await db.Database.MigrateAsync();

        logger.LogInformation("Verificando usuarios iniciales...");

        var passwordHash = hasher.Hash(DefaultPassword);

        var seedUsers = new (string Name, string Email, UserRole Role, AuditQueue? Queue)[]
        {
            ("Admin Auditart", "admin@auditart.local", UserRole.Admin, null),
            ("Jefatura Auditart", "jefatura@auditart.local", UserRole.Jefatura, null),
            ("Operador General", "operador@auditart.local", UserRole.Operador, AuditQueue.General),
            ("Telemedicina Auditart", "telemedicina@auditart.local", UserRole.Telemedicina, AuditQueue.Telemedicina),
            ("Cronicos Auditart", "cronicos@auditart.local", UserRole.Cronicos, AuditQueue.Cronicos),
            ("Facturacion Auditart", "facturacion@auditart.local", UserRole.Facturacion, null),
            ("Diego Santamarina", "diego.santamarina@auditart.com.ar", UserRole.Admin, null),
            ("Diego Vanegas Cortes", "diego.vanegas.cortes@gmail.com", UserRole.Admin, null),
            ("María González", "maria.gonzalez@auditart.com.ar", UserRole.Jefatura, null),
            ("Pablo Rodríguez", "pablo.rodriguez@auditart.com.ar", UserRole.Operador, AuditQueue.General),
            ("Laura Fernández", "laura.fernandez@auditart.com.ar", UserRole.Operador, AuditQueue.General),
            ("Martín Acosta", "martin.acosta@auditart.com.ar", UserRole.Operador, AuditQueue.General),
            ("Aylen Martínez", "aylen.martinez@auditart.com.ar", UserRole.Telemedicina, AuditQueue.Telemedicina),
            ("Carolina Ruiz", "carolina.ruiz@auditart.com.ar", UserRole.Cronicos, AuditQueue.Cronicos),
            ("Damián López", "damian.lopez@auditart.com.ar", UserRole.Facturacion, null),
            ("Dev Auditart", "dvelopmentcode@gmail.com", UserRole.Admin, null)
        };

        var existingUsers = await db.Users.ToListAsync();
        var byEmail = existingUsers.ToDictionary(u => u.Email, StringComparer.OrdinalIgnoreCase);
        var created = 0;
        var updated = 0;

        foreach (var (name, email, role, queue) in seedUsers)
        {
            if (byEmail.TryGetValue(email, out var existing))
            {
                if (string.IsNullOrEmpty(existing.PasswordHash))
                {
                    existing.SetPassword(passwordHash, mustChangePassword: true);
                    updated++;
                }

                continue;
            }

            var user = User.Create(name, email, role, queue);
            user.SetPassword(passwordHash, mustChangePassword: true);
            db.Users.Add(user);
            created++;
        }

        if (created > 0 || updated > 0)
            await db.SaveChangesAsync();

        await SeedSlaRulesAsync(db, logger);

        logger.LogInformation(
            "Seed de usuarios completado ({Created} nuevos, {Updated} con contraseña).",
            created,
            updated);
    }

    private static async Task SeedSlaRulesAsync(AppDbContext db, ILogger logger)
    {
        var statuses = new[]
        {
            AuditStatus.Rojo,
            AuditStatus.Amarillo,
            AuditStatus.Azul,
            AuditStatus.Verde
        };

        var existing = await db.SlaRules
            .Select(r => new { r.Queue, r.Status })
            .ToListAsync();
        var set = existing.Select(x => (x.Queue, x.Status)).ToHashSet();
        var added = 0;

        foreach (var queue in Enum.GetValues<AuditQueue>())
        {
            foreach (var status in statuses)
            {
                if (set.Contains((queue, status))) continue;
                var (value, unit, warn) = status switch
                {
                    AuditStatus.Rojo => (24, SlaDurationUnit.Hours, 4),
                    AuditStatus.Amarillo => (24, SlaDurationUnit.Hours, 4),
                    AuditStatus.Azul => (48, SlaDurationUnit.Hours, 12),
                    AuditStatus.Verde => (5, SlaDurationUnit.Days, 24),
                    _ => (24, SlaDurationUnit.Hours, 4)
                };
                db.SlaRules.Add(SlaRule.Create(queue, status, value, unit, warn));
                added++;
            }
        }

        if (added > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Seed de reglas SLA: {Count} creadas.", added);
        }
    }
}
