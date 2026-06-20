using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TWR.MyFamilyAuth.API.AppServices;
using TWR.MyFamilyAuth.Contracts.DTOs.Auth;
using TWR.MyFamilyAuth.Contracts.Helpers;

namespace TWR.MyFamilyAuth.API.Controllers;

[ApiController]
[Route(ApiRoutes.Auth)]
public class AuthController : ControllerBase
{
    private readonly IAuthAppService _auth;
    public AuthController(IAuthAppService auth) => _auth = auth;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.LoginAsync(request, ip);
        return result is null ? Unauthorized("Invalid email or password.") : Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.RefreshAsync(request.RefreshToken, ip);
        return result is null ? Unauthorized("Refresh token invalid or expired.") : Ok(result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        await _auth.LogoutAsync(request.RefreshToken);
        return Ok();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _auth.ForgotPasswordAsync(request.Email);
        return Ok(new { message = "If that email exists, a reset code has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var ok = await _auth.ResetPasswordAsync(request);
        return ok ? Ok() : BadRequest("Invalid or expired reset code.");
    }

    [HttpPost("validate")]
    [AllowAnonymous]
    public async Task<IActionResult> Validate([FromBody] ValidateTokenRequest request)
    {
        var result = await _auth.ValidateTokenAsync(request.Token);
        return Ok(result);
    }

    [HttpPost("verify-2fa")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.VerifyTwoFactorAsync(request, ip);
        return result is null ? Unauthorized("Invalid or expired verification code.") : Ok(result);
    }
}
