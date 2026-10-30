namespace Auditart.Application.Auth;

public sealed class LoginThrottledException : Exception
{
    public LoginThrottledException(string message) : base(message) { }
}
