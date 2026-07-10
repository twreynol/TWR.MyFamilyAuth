using Moq;
using TWR.MyFamilyAuth.API.Controllers;
using TWR.MyFamilyAuth.Contracts.DTOs.Users;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TWR.MyFamilyAuth.Tests.Users;

public class UserSettingsTests
{
    private static readonly Guid TimId   = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid SarahId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid WardId  = new("00000000-0000-0000-0000-000000000004");

    private static UsersController BuildController(Guid callerId, string role, Mock<IDataAccess> data)
    {
        var controller = new UsersController(data.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, callerId.ToString()),
                    new Claim(ClaimTypes.Role, role)
                ], "Test"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task GetSettings_Succeeds_ForSelf()
    {
        var data = new Mock<IDataAccess>();
        data.Setup(d => d.GetUserSettingsAsync(TimId, "mymedical"))
            .ReturnsAsync([new UserSetting { FamilyUserId = TimId, AppClientId = null, SettingKey = "Timezone", SettingValue = "America/Denver" }]);

        var controller = BuildController(TimId, FamilyRoles.User, data);
        var result = await controller.GetSettings(TimId, "mymedical");

        var ok = Assert.IsType<OkObjectResult>(result);
        var settings = Assert.IsAssignableFrom<IEnumerable<UserSettingDto>>(ok.Value);
        Assert.Single(settings);
    }

    [Fact]
    public async Task GetSettings_Forbidden_ForUnrelatedUser()
    {
        var data = new Mock<IDataAccess>();
        data.Setup(d => d.GetUserByIdAsync(SarahId)).ReturnsAsync(new FamilyUser { Id = SarahId, GuardianId = null });

        var controller = BuildController(TimId, FamilyRoles.User, data);
        var result = await controller.GetSettings(SarahId, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetSettings_Succeeds_ForGuardianOfWard()
    {
        var data = new Mock<IDataAccess>();
        data.Setup(d => d.GetUserByIdAsync(WardId)).ReturnsAsync(new FamilyUser { Id = WardId, IsWard = true, GuardianId = TimId });
        data.Setup(d => d.GetUserSettingsAsync(WardId, null)).ReturnsAsync([]);

        var controller = BuildController(TimId, FamilyRoles.User, data);
        var result = await controller.GetSettings(WardId, null);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetSettings_Succeeds_ForSuperAdmin_OnAnyUser()
    {
        var data = new Mock<IDataAccess>();
        data.Setup(d => d.GetUserSettingsAsync(SarahId, null)).ReturnsAsync([]);

        var controller = BuildController(TimId, FamilyRoles.SuperAdmin, data);
        var result = await controller.GetSettings(SarahId, null);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateSettings_UpsertsAndReturnsOk_ForSelf()
    {
        var data = new Mock<IDataAccess>();
        var request = new UpdateUserSettingsRequest([new UserSettingDto(null, "Timezone", "America/Denver")]);

        var controller = BuildController(TimId, FamilyRoles.User, data);
        var result = await controller.UpdateSettings(TimId, request);

        Assert.IsType<OkResult>(result);
        data.Verify(d => d.UpsertUserSettingsAsync(TimId,
            It.Is<IEnumerable<(string? AppClientId, string SettingKey, string SettingValue)>>(
                s => s.Count() == 1 && s.First().SettingKey == "Timezone" && s.First().SettingValue == "America/Denver")),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSettings_Forbidden_ForUnrelatedUser()
    {
        var data = new Mock<IDataAccess>();
        data.Setup(d => d.GetUserByIdAsync(SarahId)).ReturnsAsync(new FamilyUser { Id = SarahId, GuardianId = null });

        var controller = BuildController(TimId, FamilyRoles.User, data);
        var result = await controller.UpdateSettings(SarahId, new UpdateUserSettingsRequest([]));

        Assert.IsType<ForbidResult>(result);
        data.Verify(d => d.UpsertUserSettingsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<(string?, string, string)>>()), Times.Never);
    }
}
