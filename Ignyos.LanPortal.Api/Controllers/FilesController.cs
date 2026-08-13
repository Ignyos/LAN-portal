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

        var rootPath = StoragePathResolver.EnsureStorageRoot(configuredStoragePath);
        var originalFileName = Path.GetFileName(file.FileName);

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return BadRequest("Invalid file name.");
        }

        var targetPath = StoragePathResolver.GetUniquePath(rootPath, originalFileName);

        await using (var destination = System.IO.File.Create(targetPath))
        {
            await file.CopyToAsync(destination, cancellationToken);
        }

        var writtenFile = new FileInfo(targetPath);
        var relativePath = StoragePathResolver.ToRelativePath(rootPath, targetPath);
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
}