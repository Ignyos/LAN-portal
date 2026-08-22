namespace Ignyos.LanPortal.Contracts;

public static class PermissionKeys
{
    public const string Read = "file:read";
    public const string Add = "file:add";
    public const string Rename = "file:rename";
    public const string Move = "file:move";
    public const string Delete = "file:delete";
    public const string Upload = "file:upload";
    public const string Download = "file:download";
    public const string Search = "file:search";

    public static readonly IReadOnlyList<string> All =
    [
        Read,
        Add,
        Rename,
        Move,
        Delete,
        Upload,
        Download,
        Search
    ];
}

public static class PermissionClaimTypes
{
    public const string Permission = "perm";
}

public static class FileEventTypes
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Deleted = "deleted";
    public const string Renamed = "renamed";
    public const string Moved = "moved";
    public const string Batch = "batch";
}

public sealed record FileEntryDto(
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastModifiedUtc);

public sealed record UploadResultDto(
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastModifiedUtc);

public sealed record FileNodeDto(
    string Path,
    string Name,
    bool IsFolder,
    long? SizeBytes,
    DateTimeOffset? LastModifiedUtc);

public sealed record FolderListRequestDto(string CurrentPath);

public sealed record FolderListResponseDto(
    string CurrentPath,
    IReadOnlyList<FileNodeDto> Items);

public sealed record TreeNodeChildrenRequestDto(string ParentPath);

public sealed record TreeNodeChildrenResponseDto(
    string ParentPath,
    IReadOnlyList<FileNodeDto> Children);

public sealed record FileSearchRequestDto(
    string Query,
    string? SearchRootPath,
    int? MaxResults);

public sealed record FileSearchResponseDto(
    string Query,
    IReadOnlyList<FileNodeDto> Items);

public sealed record CreateFolderRequestDto(string CurrentPath, string Name);

public sealed record RenameItemRequestDto(string Path, string NewName);

public sealed record MoveItemsRequestDto(
    IReadOnlyList<string> Paths,
    string DestinationPath);

public sealed record DeleteItemsRequestDto(IReadOnlyList<string> Paths);

public sealed record ConflictResponseDto(
    string Code,
    string Message,
    string? Path,
    string? CorrelationId);

public sealed record FileChangeItemDto(
    string Path,
    string Name,
    bool IsFolder,
    long? SizeBytes,
    DateTimeOffset? LastModifiedUtc);

public sealed record FileChangeEventDto(
    string SchemaVersion,
    string EventId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string ScopePath,
    string? CorrelationId,
    string? BatchId,
    string? FromPath,
    string? ToPath,
    FileChangeItemDto? Item);

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
    DateTimeOffset? RefreshTokenExpiresAtUtc);

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
    int? TokenMinutes,
    string? DeviceName = null);

public sealed record DenyLoginRequestDto(string? Reason);

public sealed record AccessSessionDto(
    Guid SessionId,
    string UserName,
    string DeviceName,
    string Roles,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
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
    List<string> Permissions,
    string? Jti,
    bool IsAuthenticated,
    List<WhoAmIClaimDto>? AllClaims);

public sealed record WhoAmIClaimDto(string Type, string Value);
