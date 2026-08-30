using Ignyos.LanPortal.Contracts;
using Ignyos.LanPortal.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Ignyos.LanPortal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class FilesController(
    IAppSettingsStore settingsStore,
    IFileEventPublisher fileEventPublisher,
    IApplicationLogStore applicationLogStore) : ControllerBase
{
    private static readonly EnumerationOptions StorageEnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true
    };

    [HttpGet]
    public ActionResult<IReadOnlyList<FileEntryDto>> ListFiles()
    {
        if (!FilePermissionService.HasPermission(User, PermissionKeys.Read))
        {
            return Forbid();
        }

        var configuredStoragePath = settingsStore.GetStorageRootPath();
        if (string.IsNullOrWhiteSpace(configuredStoragePath))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Storage root path is not configured. Run local setup on Machine A.");
        }

        var rootPath = StoragePathResolver.EnsureStorageRoot(configuredStoragePath);

        var files = Directory.EnumerateFiles(rootPath, "*", StorageEnumerationOptions)
            .Select(path =>
            {
                var info = new FileInfo(path);
                var relativePath = StoragePathResolver.ToRelativePath(rootPath, path);
                return new FileEntryDto(relativePath, info.Length, info.LastWriteTimeUtc);
            })
            .OrderBy(entry => entry.RelativePath)
            .ToArray();

        return Ok(files);
    }

    [HttpGet("folder")]
    public ActionResult<FolderListResponseDto> ListFolder([FromQuery] string? currentPath = null)
    {
        if (!FilePermissionService.HasPermission(User, PermissionKeys.Read))
        {
            return Forbid();
        }

        if (!TryGetStorageRoot(out var rootPath, out var unavailableResult))
        {
            return unavailableResult!;
        }

        if (!StoragePathResolver.TryResolveOptionalPathUnderRoot(rootPath!, currentPath, out var folderPath) ||
            string.IsNullOrWhiteSpace(folderPath) ||
            !Directory.Exists(folderPath))
        {
            return BadRequest("Invalid folder path.");
        }

        var entries = Directory
            .EnumerateFileSystemEntries(folderPath)
            .Select(path =>
            {
                if (Directory.Exists(path))
                {
                    return new FileNodeDto(
                        Path: StoragePathResolver.ToRelativePath(rootPath!, path),
                        Name: Path.GetFileName(path),
                        IsFolder: true,
                        SizeBytes: null,
                        LastModifiedUtc: Directory.GetLastWriteTimeUtc(path));
                }

                var info = new FileInfo(path);
                return new FileNodeDto(
                    Path: StoragePathResolver.ToRelativePath(rootPath!, path),
                    Name: info.Name,
                    IsFolder: false,
                    SizeBytes: info.Length,
                    LastModifiedUtc: info.LastWriteTimeUtc);
            })
            .OrderByDescending(item => item.IsFolder)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var relativeCurrentPath = StoragePathResolver.ToRelativePath(rootPath!, folderPath);
        return Ok(new FolderListResponseDto(relativeCurrentPath, entries));
    }

    [HttpGet("tree/children")]
    public ActionResult<TreeNodeChildrenResponseDto> ListTreeChildren([FromQuery] string? parentPath = null)
    {
        if (!FilePermissionService.HasPermission(User, PermissionKeys.Read))
        {
            return Forbid();
        }

        if (!TryGetStorageRoot(out var rootPath, out var unavailableResult))
        {
            return unavailableResult!;
        }

        if (!StoragePathResolver.TryResolveOptionalPathUnderRoot(rootPath!, parentPath, out var fullParentPath) ||
            string.IsNullOrWhiteSpace(fullParentPath) ||
            !Directory.Exists(fullParentPath))
        {
            return BadRequest("Invalid parent path.");
        }

        var children = Directory
            .EnumerateDirectories(fullParentPath)
            .Select(path => new FileNodeDto(
                Path: StoragePathResolver.ToRelativePath(rootPath!, path),
                Name: Path.GetFileName(path),
                IsFolder: true,
                SizeBytes: null,
                LastModifiedUtc: Directory.GetLastWriteTimeUtc(path)))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var relativeParentPath = StoragePathResolver.ToRelativePath(rootPath!, fullParentPath);
        return Ok(new TreeNodeChildrenResponseDto(relativeParentPath, children));
    }

    [HttpPost("search")]
    public ActionResult<FileSearchResponseDto> Search([FromBody] FileSearchRequestDto request)
    {
        if (!FilePermissionService.HasPermission(User, PermissionKeys.Search))
        {
            return Forbid();
        }

        var query = request.Query?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Search query is required.");
        }

        if (!TryGetStorageRoot(out var rootPath, out var unavailableResult))
        {
            return unavailableResult!;
        }

        if (!StoragePathResolver.TryResolveOptionalPathUnderRoot(rootPath!, request.SearchRootPath, out var fullSearchRootPath) ||
            string.IsNullOrWhiteSpace(fullSearchRootPath) ||
            !Directory.Exists(fullSearchRootPath))
        {
            return BadRequest("Invalid search root path.");
        }

        var maxResults = Math.Clamp(request.MaxResults ?? 250, 1, 2000);
        var matches = new List<FileNodeDto>(capacity: Math.Min(maxResults, 256));

        foreach (var entryPath in Directory.EnumerateFileSystemEntries(fullSearchRootPath, "*", StorageEnumerationOptions))
        {
            var name = Path.GetFileName(entryPath);
            if (string.IsNullOrWhiteSpace(name) ||
                name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (Directory.Exists(entryPath))
            {
                matches.Add(new FileNodeDto(
                    Path: StoragePathResolver.ToRelativePath(rootPath!, entryPath),
                    Name: name,
                    IsFolder: true,
                    SizeBytes: null,
                    LastModifiedUtc: Directory.GetLastWriteTimeUtc(entryPath)));
            }
            else
            {
                var fileInfo = new FileInfo(entryPath);
                matches.Add(new FileNodeDto(
                    Path: StoragePathResolver.ToRelativePath(rootPath!, entryPath),
                    Name: name,
                    IsFolder: false,
                    SizeBytes: fileInfo.Length,
                    LastModifiedUtc: fileInfo.LastWriteTimeUtc));
            }

            if (matches.Count >= maxResults)
            {
                break;
            }
        }

        return Ok(new FileSearchResponseDto(query, matches));
    }

    [HttpPost("folders")]
    public ActionResult<FileNodeDto> CreateFolder([FromBody] CreateFolderRequestDto request)
    {
        if (!FilePermissionService.HasPermission(User, PermissionKeys.NewFolder))
        {
            return Forbid();
        }

        var folderName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(folderName) || folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return BadRequest("Invalid folder name.");
        }

        if (!TryGetStorageRoot(out var rootPath, out var unavailableResult))
        {
            return unavailableResult!;
        }

        if (!StoragePathResolver.TryResolveOptionalPathUnderRoot(rootPath!, request.CurrentPath, out var currentFolderPath) ||
            string.IsNullOrWhiteSpace(currentFolderPath) ||
            !Directory.Exists(currentFolderPath))
        {
            return BadRequest("Invalid current path.");
        }

        var targetPath = Path.Combine(currentFolderPath, folderName);
        if (Directory.Exists(targetPath) || System.IO.File.Exists(targetPath))
        {
            return Conflict(new ConflictResponseDto("already_exists", "An item with the same name already exists.", request.CurrentPath, null));
        }

        Directory.CreateDirectory(targetPath);

        var createdFolderPath = StoragePathResolver.ToRelativePath(rootPath!, targetPath);
        PublishEvent(new FileChangeEventDto(
            SchemaVersion: "1",
            EventId: Guid.NewGuid().ToString("N"),
            EventType: FileEventTypes.Created,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            ScopePath: StoragePathResolver.ToRelativePath(rootPath!, currentFolderPath),
            CorrelationId: GetCorrelationId(),
            BatchId: null,
            FromPath: null,
            ToPath: createdFolderPath,
            Item: new FileChangeItemDto(
                Path: createdFolderPath,
                Name: folderName,
                IsFolder: true,
                SizeBytes: null,
                LastModifiedUtc: Directory.GetLastWriteTimeUtc(targetPath))));

        return Ok(new FileNodeDto(
            Path: createdFolderPath,
            Name: folderName,
            IsFolder: true,
            SizeBytes: null,
            LastModifiedUtc: Directory.GetLastWriteTimeUtc(targetPath)));
    }

    [HttpPost("rename")]
    public ActionResult<FileNodeDto> Rename([FromBody] RenameItemRequestDto request)
    {
        if (!FilePermissionService.HasPermission(User, PermissionKeys.Rename))
        {
            return Forbid();
        }

        var newName = request.NewName?.Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return BadRequest("Invalid new item name.");
        }

        if (!TryGetStorageRoot(out var rootPath, out var unavailableResult))
        {
            return unavailableResult!;
        }

        if (!StoragePathResolver.TryResolvePathUnderRoot(rootPath!, request.Path, out var fullSourcePath) ||
            string.IsNullOrWhiteSpace(fullSourcePath))
        {
            return BadRequest("Invalid item path.");
        }

        var sourceExistsAsDirectory = Directory.Exists(fullSourcePath);
        var sourceExistsAsFile = System.IO.File.Exists(fullSourcePath);
        if (!sourceExistsAsDirectory && !sourceExistsAsFile)
        {
            return NotFound();
        }

        var parentDirectory = Path.GetDirectoryName(fullSourcePath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return BadRequest("Cannot rename root.");
        }

        var fullTargetPath = Path.Combine(parentDirectory, newName);
        if (Directory.Exists(fullTargetPath) || System.IO.File.Exists(fullTargetPath))
        {
            return Conflict(new ConflictResponseDto("already_exists", "An item with the same name already exists.", request.Path, null));
        }

        if (sourceExistsAsDirectory)
        {
            Directory.Move(fullSourcePath, fullTargetPath);
            var renamedPath = StoragePathResolver.ToRelativePath(rootPath!, fullTargetPath);
            PublishEvent(new FileChangeEventDto(
                SchemaVersion: "1",
                EventId: Guid.NewGuid().ToString("N"),
                EventType: FileEventTypes.Renamed,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                ScopePath: StoragePathResolver.ToRelativePath(rootPath!, parentDirectory),
                CorrelationId: GetCorrelationId(),
                BatchId: null,
                FromPath: request.Path,
                ToPath: renamedPath,
                Item: new FileChangeItemDto(
                    Path: renamedPath,
                    Name: newName,
                    IsFolder: true,
                    SizeBytes: null,
                    LastModifiedUtc: Directory.GetLastWriteTimeUtc(fullTargetPath))));

            return Ok(new FileNodeDto(
                Path: renamedPath,
                Name: newName,
                IsFolder: true,
                SizeBytes: null,
                LastModifiedUtc: Directory.GetLastWriteTimeUtc(fullTargetPath)));
        }

        System.IO.File.Move(fullSourcePath, fullTargetPath);
        var fileInfo = new FileInfo(fullTargetPath);
        var renamedFilePath = StoragePathResolver.ToRelativePath(rootPath!, fullTargetPath);
        PublishEvent(new FileChangeEventDto(
            SchemaVersion: "1",
            EventId: Guid.NewGuid().ToString("N"),
            EventType: FileEventTypes.Renamed,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            ScopePath: StoragePathResolver.ToRelativePath(rootPath!, parentDirectory),
            CorrelationId: GetCorrelationId(),
            BatchId: null,
            FromPath: request.Path,
            ToPath: renamedFilePath,
            Item: new FileChangeItemDto(
                Path: renamedFilePath,
                Name: fileInfo.Name,
                IsFolder: false,
                SizeBytes: fileInfo.Length,
                LastModifiedUtc: fileInfo.LastWriteTimeUtc)));

        return Ok(new FileNodeDto(
            Path: renamedFilePath,
            Name: fileInfo.Name,
            IsFolder: false,
            SizeBytes: fileInfo.Length,
            LastModifiedUtc: fileInfo.LastWriteTimeUtc));
    }

    [HttpPost("move")]
    public ActionResult<IReadOnlyList<FileNodeDto>> Move([FromBody] MoveItemsRequestDto request)
    {
        if (!FilePermissionService.HasPermission(User, PermissionKeys.Move))
        {
            return Forbid();
        }

        if (request.Paths is null || request.Paths.Count == 0)
        {
            return BadRequest("At least one source path is required.");
        }

        if (!TryGetStorageRoot(out var rootPath, out var unavailableResult))
        {
            return unavailableResult!;
        }

        if (!StoragePathResolver.TryResolvePathUnderRoot(rootPath!, request.DestinationPath, out var fullDestinationPath) ||
            string.IsNullOrWhiteSpace(fullDestinationPath) ||
            !Directory.Exists(fullDestinationPath))
        {
            return BadRequest("Invalid destination path.");
        }

        var movedItems = new List<FileNodeDto>(request.Paths.Count);
        foreach (var sourcePath in request.Paths)
        {
            if (!StoragePathResolver.TryResolvePathUnderRoot(rootPath!, sourcePath, out var fullSourcePath) ||
                string.IsNullOrWhiteSpace(fullSourcePath))
            {
                return BadRequest($"Invalid source path: '{sourcePath}'.");
            }

            var sourceExistsAsDirectory = Directory.Exists(fullSourcePath);
            var sourceExistsAsFile = System.IO.File.Exists(fullSourcePath);
            if (!sourceExistsAsDirectory && !sourceExistsAsFile)
            {
                return NotFound($"Source item not found: '{sourcePath}'.");
            }

            var itemName = Path.GetFileName(fullSourcePath);
            var fullTargetPath = Path.Combine(fullDestinationPath, itemName);
            if (Directory.Exists(fullTargetPath) || System.IO.File.Exists(fullTargetPath))
            {
                return Conflict(new ConflictResponseDto("already_exists", "A destination item with the same name already exists.", sourcePath, null));
            }

            if (sourceExistsAsDirectory)
            {
                if (IsNestedPath(fullSourcePath, fullDestinationPath))
                {
                    return BadRequest("Cannot move a folder into itself or one of its descendants.");
                }

                Directory.Move(fullSourcePath, fullTargetPath);
                var movedFolderPath = StoragePathResolver.ToRelativePath(rootPath!, fullTargetPath);
                PublishEvent(new FileChangeEventDto(
                    SchemaVersion: "1",
                    EventId: Guid.NewGuid().ToString("N"),
                    EventType: FileEventTypes.Moved,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    ScopePath: StoragePathResolver.ToRelativePath(rootPath!, fullDestinationPath),
                    CorrelationId: GetCorrelationId(),
                    BatchId: null,
                    FromPath: sourcePath,
                    ToPath: movedFolderPath,
                    Item: new FileChangeItemDto(
                        Path: movedFolderPath,
                        Name: itemName,
                        IsFolder: true,
                        SizeBytes: null,
                        LastModifiedUtc: Directory.GetLastWriteTimeUtc(fullTargetPath))));

                movedItems.Add(new FileNodeDto(
                    Path: movedFolderPath,
                    Name: itemName,
                    IsFolder: true,
                    SizeBytes: null,
                    LastModifiedUtc: Directory.GetLastWriteTimeUtc(fullTargetPath)));
            }
            else
            {
                System.IO.File.Move(fullSourcePath, fullTargetPath);
                var movedFileInfo = new FileInfo(fullTargetPath);
                var movedFilePath = StoragePathResolver.ToRelativePath(rootPath!, fullTargetPath);
                PublishEvent(new FileChangeEventDto(
                    SchemaVersion: "1",
                    EventId: Guid.NewGuid().ToString("N"),
                    EventType: FileEventTypes.Moved,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    ScopePath: StoragePathResolver.ToRelativePath(rootPath!, fullDestinationPath),
                    CorrelationId: GetCorrelationId(),
                    BatchId: null,
                    FromPath: sourcePath,
                    ToPath: movedFilePath,
                    Item: new FileChangeItemDto(
                        Path: movedFilePath,
                        Name: movedFileInfo.Name,
                        IsFolder: false,
                        SizeBytes: movedFileInfo.Length,
                        LastModifiedUtc: movedFileInfo.LastWriteTimeUtc)));

                movedItems.Add(new FileNodeDto(
                    Path: movedFilePath,
                    Name: movedFileInfo.Name,
                    IsFolder: false,
                    SizeBytes: movedFileInfo.Length,
                    LastModifiedUtc: movedFileInfo.LastWriteTimeUtc));
            }
        }

        return Ok(movedItems);
    }

    [HttpPost("delete")]
    public ActionResult Delete([FromBody] DeleteItemsRequestDto request)
    {
        if (!FilePermissionService.HasPermission(User, PermissionKeys.Delete))
        {
            return Forbid();
        }

        if (request.Paths is null || request.Paths.Count == 0)
        {
            return BadRequest("At least one path is required.");
        }

        if (!TryGetStorageRoot(out var rootPath, out var unavailableResult))
        {
            return unavailableResult!;
        }

        foreach (var sourcePath in request.Paths)
        {
            if (!StoragePathResolver.TryResolvePathUnderRoot(rootPath!, sourcePath, out var fullSourcePath) ||
                string.IsNullOrWhiteSpace(fullSourcePath))
            {
                return BadRequest($"Invalid path: '{sourcePath}'.");
            }

            var normalizedPath = StoragePathResolver.ToRelativePath(rootPath!, fullSourcePath);
            if (Directory.Exists(fullSourcePath))
            {
                PublishEvent(new FileChangeEventDto(
                    SchemaVersion: "1",
                    EventId: Guid.NewGuid().ToString("N"),
                    EventType: FileEventTypes.Deleted,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    ScopePath: FileEventsGroupName.ParentOfPath(normalizedPath) ?? string.Empty,
                    CorrelationId: GetCorrelationId(),
                    BatchId: null,
                    FromPath: normalizedPath,
                    ToPath: null,
                    Item: new FileChangeItemDto(
                        Path: normalizedPath,
                        Name: Path.GetFileName(fullSourcePath),
                        IsFolder: true,
                        SizeBytes: null,
                        LastModifiedUtc: null)));

                Directory.Delete(fullSourcePath, recursive: true);
                continue;
            }

            if (System.IO.File.Exists(fullSourcePath))
            {
                PublishEvent(new FileChangeEventDto(
                    SchemaVersion: "1",
                    EventId: Guid.NewGuid().ToString("N"),
                    EventType: FileEventTypes.Deleted,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    ScopePath: FileEventsGroupName.ParentOfPath(normalizedPath) ?? string.Empty,
                    CorrelationId: GetCorrelationId(),
                    BatchId: null,
                    FromPath: normalizedPath,
                    ToPath: null,
                    Item: new FileChangeItemDto(
                        Path: normalizedPath,
                        Name: Path.GetFileName(fullSourcePath),
                        IsFolder: false,
                        SizeBytes: null,
                        LastModifiedUtc: null)));

                System.IO.File.Delete(fullSourcePath);
                continue;
            }

            return NotFound($"Item not found: '{sourcePath}'.");
        }

        return NoContent();
    }

    [HttpGet("storage-info")]
    public ActionResult<StorageInfoDto> GetStorageInfo()
    {
        if (!FilePermissionService.HasPermission(User, PermissionKeys.Read))
        {
            return Forbid();
        }

        var configuredStoragePath = settingsStore.GetStorageRootPath();
        if (string.IsNullOrWhiteSpace(configuredStoragePath))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Storage root path is not configured. Run local setup on Machine A.");
        }

        var rootPath = StoragePathResolver.EnsureStorageRoot(configuredStoragePath);
        var drive = new DriveInfo(Path.GetPathRoot(rootPath)!);

        return Ok(new StorageInfoDto(drive.AvailableFreeSpace, drive.TotalSize));
    }

    /// <summary>Streams the raw request body to disk so multi-gigabyte uploads are never buffered in memory or a temp file.</summary>
    [HttpPost("upload/stream")]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<UploadResultDto>> UploadStream(
        [FromQuery] string fileName,
        [FromQuery] string? currentPath,
        CancellationToken cancellationToken)
    {
        if (!FilePermissionService.HasPermission(User, PermissionKeys.Upload))
        {
            return Forbid();
        }

        var originalFileName = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return BadRequest("Invalid file name.");
        }

        var configuredStoragePath = settingsStore.GetStorageRootPath();
        if (string.IsNullOrWhiteSpace(configuredStoragePath))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Storage root path is not configured. Run local setup on Machine A.");
        }

        var rootPath = StoragePathResolver.EnsureStorageRoot(configuredStoragePath);

        if (!StoragePathResolver.TryResolveOptionalPathUnderRoot(rootPath, currentPath, out var fullUploadFolderPath) ||
            string.IsNullOrWhiteSpace(fullUploadFolderPath) ||
            !Directory.Exists(fullUploadFolderPath))
        {
            return BadRequest("Invalid current folder path for upload.");
        }

        var targetPath = StoragePathResolver.GetUniquePath(fullUploadFolderPath, originalFileName);

        try
        {
            await using (var destination = new FileStream(
                targetPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 1024,
                useAsync: true))
            {
                await Request.Body.CopyToAsync(destination, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            TryDeletePartialUpload(targetPath);
            LogUploadFailure(originalFileName, Request.ContentLength ?? 0, exception);
            throw;
        }

        var writtenFile = new FileInfo(targetPath);
        var relativePath = StoragePathResolver.ToRelativePath(rootPath, targetPath);
        PublishEvent(new FileChangeEventDto(
            SchemaVersion: "1",
            EventId: Guid.NewGuid().ToString("N"),
            EventType: FileEventTypes.Created,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            ScopePath: StoragePathResolver.ToRelativePath(rootPath, fullUploadFolderPath),
            CorrelationId: GetCorrelationId(),
            BatchId: null,
            FromPath: null,
            ToPath: relativePath,
            Item: new FileChangeItemDto(
                Path: relativePath,
                Name: writtenFile.Name,
                IsFolder: false,
                SizeBytes: writtenFile.Length,
                LastModifiedUtc: writtenFile.LastWriteTimeUtc)));

        return Ok(new UploadResultDto(relativePath, writtenFile.Length, writtenFile.LastWriteTimeUtc));
    }

    [HttpGet("download/{**relativePath}")]
    public IActionResult Download(string relativePath)
    {
        if (!FilePermissionService.HasPermission(User, PermissionKeys.Download))
        {
            return Forbid();
        }

        var configuredStoragePath = settingsStore.GetStorageRootPath();
        if (string.IsNullOrWhiteSpace(configuredStoragePath))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Storage root path is not configured. Run local setup on Machine A.");
        }

        var rootPath = StoragePathResolver.EnsureStorageRoot(configuredStoragePath);
        var resolved = StoragePathResolver.TryResolvePathUnderRoot(rootPath, relativePath, out var fullPath);

        if (!resolved || fullPath is null)
        {
            return BadRequest("Invalid file path.");
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        return PhysicalFile(fullPath, "application/octet-stream", Path.GetFileName(fullPath), enableRangeProcessing: true);
    }

    private bool TryGetStorageRoot(out string? rootPath, out ActionResult? unavailableResult)
    {
        rootPath = null;
        unavailableResult = null;

        var configuredStoragePath = settingsStore.GetStorageRootPath();
        if (string.IsNullOrWhiteSpace(configuredStoragePath))
        {
            unavailableResult = StatusCode(StatusCodes.Status503ServiceUnavailable, "Storage root path is not configured. Run local setup on Machine A.");
            return false;
        }

        rootPath = StoragePathResolver.EnsureStorageRoot(configuredStoragePath);
        return true;
    }

    private static bool IsNestedPath(string sourceDirectoryPath, string destinationDirectoryPath)
    {
        var sourceFullPath = Path.GetFullPath(sourceDirectoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var destinationFullPath = Path.GetFullPath(destinationDirectoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return destinationFullPath.StartsWith(sourceFullPath, StringComparison.OrdinalIgnoreCase);
    }

    private string? GetCorrelationId()
    {
        if (Request.Headers.TryGetValue("X-Correlation-ID", out var explicitValue))
        {
            var value = explicitValue.ToString().Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private void PublishEvent(FileChangeEventDto fileEvent)
    {
        _ = fileEventPublisher.PublishAsync(fileEvent);
    }

    private static void TryDeletePartialUpload(string targetPath)
    {
        try
        {
            if (System.IO.File.Exists(targetPath))
            {
                System.IO.File.Delete(targetPath);
            }
        }
        catch
        {
            // A partial file that cannot be removed must not mask the original upload failure.
        }
    }

    private void LogUploadFailure(string fileName, long sizeBytes, Exception exception)
    {
        applicationLogStore.Write(new ApplicationLogRecord(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            ApplicationLogSeverity.Error,
            ApplicationLogCategory.App,
            "FilesController",
            GetCorrelationId(),
            User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value,
            User.FindFirst("device_name")?.Value,
            $"Upload failed for '{fileName}' ({sizeBytes} bytes).",
            exception.GetType().Name,
            exception.Message,
            null,
            false));
    }
}