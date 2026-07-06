using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using TWR.MyFamilyAuth.API.Controllers;
using TWR.MyFamilyAuth.Contracts.DTOs.Invitations;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;

namespace TWR.MyFamilyAuth.Tests.BuddyGrants;

public class InvitationTests
{
    private static readonly Guid TimId = new("00000000-0000-0000-0000-000000000003");

    private static InvitationsController BuildController(Guid callerId, Mock<IDataAccess> data)
    {
        var controller = new InvitationsController(data.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, callerId.ToString()),
                    new Claim(ClaimTypes.Role, FamilyRoles.SuperAdmin)
                ], "Test"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task CreateInvitation_Succeeds_AndReturnsToken()
    {
        var invitation = new Invitation
        {
            Id              = Guid.NewGuid(),
            InviteeEmail    = "jeff@reger.com",
            DisplayName     = "Reger Family",
            Token           = "abc123",
            InvitedByUserId = TimId,
            CreatedAt       = DateTime.UtcNow,
            ExpiresAt       = DateTime.UtcNow.AddDays(7),
            InvitedBy       = new FamilyUser { Id = TimId, FirstName = "Tim", LastName = "Reynolds", Email = "tim@test.com" }
        };

        var data = new Mock<IDataAccess>();
        data.Setup(d => d.CreateInvitationAsync(It.IsAny<Invitation>())).ReturnsAsync(invitation);

        var controller = BuildController(TimId, data);
        var result     = await controller.CreateInvitation(new CreateInvitationRequest("jeff@reger.com", "Reger Family"));

        var ok  = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<InvitationDto>(ok.Value);
        Assert.Equal("jeff@reger.com", dto.InviteeEmail);
        Assert.Equal("Reger Family",   dto.DisplayName);
        Assert.False(dto.IsAccepted);
    }

    [Fact]
    public async Task CreateInvitation_DoesNotCreateBuddyGrant()
    {
        // An invitation must NEVER automatically create a BuddyGrant.
        // The invitee must explicitly grant access after accepting.
        var invitation = new Invitation
        {
            Id              = Guid.NewGuid(),
            InviteeEmail    = "jeff@reger.com",
            Token           = "abc123",
            InvitedByUserId = TimId,
            CreatedAt       = DateTime.UtcNow,
            ExpiresAt       = DateTime.UtcNow.AddDays(7),
            InvitedBy       = new FamilyUser { Id = TimId, FirstName = "Tim", LastName = "Reynolds", Email = "tim@test.com" }
        };

        var data = new Mock<IDataAccess>();
        data.Setup(d => d.CreateInvitationAsync(It.IsAny<Invitation>())).ReturnsAsync(invitation);

        var controller = BuildController(TimId, data);
        await controller.CreateInvitation(new CreateInvitationRequest("jeff@reger.com", null));

        // CreateBuddyGrantAsync must NEVER be called as a side effect of creating an invitation
        data.Verify(d => d.CreateBuddyGrantAsync(It.IsAny<BuddyGrant>()), Times.Never);
    }

    [Fact]
    public async Task GetByToken_ReturnsNotFound_WhenTokenDoesNotExist()
    {
        var data = new Mock<IDataAccess>();
        data.Setup(d => d.GetInvitationByTokenAsync("bad-token")).ReturnsAsync((Invitation?)null);

        var controller = BuildController(TimId, data);
        var result     = await controller.GetByToken("bad-token");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetByToken_ReturnsBadRequest_WhenExpired()
    {
        var invitation = new Invitation
        {
            Id              = Guid.NewGuid(),
            InviteeEmail    = "jeff@reger.com",
            Token           = "expired-token",
            InvitedByUserId = TimId,
            CreatedAt       = DateTime.UtcNow.AddDays(-10),
            ExpiresAt       = DateTime.UtcNow.AddDays(-3),  // expired
            InvitedBy       = new FamilyUser { Id = TimId, FirstName = "Tim", LastName = "Reynolds", Email = "tim@test.com" }
        };

        var data = new Mock<IDataAccess>();
        data.Setup(d => d.GetInvitationByTokenAsync("expired-token")).ReturnsAsync(invitation);

        var controller = BuildController(TimId, data);
        var result     = await controller.GetByToken("expired-token");

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
