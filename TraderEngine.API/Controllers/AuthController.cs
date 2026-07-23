using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TraderEngine.API.AppSettings;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Data.Entities;

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
  private readonly JwtSettings _jwtSettings;

  public AuthController(
    ILogger<AuthController> logger,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    IOptions<JwtSettings> jwtOptions)
  {
    _logger = logger;
    _userManager = userManager;
    _signInManager = signInManager;
    _jwtSettings = jwtOptions.Value;
  }

  [AllowAnonymous]
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

    var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);

    var claims = new List<Claim>
    {
      new(ClaimTypes.NameIdentifier, user.Id.ToString()),
      new(ClaimTypes.Name, user.UserName ?? user.Id.ToString()),
    };

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SigningKey));
    var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
      issuer: _jwtSettings.Issuer,
      audience: _jwtSettings.Audience,
      claims: claims,
      expires: expiresAt.UtcDateTime,
      signingCredentials: credentials);

    return Ok(new LoginResponseDto
    {
      Token = new JwtSecurityTokenHandler().WriteToken(token),
      ExpiresAt = expiresAt,
    });
  }
}
