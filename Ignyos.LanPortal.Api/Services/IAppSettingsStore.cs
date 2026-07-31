namespace Ignyos.LanPortal.Api.Services;

public interface IAppSettingsStore
{
    void Initialize();

    JwtDatabaseConfig GetJwtConfig();

    string? GetStorageRootPath();

    void SetStorageRootPath(string rootPath);

    bool IsSetupComplete();

    void RecordIssuedAccessSession(AccessSessionRecord record);

    void UpsertRefreshToken(Guid sessionId, string refreshTokenHash, DateTimeOffset refreshTokenExpiresAtUtc);

    AccessSessionRecord? GetActiveAccessSessionByRefreshTokenHash(string refreshTokenHash);

    bool RefreshAccessSession(Guid sessionId, string refreshTokenHash, string newJti, DateTimeOffset issuedAtUtc);

    bool UpdateAccessSessionRoles(Guid sessionId, string roles, string changedBy, string reason, DateTimeOffset changedAtUtc);

    IReadOnlyList<RoleChangeAuditRecord> GetRecentRoleChanges(int maxCount = 100);

    bool IsAccessTokenActive(string jti);

    IReadOnlyList<AccessSessionRecord> GetActiveAccessSessions(int maxCount = 250);

    bool RevokeAccessSession(Guid sessionId, string reason);

    AccessSessionRecord? RevokeAccessSessionByJti(string jti, string reason);

    int RevokeAccessByUserDevice(string? userName, string? deviceName, string reason);
}

public sealed record JwtDatabaseConfig(string Issuer, string Audience, string SigningKey);

public sealed record AccessSessionRecord(
    Guid SessionId,
    string Jti,
    string UserName,
    string DeviceName,
    string Roles,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    string? RevokedReason,
    DateTimeOffset LastSeenAtUtc);

public sealed record RoleChangeAuditRecord(
    Guid AuditId,
    Guid SessionId,
    string UserName,
    string DeviceName,
    string PreviousRoles,
    string NewRoles,
    string ChangedBy,
    string Reason,
    DateTimeOffset ChangedAtUtc);
