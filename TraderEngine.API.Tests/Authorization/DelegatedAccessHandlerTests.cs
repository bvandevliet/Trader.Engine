using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using TraderEngine.API.Authorization;
using TraderEngine.Data.Constants;
using TraderEngine.Data.Services;

namespace TraderEngine.API.Tests.Authorization;

/// <summary>
/// Covers <see cref="DelegatedAccessHandler"/> — wired into the default authorization policy (see
/// TraderEngine.API's Program.cs) so it runs for every authenticated request, not just the
/// controllers that know about delegation. Every branch other than "no ActingAsClientId claim at
/// all" and "IDelegationAuthorizationService says yes" must leave the requirement unsatisfied,
/// including malformed claims a tampered or hand-crafted token could carry — this handler must
/// never throw on bad input, since an unhandled exception here would take down request processing
/// for every endpoint, not just fail one delegated check.
/// </summary>
[TestClass]
public class DelegatedAccessHandlerTests
{
  private static ClaimsPrincipal NewPrincipal(params Claim[] claims) =>
    new(new ClaimsIdentity(claims, "TestAuthType"));

  private static async Task<AuthorizationHandlerContext> RunHandler(IDelegationAuthorizationService delegationAuth, ClaimsPrincipal user)
  {
    var handler = new DelegatedAccessHandler(delegationAuth);
    var context = new AuthorizationHandlerContext([new DelegatedAccessRequirement()], user, resource: null);

    await handler.HandleAsync(context);

    return context;
  }

  [TestMethod]
  public async Task HandleRequirementAsync_NoActingAsClientIdClaim_Succeeds_WithoutConsultingTheAuthorizationService()
  {
    // Arrange — the ordinary case: a user acting on their own account.
    var delegationAuth = Substitute.For<IDelegationAuthorizationService>();
    var user = NewPrincipal(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

    // Act
    var context = await RunHandler(delegationAuth, user);

    // Assert
    Assert.IsTrue(context.HasSucceeded);
    await delegationAuth.DidNotReceive().IsAuthorizedAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
  }

  [TestMethod]
  public async Task HandleRequirementAsync_ActingAsClientIdPresentButNameIdentifierMissing_DoesNotSucceed()
  {
    // Arrange — malformed/hand-crafted token: carries a delegation claim with no caller identity.
    var delegationAuth = Substitute.For<IDelegationAuthorizationService>();
    var user = NewPrincipal(new Claim(DelegationClaimTypes.ActingAsClientId, Guid.NewGuid().ToString()));

    // Act
    var context = await RunHandler(delegationAuth, user);

    // Assert
    Assert.IsFalse(context.HasSucceeded);
  }

  [TestMethod]
  public async Task HandleRequirementAsync_ActingAsClientIdIsNotAGuid_DoesNotSucceed_AndDoesNotThrow()
  {
    // Arrange
    var delegationAuth = Substitute.For<IDelegationAuthorizationService>();
    var user = NewPrincipal(
      new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
      new Claim(DelegationClaimTypes.ActingAsClientId, "not-a-guid"));

    // Act
    var context = await RunHandler(delegationAuth, user);

    // Assert
    Assert.IsFalse(context.HasSucceeded);
  }

  [TestMethod]
  public async Task HandleRequirementAsync_NameIdentifierIsNotAGuid_DoesNotSucceed_AndDoesNotThrow()
  {
    // Arrange
    var delegationAuth = Substitute.For<IDelegationAuthorizationService>();
    var user = NewPrincipal(
      new Claim(ClaimTypes.NameIdentifier, "not-a-guid"),
      new Claim(DelegationClaimTypes.ActingAsClientId, Guid.NewGuid().ToString()));

    // Act
    var context = await RunHandler(delegationAuth, user);

    // Assert
    Assert.IsFalse(context.HasSucceeded);
  }

  [TestMethod]
  public async Task HandleRequirementAsync_AuthorizationServiceDenies_DoesNotSucceed()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    var delegationAuth = Substitute.For<IDelegationAuthorizationService>();
    delegationAuth.IsAuthorizedAsync(managerId, clientId).Returns(false);
    var user = NewPrincipal(
      new Claim(ClaimTypes.NameIdentifier, managerId.ToString()),
      new Claim(DelegationClaimTypes.ActingAsClientId, clientId.ToString()));

    // Act
    var context = await RunHandler(delegationAuth, user);

    // Assert
    Assert.IsFalse(context.HasSucceeded);
  }

  /// <summary>
  /// Confirms the claim-to-argument mapping isn't accidentally swapped: the caller
  /// (NameIdentifier) must be passed as the manager id, and the delegation target
  /// (ActingAsClientId) as the client id, not the reverse.
  /// </summary>
  [TestMethod]
  public async Task HandleRequirementAsync_AuthorizationServiceGrants_Succeeds_WithCorrectlyMappedArguments()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    var delegationAuth = Substitute.For<IDelegationAuthorizationService>();
    delegationAuth.IsAuthorizedAsync(managerId, clientId).Returns(true);
    var user = NewPrincipal(
      new Claim(ClaimTypes.NameIdentifier, managerId.ToString()),
      new Claim(DelegationClaimTypes.ActingAsClientId, clientId.ToString()));

    // Act
    var context = await RunHandler(delegationAuth, user);

    // Assert
    Assert.IsTrue(context.HasSucceeded);
    await delegationAuth.Received(1).IsAuthorizedAsync(managerId, clientId);
  }
}
