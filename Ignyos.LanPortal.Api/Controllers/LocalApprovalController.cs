using System.Net;
using Ignyos.LanPortal.Api.Services;
using Ignyos.LanPortal.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Ignyos.LanPortal.Api.Controllers;

[ApiController]
public sealed class LocalApprovalController(IDeviceLoginStore loginStore) : ControllerBase
{
    [HttpGet("local/approvals")]
    public IActionResult ApprovalPage()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        return Redirect("/local/admin");
    }

    [HttpGet("api/local/approvals/debug")]
    public ActionResult<object> Debug()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        var pending = loginStore.GetPendingRequests();
        var recent = loginStore.GetRecentDecisions();

        return Ok(new
        {
            pendingRequests = pending.Select(r => new
            {
                r.RequestId,
                r.RequestedUserName,
                r.DeviceName,
                r.CreatedAtUtc,
                r.ExpiresAtUtc
            }),
            recentDecisions = recent.Select(d => new
            {
                d.RequestId,
                d.DeviceName,
                d.Decision,
                d.UserName,
                d.Roles,
                d.Reason,
                d.DecidedAtUtc
            })
        });
    }

    [HttpGet("api/local/approvals/pending")]
    public ActionResult<IReadOnlyList<PendingLoginRequestDto>> Pending()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        return Ok(loginStore.GetPendingRequests());
    }

    [HttpPost("api/local/approvals/{requestId:guid}/approve")]
    public IActionResult Approve(Guid requestId, [FromBody] ApproveLoginRequestDto request)
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

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

    [HttpPost("api/local/approvals/{requestId:guid}/deny")]
    public IActionResult Deny(Guid requestId, [FromBody] DenyLoginRequestDto request)
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        var denied = loginStore.Deny(requestId, request.Reason);
        return denied ? Ok() : NotFound();
    }

    [HttpGet("api/local/approvals/recent")]
    public ActionResult<IReadOnlyList<LoginDecisionDto>> Recent()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        return Ok(loginStore.GetRecentDecisions());
    }

    private static bool IsLocalRequest(HttpContext httpContext)
    {
        var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
        if (remoteIpAddress is null)
        {
            return false;
        }

        if (IPAddress.IsLoopback(remoteIpAddress))
        {
            return true;
        }

        if (remoteIpAddress.IsIPv4MappedToIPv6)
        {
            var mapped = remoteIpAddress.MapToIPv4();
            if (IPAddress.IsLoopback(mapped))
            {
                return true;
            }
        }

        var localIpAddress = httpContext.Connection.LocalIpAddress;
        return localIpAddress is not null && remoteIpAddress.Equals(localIpAddress);
    }
}
