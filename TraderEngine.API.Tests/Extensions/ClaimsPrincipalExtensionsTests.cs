using System.Security.Claims;
using TraderEngine.API.Extensions;
using TraderEngine.Data.Constants;

namespace TraderEngine.API.Tests.Extensions;

/// <summary>
/// Covers <see cref="ClaimsPrincipalExtensions.GetEffectiveUserId"/> — the single call site every
/// per-user controller action (RebalanceController, AllocationsController, AccountController)
/// uses instead of reading <see cref="ClaimTypes.NameIdentifier"/> directly. By the time this runs
/// in production, TraderEngine.API's default authorization policy has already independently
/// verified an ActingAsClientId claim via DelegatedAccessHandler — this extension only needs to
/// resolve which id a now-authorized request should act on.
/// </summary>
[TestClass]
public class ClaimsPrincipalExtensionsTests
{
  private static ClaimsPrincipal NewPrincipal(params Claim[] claims) =>
    new(new ClaimsIdentity(claims, "TestAuthType"));

  [TestMethod]
  public void GetEffectiveUserId_NoActingAsClientIdClaim_ReturnsTheCallersOwnId()
  {
    // Arrange
    var userId = Guid.NewGuid();
    var principal = NewPrincipal(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

    // Act
    var effectiveUserId = principal.GetEffectiveUserId();

    // Assert
    Assert.AreEqual(userId, effectiveUserId);
  }

  /// <summary>
  /// The complementary invariant to JwtTokenService's own test: NameIdentifier stays the real
  /// caller, so it's this extension's job to prefer ActingAsClientId when acting delegated,
  /// otherwise every delegated request would silently operate on the manager's own account.
  /// </summary>
  [TestMethod]
  public void GetEffectiveUserId_ActingAsClientIdClaimPresent_ReturnsTheClientIdNotTheCallersId()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    var principal = NewPrincipal(
      new Claim(ClaimTypes.NameIdentifier, managerId.ToString()),
      new Claim(DelegationClaimTypes.ActingAsClientId, clientId.ToString()));

    // Act
    var effectiveUserId = principal.GetEffectiveUserId();

    // Assert
    Assert.AreEqual(clientId, effectiveUserId);
    Assert.AreNotEqual(managerId, effectiveUserId);
  }
}