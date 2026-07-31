using Ignyos.LanPortal.Contracts;
using Ignyos.LanPortal.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ignyos.LanPortal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class FilesController(IAppSettingsStore settingsStore) : ControllerBase
{
    private static readonly EnumerationOptions StorageEnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true
    };

    [HttpGet]
    public ActionResult<IReadOnlyList<FileEntryDto>> ListFiles()
    {
        var configuredStoragePath = settingsStore.GetStorageRootPath();
        if (string.IsNullOrWhiteSpace(configuredStoragePath))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Storage root path is not configured. Run local setup on Machine A.");
        }

        var rootPath = EnsureStorageRoot(configuredStoragePath);

        var files = Directory.EnumerateFiles(rootPath, "*", StorageEnumerationOptions)
            .Select(path =>
            {
                var info = new FileInfo(path);
                var relativePath = Path.GetRelativePath(rootPath, path).Replace('\\', '/');
                return new FileEntryDto(relativePath, info.Length, info.LastWriteTimeUtc);
            })
            .OrderBy(entry => entry.RelativePath)
            .ToArray();

        return Ok(files);
    }

    [HttpPost("upload")]
    [RequestFormLimits(MultipartBodyLengthLimit = 10L * 1024L * 1024L * 1024L)]
    public async Task<ActionResult<UploadResultDto>> Upload([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        file ??= Request.Form.Files.FirstOrDefault();

        if (file is null || file.Length <= 0)
        {
            return BadRequest("No file was uploaded.");
        }

        var configuredStoragePath = settingsStore.GetStorageRootPath();
        if (string.IsNullOrWhiteSpace(configuredStoragePath))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Storage root path is not configured. Run local setup on Machine A.");
        }

        var rootPath = EnsureStorageRoot(configuredStoragePath);
        var originalFileName = Path.GetFileName(file.FileName);

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return BadRequest("Invalid file name.");
        }

        var targetPath = GetUniquePath(rootPath, originalFileName);

        await using (var destination = System.IO.File.Create(targetPath))
        {
            await file.CopyToAsync(destination, cancellationToken);
        }

        var writtenFile = new FileInfo(targetPath);
        var relativePath = Path.GetRelativePath(rootPath, targetPath).Replace('\\', '/');
        return Ok(new UploadResultDto(relativePath, writtenFile.Length, writtenFile.LastWriteTimeUtc));
    }

    [HttpGet("download/{**relativePath}")]
    public IActionResult Download(string relativePath)
    {
        var configuredStoragePath = settingsStore.GetStorageRootPath();
        if (string.IsNullOrWhiteSpace(configuredStoragePath))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Storage root path is not configured. Run local setup on Machine A.");
        }

        var rootPath = EnsureStorageRoot(configuredStoragePath);
        var fullPath = ResolveSafePath(rootPath, relativePath);

        if (fullPath is null)
        {
            return BadRequest("Invalid file path.");
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        return PhysicalFile(fullPath, "application/octet-stream", Path.GetFileName(fullPath), enableRangeProcessing: true);
    }

    private static string EnsureStorageRoot(string? configuredRootPath)
    {
        var rootPath = string.IsNullOrWhiteSpace(configuredRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "storage")
            : configuredRootPath;

        rootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }

    private static string GetUniquePath(string rootPath, string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = Path.Combine(rootPath, fileName);
        var counter = 1;

        while (System.IO.File.Exists(candidate))
        {
            candidate = Path.Combine(rootPath, $"{baseName}-{counter}{extension}");
            counter++;
        }

        return candidate;
    }

    private static string? ResolveSafePath(string rootPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var combined = Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var fullPath = Path.GetFullPath(combined);
        var relativeFromRoot = Path.GetRelativePath(rootPath, fullPath);

        if (relativeFromRoot.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativeFromRoot))
        {
            return null;
        }

        return fullPath;
    }
}