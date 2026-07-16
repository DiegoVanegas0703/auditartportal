using System.Text;
using Auditart.Application.Abstractions;
using Auditart.Application.Auth;
using Auditart.Infrastructure.Auth;
using Auditart.Infrastructure.Gmail;
using Auditart.Infrastructure.Persistence;
using Auditart.Infrastructure.Storage;
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
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<IObjectStorage, S3ObjectStorage>();
        services.AddScoped<IGmailInboxService, StubGmailInboxService>();
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
