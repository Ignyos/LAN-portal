namespace Ignyos.LanPortal.Api.Services;

public interface ISessionLifecycleService
{
    AccessSessionRecord? Revoke(Guid sessionId, string reason);

    AccessSessionRecord? Logout(string jti, string reason);

    IReadOnlyList<AccessSessionRecord> RevokeByFilter(string? userName, string? deviceName, string reason);
}
