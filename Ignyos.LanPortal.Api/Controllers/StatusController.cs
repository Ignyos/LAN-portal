using Microsoft.AspNetCore.Mvc;

namespace Ignyos.LanPortal.Api.Controllers;

[ApiController]
public sealed class StatusController : ControllerBase
{
    [HttpGet("/")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Root()
    {
        return Ok(new
        {
            name = "Ignyos LAN Portal API",
            status = "Running",
            localAdmin = "/local/admin",
            localSetup = "/local/setup",
            localApprovals = "/local/approvals",
            deviceLoginRequest = "/api/auth/device/request",
            deviceLoginPoll = "/api/auth/device/poll",
            files = "/api/files"
        });
    }

    [HttpGet("/api/public/portal-config")]
    public IActionResult PortalConfig()
    {
        return Ok(new
        {
            networkName = "My Home"
        });
    }
}
