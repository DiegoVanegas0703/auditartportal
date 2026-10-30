namespace Auditart.Application.Abstractions;

public interface ILoginLockoutService
{
    bool IsLocked(string email, string? clientKey, out string? message);
    void RegisterFailure(string email, string? clientKey);
    void RegisterSuccess(string email, string? clientKey);
}
