using Auditart.Application.Abstractions;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Auditart.Infrastructure.Auth;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _env;

    public GoogleAuthService(IConfiguration configuration, IHostEnvironment env)
    {
        _configuration = configuration;
        _env = env;
    }

    public async Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        // Modo desarrollo: token "dev:<email>" para probar sin Google Cloud Console.
        if (_env.IsDevelopment() && idToken.StartsWith("dev:", StringComparison.OrdinalIgnoreCase))
        {
            var email = idToken["dev:".Length..].Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
                throw new UnauthorizedAccessException("Token de desarrollo inválido.");

            return new GoogleUserInfo(
                SubjectId: $"dev-{email}",
                Email: email,
                Name: email.Split('@')[0],
                EmailVerified: true);
        }

        var clientId = _configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Google:ClientId no configurado.");

        var payload = await GoogleJsonWebSignature.ValidateAsync(
            idToken,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId]
            });

        return new GoogleUserInfo(
            payload.Subject,
            payload.Email,
            payload.Name ?? payload.Email,
            payload.EmailVerified);
    }
}
