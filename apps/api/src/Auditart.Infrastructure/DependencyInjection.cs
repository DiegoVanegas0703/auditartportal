using System.Text;
using Auditart.Application.Abstractions;
using Auditart.Application.Auth;
using Auditart.Application.Triage;
using Auditart.Application.Users;
using Auditart.Application.Prestadores;
using Auditart.Infrastructure.Auth;
using Auditart.Infrastructure.Gmail;
using Auditart.Infrastructure.Persistence;
using Auditart.Infrastructure.Storage;
using Auditart.Application.Chronic;
using Auditart.Application.Reports;
using Auditart.Application.Sla;
using Auditart.Infrastructure.Chronic;
using Auditart.Infrastructure.Sla;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Auditart.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IObjectStorage, S3ObjectStorage>();
        services.Configure<GmailOptions>(configuration.GetSection(GmailOptions.SectionName));
        services.AddSingleton<IGmailChannelRegistry, GmailChannelRegistry>();
        services.AddScoped<GmailIngestionService>();
        services.AddScoped<EmailConversationService>();
        services.AddScoped<EmailOutboxProcessor>();
        services.AddScoped<DenunciaPdfParser>();
        services.AddScoped<UserAdminService>();
        services.AddScoped<PrestadorService>();
        services.AddScoped<Auditart.Application.Pacientes.PacienteService>();
        services.AddScoped<Auditart.Application.Precios.PrecioCatalogoService>();
        services.AddHostedService<GmailPollingWorker>();
        services.AddHostedService<EmailOutboxWorker>();
        services.AddScoped<ChronicServiceService>();
        services.AddScoped<ReportsService>();
        services.AddScoped<SlaRuleService>();
        services.AddScoped<InAppAlertService>();
        services.Configure<ChronicRenewalOptions>(configuration.GetSection(ChronicRenewalOptions.SectionName));
        services.AddHostedService<ChronicRenewalWorker>();
        services.Configure<SlaAlertOptions>(configuration.GetSection(SlaAlertOptions.SectionName));
        services.AddHostedService<SlaAlertWorker>();
        services.AddScoped<AuthService>();

        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key requerido.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "auditart",
                    ValidAudience = configuration["Jwt:Audience"] ?? "auditart-web",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();
        return services;
    }
}
