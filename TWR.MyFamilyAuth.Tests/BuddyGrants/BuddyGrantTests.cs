using Moq;
using TWR.MyFamilyAuth.API.Controllers;
using TWR.MyFamilyAuth.Contracts.DTOs.BuddyGrants;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace TWR.MyFamilyAuth.Tests.BuddyGrants;

public class BuddyGrantTests
{
    private static readonly Guid TimId       = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid SarahId     = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid ElizabethId = new("67bde9f5-f610-4718-8dc3-d1cfef232306");

    private static BuddyGrantsController BuildController(Guid callerId, Mock<IDataAccess> data)
    {
        var cache = new Mock<IMemoryCache>();
        var controller = new BuddyGrantsController(data.Object, cache.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, callerId.ToString()),
                    new Claim(ClaimTypes.Role, FamilyRoles.User)
                ], "Test"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task CreateGrant_Succeeds_WhenCallerIsGrantor()
    {
        var grant = new BuddyGrant
        {
            Id          = Guid.NewGuid(),
            GrantorId   = TimId,
            GranteeId   = SarahId,
            Permissions = ["Medical", "Info", "Messaging"],
            IsActive    = true,
            GrantedAt   = DateTime.UtcNow,
            Grantor     = new FamilyUser { Id = TimId,   FirstName = "Tim",   LastName = "Reynolds", Email = "tim@test.com" },
            Grantee     = new FamilyUser { Id = SarahId, FirstName = "Sarah", LastName = "Reynolds", Email = "sarah@test.com" }
        };

        var data = new Mock<IDataAccess>();
        data.Setup(d => d.GetGrantBetweenAsync(TimId, SarahId)).ReturnsAsync((BuddyGrant?)null);
        data.Setup(d => d.CreateBuddyGrantAsync(It.IsAny<BuddyGrant>())).ReturnsAsync(grant);
        data.Setup(d => d.GetGrantByIdAsync(grant.Id)).ReturnsAsync(grant);

        var controller = BuildController(TimId, data);
        var result = await controller.CreateGrant(TimId, new CreateBuddyGrantRequest(SarahId, ["Medical", "Info", "Messaging"]));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<BuddyGrantDto>(ok.Value);
        Assert.Equal(TimId, dto.GrantorId);
        Assert.Equal(SarahId, dto.GranteeId);
        Assert.Contains("Medical", dto.Permissions);
    }

    [Fact]
    public async Task CreateGrant_Forbidden_WhenCallerIsNotGrantor()
    {
        var data       = new Mock<IDataAccess>();
        var controller = BuildController(SarahId, data); // Sarah tries to create a grant as Tim

        var result = await controller.CreateGrant(TimId, new CreateBuddyGrantRequest(ElizabethId, ["Medical"]));

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CreateGrant_BadRequest_WhenGrantingToSelf()
    {
        var data       = new Mock<IDataAccess>();
        var controller = BuildController(TimId, data);

        var result = await controller.CreateGrant(TimId, new CreateBuddyGrantRequest(TimId, ["Medical"]));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateGrant_BadRequest_WhenPermissionInvalid()
    {
        var data       = new Mock<IDataAccess>();
        var controller = BuildController(TimId, data);

        var result = await controller.CreateGrant(TimId, new CreateBuddyGrantRequest(SarahId, ["InvalidPermission"]));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RevokeGrant_Succeeds_WhenCallerIsGrantor()
    {
        var grantId = Guid.NewGuid();
        var data    = new Mock<IDataAccess>();
        data.Setup(d => d.GetGrantByIdAsync(grantId))
            .ReturnsAsync(new BuddyGrant { Id = grantId, GrantorId = TimId, GranteeId = SarahId });
        data.Setup(d => d.RevokeBuddyGrantAsync(grantId, TimId)).ReturnsAsync(true);

        var controller = BuildController(TimId, data);
        var result     = await controller.RevokeGrant(TimId, grantId);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task RevokeGrant_Forbidden_WhenCallerIsNotGrantor()
    {
        var grantId    = Guid.NewGuid();
        var data       = new Mock<IDataAccess>();
        var controller = BuildController(SarahId, data); // Sarah tries to revoke Tim's grant

        var result = await controller.RevokeGrant(TimId, grantId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CheckPermission_GrantTimToSarah_DoesNotImplyGrantSarahToTim()
    {
        var data = new Mock<IDataAccess>();
        // Tim→Sarah Medical exists
        data.Setup(d => d.HasPermissionAsync(SarahId, TimId, "Medical")).ReturnsAsync(true);
        // Sarah→Tim Medical does NOT exist
        data.Setup(d => d.HasPermissionAsync(TimId, SarahId, "Medical")).ReturnsAsync(false);

        var controller = BuildController(TimId, data);

        var sarahCanReadTim = await controller.CheckPermission(TimId, SarahId, "Medical");
        var okSarah = Assert.IsType<OkObjectResult>(sarahCanReadTim);

        var timCanReadSarah = await controller.CheckPermission(SarahId, TimId, "Medical");
        var okTim = Assert.IsType<OkObjectResult>(timCanReadSarah);

        // Read the anonymous type values via reflection
        var sarahHas = (bool)okSarah.Value!.GetType().GetProperty("HasPermission")!.GetValue(okSarah.Value)!;
        var timHas   = (bool)okTim.Value!.GetType().GetProperty("HasPermission")!.GetValue(okTim.Value)!;

        Assert.True(sarahHas,  "Sarah should be able to read Tim's data (grant exists)");
        Assert.False(timHas,   "Tim should NOT be able to read Sarah's data (no grant from Sarah)");
    }
}
