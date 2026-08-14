using System.Security.Claims;
using Ignyos.LanPortal.Api.Services;
using Ignyos.LanPortal.Contracts;
using Xunit;

namespace Ignyos.LanPortal.Api.Tests.Services;

public sealed class FilePermissionServiceTests
{
    [Fact]
    public void HasPermission_ReturnsTrue_ForAdminRole()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("role", "Admin")
        ],
        authenticationType: "test",
        nameType: ClaimTypes.Name,
        roleType: "role");

        var principal = new ClaimsPrincipal(identity);

        var allowed = FilePermissionService.HasPermission(principal, PermissionKeys.Delete);

        Assert.True(allowed);
    }

    [Fact]
    public void HasPermission_ReturnsTrue_WhenPermissionClaimExists()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(PermissionClaimTypes.Permission, PermissionKeys.Upload)
        ],
        authenticationType: "test");

        var principal = new ClaimsPrincipal(identity);

        var allowed = FilePermissionService.HasPermission(principal, PermissionKeys.Upload);

        Assert.True(allowed);
    }

    [Fact]
    public void HasPermission_ReturnsFalse_WhenPermissionMissing()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(PermissionClaimTypes.Permission, PermissionKeys.Read)
        ],
        authenticationType: "test");

        var principal = new ClaimsPrincipal(identity);

        var allowed = FilePermissionService.HasPermission(principal, PermissionKeys.Delete);

        Assert.False(allowed);
    }
}
