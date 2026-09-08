using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using TraderEngine.Data.AppSettings;
using TraderEngine.Data.Constants;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Services;

namespace TraderEngine.Data.Tests.Services;

/// <summary>
/// Covers the delegation-related addition to <see cref="JwtTokenService.GenerateToken"/>. The
/// property under test throughout: <see cref="ClaimTypes.NameIdentifier"/> must always be the real
/// caller's own id, delegated or not — this is the invariant
/// <c>ApiCredentialsController</c>'s isolation from delegated access depends on, since it only
/// ever reads that claim. <see cref="DelegationClaimTypes.ActingAsClientId"/> is the only claim
/// that should ever carry a target other than the caller.
/// </summary>
[TestClass]
public class JwtTokenServiceActingAsClaimTests
{
  private static JwtTokenService NewService() => new(Options.Create(new JwtSettings
  {
    Issuer = "TestIssuer",
    Audience = "TestAudience",
    SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!",
    ExpiryMinutes = 60,
  }));

  private static JsonWebToken Decode(string token) => new JsonWebTokenHandler().ReadJsonWebToken(token);

  [TestMethod]
  public void GenerateToken_NoActingAsClientId_OmitsTheClaimAndUsesCallerAsNameIdentifier()
  {
    // Arrange
    var user = new AppUser { Id = Guid.NewGuid(), UserName = "alice" };
    var service = NewService();

    // Act
    var (token, _) = service.GenerateToken(user);

    // Assert
    var jwt = Decode(token);
    Assert.AreEqual(user.Id.ToString(), jwt.GetClaim(ClaimTypes.NameIdentifier).Value);
    Assert.AreEqual("alice", jwt.GetClaim(ClaimTypes.Name).Value);
    Assert.IsFalse(jwt.Claims.Any(c => c.Type == DelegationClaimTypes.ActingAsClientId));
  }

  /// <summary>
  /// Same guard <see cref="Web.Services.DelegatedAccessResolver"/> and Dashboard/Config's callers
  /// rely on to avoid re-deriving "is this actually delegated" themselves before calling this
  /// method — passing your own id as the target must be indistinguishable from not passing one.
  /// </summary>
  [TestMethod]
  public void GenerateToken_ActingAsClientIdEqualsOwnId_OmitsTheClaim()
  {
    // Arrange
    var user = new AppUser { Id = Guid.NewGuid(), UserName = "alice" };
    var service = NewService();

    // Act
    var (token, _) = service.GenerateToken(user, user.Id);

    // Assert
    var jwt = Decode(token);
    Assert.IsFalse(jwt.Claims.Any(c => c.Type == DelegationClaimTypes.ActingAsClientId));
  }

  [TestMethod]
  public void GenerateToken_ActingAsADifferentClientId_KeepsNameIdentifierAsTheRealCaller_AndEmbedsTheTargetSeparately()
  {
    // Arrange
    var manager = new AppUser { Id = Guid.NewGuid(), UserName = "manager" };
    var clientId = Guid.NewGuid();
    var service = NewService();

    // Act
    var (token, _) = service.GenerateToken(manager, clientId);

    // Assert — the single most important invariant in the whole delegation feature: the JWT's
    // "who is this" claim never becomes the delegated target, only a separate, explicit claim does.
    var jwt = Decode(token);
    Assert.AreEqual(manager.Id.ToString(), jwt.GetClaim(ClaimTypes.NameIdentifier).Value);
    Assert.AreEqual(clientId.ToString(), jwt.GetClaim(DelegationClaimTypes.ActingAsClientId).Value);
  }

  [TestMethod]
  public void GenerateToken_UserNameIsNull_FallsBackToUserIdForTheNameClaim()
  {
    // Arrange
    var user = new AppUser { Id = Guid.NewGuid(), UserName = null };
    var service = NewService();

    // Act
    var (token, _) = service.GenerateToken(user);

    // Assert
    var jwt = Decode(token);
    Assert.AreEqual(user.Id.ToString(), jwt.GetClaim(ClaimTypes.Name).Value);
  }
}
