using System.Security.Claims;
using Ignyos.LanPortal.Contracts;

namespace Ignyos.LanPortal.Api.Services;

public static class FilePermissionService
{
    public static bool HasPermission(ClaimsPrincipal user, string permission)
    {
        if (user.IsInRole("Admin"))
        {
            return true;
        }

        return user
            .FindAll(PermissionClaimTypes.Permission)
            .Any(claim => string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase));
    }
}
