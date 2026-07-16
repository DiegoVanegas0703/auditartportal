using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Auditart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Auditart.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync())
            return;

        logger.LogInformation("Sembrando usuarios mock iniciales...");

        var users = new[]
        {
            User.Create("Diego Santamarina", "diego.santamarina@auditart.com.ar", UserRole.Admin),
            User.Create("María González", "maria.gonzalez@auditart.com.ar", UserRole.Jefatura),
            User.Create("Pablo Rodríguez", "pablo.rodriguez@auditart.com.ar", UserRole.Operador, AuditQueue.General),
            User.Create("Laura Fernández", "laura.fernandez@auditart.com.ar", UserRole.Operador, AuditQueue.General),
            User.Create("Martín Acosta", "martin.acosta@auditart.com.ar", UserRole.Operador, AuditQueue.General),
            User.Create("Aylen Martínez", "aylen.martinez@auditart.com.ar", UserRole.Telemedicina, AuditQueue.Telemedicina),
            User.Create("Carolina Ruiz", "carolina.ruiz@auditart.com.ar", UserRole.Cronicos, AuditQueue.Cronicos),
            User.Create("Damián López", "damian.lopez@auditart.com.ar", UserRole.Facturacion),
            // Cuenta de prueba Google para desarrollo
            User.Create("Dev Auditart", "developmentcode@gmail.com", UserRole.Admin)
        };

        db.Users.AddRange(users);
        await db.SaveChangesAsync();
        logger.LogInformation("Seed de usuarios completado ({Count}).", users.Length);
    }
}
