using System.IdentityModel.Tokens.Jwt;
using Ignyos.LanPortal.Api.Services;
using Ignyos.LanPortal.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ignyos.LanPortal.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController(IAppSettingsStore settingsStore, IDeviceLoginStore loginStore) : ControllerBase
{
    [HttpGet("whoami")]
    [AllowAnonymous]
    public IActionResult WhoAmI()
    {
        // With JwtSecurityTokenHandler.DefaultMapInboundClaims = false,
        // claims should be preserved in their original form ("role" not URI).
        var roles = User.FindAll("role").Select(c => c.Value).ToList();

        var userName = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value;
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

        return Ok(new
        {
            userName = userName ?? sub ?? "unknown",
            roles,
            jti,
            isAuthenticated,
            allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("approvals/pending")]
    public ActionResult<IReadOnlyList<PendingLoginRequestDto>> PendingApprovals()
    {
        return Ok(loginStore.GetPendingRequests());
    }

    [HttpPost("approvals/{requestId:guid}/approve")]
    public IActionResult ApproveLogin(Guid requestId, [FromBody] ApproveLoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return BadRequest("UserName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Roles))
        {
            return BadRequest("Roles is required.");
        }

        const int maxTokenMinutes = 87600 * 60;
        if (request.TokenMinutes is < 5 or > maxTokenMinutes)
        {
            return BadRequest($"TokenMinutes must be between 5 and {maxTokenMinutes}.");
        }

        var approved = loginStore.Approve(requestId, request.UserName.Trim(), request.Roles, request.TokenMinutes);
        return approved ? Ok() : NotFound();
    }

    [HttpPost("approvals/{requestId:guid}/deny")]
    public IActionResult DenyLogin(Guid requestId, [FromBody] DenyLoginRequestDto request)
    {
        var denied = loginStore.Deny(requestId, request.Reason);
        return denied ? Ok() : NotFound();
    }

    [HttpGet("sessions/active")]
    public ActionResult<IReadOnlyList<AccessSessionDto>> ActiveSessions()
    {
        var currentJti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        var sessions = settingsStore
            .GetActiveAccessSessions()
            .Select(session => new AccessSessionDto(
                SessionId: session.SessionId,
                UserName: session.UserName,
                DeviceName: session.DeviceName,
                Roles: session.Roles,
                IssuedAtUtc: session.IssuedAtUtc,
                ExpiresAtUtc: session.ExpiresAtUtc,
                LastSeenAtUtc: session.LastSeenAtUtc,
                IsCurrentSession: !string.IsNullOrWhiteSpace(currentJti) &&
                    string.Equals(session.Jti, currentJti, StringComparison.Ordinal)))
            .ToArray();

        return Ok(sessions);
    }

    [HttpPost("sessions/{sessionId:guid}/revoke")]
    public IActionResult RevokeSession(Guid sessionId, [FromBody] RevokeSessionRequestDto request)
    {
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? $"Revoked by admin user '{GetActorName()}'."
            : request.Reason.Trim();

        var revoked = settingsStore.RevokeAccessSession(sessionId, reason);
        return revoked ? Ok() : NotFound();
    }

    [HttpPost("sessions/revoke-by-filter")]
    public ActionResult<RevokeByFilterResponseDto> RevokeByFilter([FromBody] RevokeByFilterRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) && string.IsNullOrWhiteSpace(request.DeviceName))
        {
            return BadRequest("Provide UserName and/or DeviceName.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? $"Revoked by admin user '{GetActorName()}'."
            : request.Reason.Trim();

        var revokedCount = settingsStore.RevokeAccessByUserDevice(request.UserName, request.DeviceName, reason);
        return Ok(new RevokeByFilterResponseDto(revokedCount));
    }

    [HttpPost("sessions/{sessionId:guid}/roles")]
    public ActionResult<UpdateSessionRolesResponseDto> UpdateRoles(Guid sessionId, [FromBody] UpdateSessionRolesRequestDto request)
    {
        var normalizedRoles = NormalizeRoles(request.Roles);
        if (string.IsNullOrWhiteSpace(normalizedRoles))
        {
            return BadRequest("At least one role is required.");
        }

        var changedAtUtc = DateTimeOffset.UtcNow;
        var changed = settingsStore.UpdateAccessSessionRoles(
            sessionId,
            normalizedRoles,
            GetActorName(),
            string.IsNullOrWhiteSpace(request.Reason) ? "Roles updated by admin." : request.Reason.Trim(),
            changedAtUtc);

        if (!changed)
        {
            return NotFound();
        }

        return Ok(new UpdateSessionRolesResponseDto(sessionId, normalizedRoles, changedAtUtc));
    }

    private string GetActorName()
    {
        return User.Identity?.Name
            ?? User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? "unknown";
    }

    private static string NormalizeRoles(string roles)
    {
        var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "User", "Admin" };

        var values = roles
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(role => allowedRoles.Contains(role))
            .Select(role => role.Equals("admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "User")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? string.Empty : string.Join(',', values);
    }
}
