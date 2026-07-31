namespace Ignyos.LanPortal.Contracts;

public sealed record FileEntryDto(
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastModifiedUtc);

public sealed record UploadResultDto(
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastModifiedUtc);

public sealed record DeviceLoginStartRequestDto(
    string UserName,
    string DeviceName);

public sealed record DeviceLoginStartResponseDto(
    Guid RequestId,
    string UserCode,
    DateTimeOffset ExpiresAtUtc,
    int PollIntervalSeconds);

public sealed record DeviceLoginPollRequestDto(
    Guid RequestId,
    string UserCode);

public sealed record DeviceLoginPollResponseDto(
    string Status,
    string? AccessToken,
    DateTimeOffset? AccessTokenExpiresAtUtc,
    string? RefreshToken,
    DateTimeOffset? RefreshTokenExpiresAtUtc,
    string? Message);

public sealed record RefreshTokenRequestDto(string RefreshToken);

public sealed record RefreshTokenResponseDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

public sealed record PendingLoginRequestDto(
    Guid RequestId,
    string RequestedUserName,
    string DeviceName,
    string? SourceIp,
    string? UserAgent,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record ApproveLoginRequestDto(
    string UserName,
    string Roles,
    int TokenMinutes);

public sealed record DenyLoginRequestDto(string? Reason);

public sealed record AccessSessionDto(
    Guid SessionId,
    string UserName,
    string DeviceName,
    string Roles,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset LastSeenAtUtc,
    bool IsCurrentSession);

public sealed record RevokeSessionRequestDto(string? Reason);

public sealed record RevokeByFilterRequestDto(
    string? UserName,
    string? DeviceName,
    string? Reason);

public sealed record RevokeByFilterResponseDto(int RevokedCount);

public sealed record UpdateSessionRolesRequestDto(
    string Roles,
    string? Reason);

public sealed record UpdateSessionRolesResponseDto(
    Guid SessionId,
    string Roles,
    DateTimeOffset ChangedAtUtc);

public sealed record WhoAmIResponseDto(
    string UserName,
    List<string> Roles,
    string? Jti,
    bool IsAuthenticated,
    List<WhoAmIClaimDto>? AllClaims);

public sealed record WhoAmIClaimDto(string Type, string Value);
