using Microsoft.AspNetCore.Identity;
using NSubstitute;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Services;
using TraderEngine.Web.Services;

namespace TraderEngine.Web.Tests.Services;

/// <summary>
/// Covers <see cref="DelegatedAccessResolver"/> — TraderEngine.Web's pre-check, run before every
/// per-user page handler and before minting a delegated JWT. This is one half of the feature's
/// defense-in-depth pair; TraderEngine.API's <c>DelegatedAccessHandler</c> independently re-runs
/// the equivalent check server-side regardless of what this resolver decides.
/// </summary>
[TestClass]
public class DelegatedAccessResolverTests
{
  private static UserManager<AppUser> NewUserManagerSubstitute()
  {
    var store = Substitute.For<IUserStore<AppUser>>();

    return Substitute.For<UserManager<AppUser>>(store, null, null, null, null, null, null, null, null);
  }

  [TestMethod]
  public async Task ResolveAsync_NullActingAsClientId_ReturnsSelfContext_WithoutConsultingTheAuthorizationService()
  {
    // Arrange
    var caller = new AppUser { Id = Guid.NewGuid() };
    var delegationAuth = Substitute.For<IDelegationAuthorizationService>();
    var resolver = new DelegatedAccessResolver(delegationAuth, NewUserManagerSubstitute());

    // Act
    var ctx = await resolver.ResolveAsync(caller, null);

    // Assert
    Assert.IsFalse(ctx.IsDelegated);
    Assert.AreSame(caller, ctx.Caller);
    Assert.AreSame(caller, ctx.EffectiveUser);
    await delegationAuth.DidNotReceive().IsAuthorizedAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
  }

  [TestMethod]
  public async Task ResolveAsync_ActingAsClientIdEqualsOwnId_ReturnsSelfContext_WithoutConsultingTheAuthorizationService()
  {
    // Arrange — a user "delegating to themselves" must be indistinguishable from not delegating.
    var caller = new AppUser { Id = Guid.NewGuid() };
    var delegationAuth = Substitute.For<IDelegationAuthorizationService>();
    var resolver = new DelegatedAccessResolver(delegationAuth, NewUserManagerSubstitute());

    // Act
    var ctx = await resolver.ResolveAsync(caller, caller.Id);

    // Assert
    Assert.IsFalse(ctx.IsDelegated);
    await delegationAuth.DidNotReceive().IsAuthorizedAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
  }

  [TestMethod]
  public async Task ResolveAsync_NotAuthorized_ThrowsDelegationAccessDeniedException()
  {
    // Arrange
    var caller = new AppUser { Id = Guid.NewGuid() };
    var clientId = Guid.NewGuid();
    var delegationAuth = Substitute.For<IDelegationAuthorizationService>();
    delegationAuth.IsAuthorizedAsync(caller.Id, clientId).Returns(false);
    var resolver = new DelegatedAccessResolver(delegationAuth, NewUserManagerSubstitute());

    // Act + Assert
    await Assert.ThrowsExactlyAsync<DelegationAccessDeniedException>(() => resolver.ResolveAsync(caller, clientId));
  }

  /// <summary>
  /// Edge case: a grant is active but the client account was deleted between the grant check and
  /// the user lookup (or a stale/tampered id was supplied) — must fail safe, not throw an
  /// unhandled null-reference exception from deep inside a page handler.
  /// </summary>
  [TestMethod]
  public async Task ResolveAsync_AuthorizedButClientAccountDoesNotExist_ThrowsDelegationAccessDeniedException()
  {
    // Arrange
    var caller = new AppUser { Id = Guid.NewGuid() };
    var clientId = Guid.NewGuid();
    var delegationAuth = Substitute.For<IDelegationAuthorizationService>();
    delegationAuth.IsAuthorizedAsync(caller.Id, clientId).Returns(true);
    var userManager = NewUserManagerSubstitute();
    userManager.FindByIdAsync(clientId.ToString()).Returns((AppUser?)null);
    var resolver = new DelegatedAccessResolver(delegationAuth, userManager);

    // Act + Assert
    await Assert.ThrowsExactlyAsync<DelegationAccessDeniedException>(() => resolver.ResolveAsync(caller, clientId));
  }

  [TestMethod]
  public async Task ResolveAsync_AuthorizedAndClientExists_ReturnsDelegatedContext()
  {
    // Arrange
    var caller = new AppUser { Id = Guid.NewGuid() };
    var client = new AppUser { Id = Guid.NewGuid() };
    var delegationAuth = Substitute.For<IDelegationAuthorizationService>();
    delegationAuth.IsAuthorizedAsync(caller.Id, client.Id).Returns(true);
    var userManager = NewUserManagerSubstitute();
    userManager.FindByIdAsync(client.Id.ToString()).Returns(client);
    var resolver = new DelegatedAccessResolver(delegationAuth, userManager);

    // Act
    var ctx = await resolver.ResolveAsync(caller, client.Id);

    // Assert
    Assert.IsTrue(ctx.IsDelegated);
    Assert.AreSame(caller, ctx.Caller);
    Assert.AreSame(client, ctx.EffectiveUser);
  }
}
