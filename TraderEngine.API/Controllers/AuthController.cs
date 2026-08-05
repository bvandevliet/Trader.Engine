using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Services;

namespace TraderEngine.API.Controllers;

/// <summary>
/// Interim, API-only authentication: issues a JWT backed by the existing <see cref="AppUser"/>
/// Identity store, so endpoints handling per-user data (e.g. <see cref="ApiCredentialsController"/>)
/// can require <see cref="AuthorizeAttribute"/> and derive the caller's identity from the token
/// instead of trusting a client-supplied user id. A real login UI is still Phase 5's job — this
/// exists solely to close that authorization gap now.
/// </summary>
[ApiController, Route("api/[controller]")]
public class AuthController : ControllerBase
{
  private readonly ILogger<AuthController> _logger;
  private readonly UserManager<AppUser> _userManager;
  private readonly SignInManager<AppUser> _signInManager;
  private readonly IJwtTokenService _jwtTokenService;

  public AuthController(
    ILogger<AuthController> logger,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    IJwtTokenService jwtTokenService)
  {
    _logger = logger;
    _userManager = userManager;
    _signInManager = signInManager;
    _jwtTokenService = jwtTokenService;
  }

  [AllowAnonymous]
  [EnableRateLimiting("login")]
  [HttpPost("login")]
  public async Task<ActionResult<LoginResponseDto>> Login(LoginReqDto request)
  {
    _logger.LogTrace("Handling Login request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var user = await _userManager.FindByNameAsync(request.UserName);

    if (user == null)
      return Unauthorized();

    var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

    if (!result.Succeeded)
      return Unauthorized();

    // Credentials are valid but a forced password change is pending (set when an admin creates
    // or resets this account) — refuse to mint a token rather than silently letting this
    // interim, API-only login path bypass the same gate TraderEngine.Web enforces via
    // MustChangePasswordMiddleware. 403 rather than 401: the caller *is* who they say they are,
    // they're just not allowed a token yet.
    if (user.MustChangePassword)
      return StatusCode(StatusCodes.Status403Forbidden, "Password change required. Log in via the web application to set a new password.");

    var (token, expiresAt) = _jwtTokenService.GenerateToken(user);

    return Ok(new LoginResponseDto
    {
      Token = token,
      ExpiresAt = expiresAt,
    });
  }
}
