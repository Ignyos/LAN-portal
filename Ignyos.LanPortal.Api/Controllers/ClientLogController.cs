using Ignyos.LanPortal.Contracts;
using Ignyos.LanPortal.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Ignyos.LanPortal.Api.Controllers;

[ApiController]
[Route("api/client-logs")]
[Authorize]
public sealed class ClientLogController(IApplicationLogStore applicationLogStore) : ControllerBase
{
    private const int MaxMessageLength = 2000;
    private const int MaxFieldLength = 400;
    private const int MaxDetailsLength = 4000;

    [HttpPost]
    [RequestSizeLimit(32 * 1024)]
    public IActionResult Write([FromBody] ClientLogRequestDto request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest("Message is required.");
        }

        // Client-supplied severity is untrusted; anything unrecognized is recorded as an error.
        var severity = Enum.TryParse<ApplicationLogSeverity>(request.Severity, ignoreCase: true, out var parsedSeverity)
            && parsedSeverity is not ApplicationLogSeverity.Critical
                ? parsedSeverity
                : ApplicationLogSeverity.Error;

        applicationLogStore.Write(new ApplicationLogRecord(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            severity,
            ApplicationLogCategory.Client,
            Truncate(request.Source, MaxFieldLength) ?? "WebClient",
            Truncate(request.CorrelationId, MaxFieldLength),
            User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            User.FindFirst("device_name")?.Value,
            Truncate(request.Message, MaxMessageLength)!,
            Truncate(request.ExceptionType, MaxFieldLength),
            Truncate(request.ExceptionMessage, MaxMessageLength),
            Truncate(request.DetailsJson, MaxDetailsLength),
            false));

        return Accepted();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
