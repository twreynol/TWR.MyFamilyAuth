using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TWR.MyFamilyAuth.API.AppServices;
using TWR.MyFamilyAuth.Contracts.DTOs.WebAuthn;
using TWR.MyFamilyAuth.Contracts.Helpers;

namespace TWR.MyFamilyAuth.API.Controllers;

[ApiController]
[Route(ApiRoutes.WebAuthn)]
public class WebAuthnController : ControllerBase
{
    private readonly IWebAuthnAppService _webAuthn;

    public WebAuthnController(IWebAuthnAppService webAuthn) => _webAuthn = webAuthn;

    private string? Origin => Request.Headers.Origin.FirstOrDefault();

    [HttpPost("register-options")]
    [Authorize]
    public async Task<IActionResult> RegisterOptions()
    {
        var userId      = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var appClientId = User.FindFirstValue("app_client_id");
        if (string.IsNullOrEmpty(appClientId)) return BadRequest("Token has no app_client_id claim.");

        var result = await _webAuthn.GetRegisterOptionsAsync(userId, appClientId, Origin);
        return result is null ? BadRequest("Unable to start registration for this app/origin.") : Ok(result);
    }

    [HttpPost("register-complete")]
    [Authorize]
    public async Task<IActionResult> RegisterComplete([FromBody] RegisterCompleteRequest request)
    {
        var userId      = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var appClientId = User.FindFirstValue("app_client_id");
        if (string.IsNullOrEmpty(appClientId)) return BadRequest("Token has no app_client_id claim.");

        var result = await _webAuthn.CompleteRegisterAsync(userId, appClientId, Origin, request);
        return result is null ? BadRequest("Registration verification failed.") : Ok(result);
    }

    [HttpPost("login-options")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginOptions([FromBody] WebAuthnLoginOptionsRequest request)
    {
        var result = await _webAuthn.GetLoginOptionsAsync(request, Origin);
        return result is null ? BadRequest("Unable to start passkey login for this app/origin.") : Ok(result);
    }

    [HttpPost("login-complete")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginComplete([FromBody] WebAuthnLoginCompleteRequest request)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _webAuthn.CompleteLoginAsync(request, Origin, ip);
        return result is null ? Unauthorized("Passkey verification failed.") : Ok(result);
    }

    [HttpGet("credentials")]
    [Authorize]
    public async Task<IActionResult> ListCredentials()
    {
        var userId      = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var appClientId = User.FindFirstValue("app_client_id");
        if (string.IsNullOrEmpty(appClientId)) return BadRequest("Token has no app_client_id claim.");

        var result = await _webAuthn.ListPasskeysAsync(userId, appClientId, Origin);
        return result is null ? BadRequest("Unable to list passkeys for this app/origin.") : Ok(result);
    }

    [HttpDelete("credentials/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteCredential(Guid id)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ok     = await _webAuthn.DeletePasskeyAsync(userId, id);
        return ok ? Ok() : NotFound();
    }
}
