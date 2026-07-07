using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TWR.MyFamilyAuth.API.Controllers;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;
using Xunit;

namespace TWR.MyFamilyAuth.Tests.ServiceController;

/// <summary>
/// Tests for the service-to-service GET /api/service/users/{id}/grantees endpoint, used by
/// MyMessages to compute Urgent-message fan-out from the live BuddyGrant list.
/// </summary>
public class ServiceControllerGranteesTests
{
    private static readonly Guid TimId   = Guid.NewGuid();
    private static readonly Guid DianeId = Guid.NewGuid();
    private static readonly Guid SarahId = Guid.NewGuid();

    private const string ServiceApiKey = "test-service-key";

    private static API.Controllers.ServiceController BuildController(Mock<IDataAccess> data, string? headerKey = ServiceApiKey)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["ServiceApiKey"]).Returns(ServiceApiKey);

        var controller = new API.Controllers.ServiceController(data.Object, config.Object, NullLogger<API.Controllers.ServiceController>.Instance);
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        if (headerKey is not null)
            httpContext.Request.Headers["X-Service-Key"] = headerKey;

        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = httpContext
        };
        return controller;
    }

    [Fact]
    public async Task GetGrantees_ReturnsGranteeIds_WhoHoldTheRequestedPermission()
    {
        var data = new Mock<IDataAccess>();
        data.Setup(d => d.GetGrantsGivenAsync(TimId)).ReturnsAsync(
        [
            new BuddyGrant { GrantorId = TimId, GranteeId = DianeId, Permissions = ["Medical", "Info"], IsActive = true },
            new BuddyGrant { GrantorId = TimId, GranteeId = SarahId, Permissions = ["Info"], IsActive = true }
        ]);

        var controller = BuildController(data);
        var result = await controller.GetGrantees(TimId, "Medical") as Microsoft.AspNetCore.Mvc.OkObjectResult;

        Assert.NotNull(result);
        var body = result!.Value;
        var granteeIds = (Guid[])body!.GetType().GetProperty("GranteeIds")!.GetValue(body)!;
        Assert.Single(granteeIds);
        Assert.Contains(DianeId, granteeIds);
        Assert.DoesNotContain(SarahId, granteeIds);
    }

    [Fact]
    public async Task GetGrantees_ReturnsEmpty_WhenNoGrantsMatchPermission()
    {
        var data = new Mock<IDataAccess>();
        data.Setup(d => d.GetGrantsGivenAsync(TimId)).ReturnsAsync(
        [
            new BuddyGrant { GrantorId = TimId, GranteeId = SarahId, Permissions = ["Info"], IsActive = true }
        ]);

        var controller = BuildController(data);
        var result = await controller.GetGrantees(TimId, "Medical") as Microsoft.AspNetCore.Mvc.OkObjectResult;

        Assert.NotNull(result);
        var granteeIds = (Guid[])result!.Value!.GetType().GetProperty("GranteeIds")!.GetValue(result.Value)!;
        Assert.Empty(granteeIds);
    }

    [Fact]
    public async Task GetGrantees_ReturnsUnauthorized_WhenServiceKeyMissingOrWrong()
    {
        var data = new Mock<IDataAccess>();
        var controller = BuildController(data, headerKey: "wrong-key");

        var result = await controller.GetGrantees(TimId, "Medical");

        Assert.IsType<Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetGrantees_ReturnsBadRequest_WhenPermissionMissing()
    {
        var data = new Mock<IDataAccess>();
        var controller = BuildController(data);

        var result = await controller.GetGrantees(TimId, "");

        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
    }
}
