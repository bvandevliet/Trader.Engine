using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using TraderEngine.Data.Constants;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Repositories;
using TraderEngine.Web.Pages;

namespace TraderEngine.Web.Tests.Pages;

/// <summary>
/// Covers the two highest-stakes handlers on <see cref="DelegationModel"/>:
/// <see cref="DelegationModel.OnPostToggleManagerRoleAsync"/> (the self-service role opt-in/out —
/// answers directly whether a request could tamper its way into a role other than
/// PortfolioManager) and the grant/revoke handlers (answers whether a caller-supplied id alone
/// can ever act as authorization for someone else's account).
/// </summary>
[TestClass]
public class DelegationModelTests
{
  private static UserManager<AppUser> NewUserManagerSubstitute()
  {
    var store = Substitute.For<IUserStore<AppUser>>();

    return Substitute.For<UserManager<AppUser>>(store, null, null, null, null, null, null, null, null);
  }

  private static DelegationModel NewModel(UserManager<AppUser> userManager, IPortfolioDelegationRepository delegationRepository, AppUser currentUser)
  {
    userManager.GetUserAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>()).Returns(currentUser);

    return new DelegationModel(userManager, delegationRepository)
    {
      PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
      TempData = Substitute.For<ITempDataDictionary>(),
    };
  }

  [TestMethod]
  public async Task OnPostToggleManagerRoleAsync_UserNotCurrentlyManager_AddsExactlyThePortfolioManagerRole_NeverAnyOtherRole()
  {
    // Arrange
    var user = new AppUser { Id = Guid.NewGuid() };
    var userManager = NewUserManagerSubstitute();
    userManager.IsInRoleAsync(user, Roles.PortfolioManager).Returns(false);
    userManager.AddToRoleAsync(Arg.Any<AppUser>(), Arg.Any<string>()).Returns(IdentityResult.Success);
    var model = NewModel(userManager, Substitute.For<IPortfolioDelegationRepository>(), user);

    // Act
    await model.OnPostToggleManagerRoleAsync();

    // Assert — the role string is a hardcoded server-side constant; this locks in that no request
    // shape can ever cause a different role (e.g. Admin) to be assigned through this handler,
    // since it takes zero parameters and never reads a role name from client input.
    await userManager.Received(1).AddToRoleAsync(user, Roles.PortfolioManager);
    await userManager.DidNotReceive().AddToRoleAsync(Arg.Any<AppUser>(), Arg.Is<string>(role => role != Roles.PortfolioManager));
    await userManager.DidNotReceive().RemoveFromRoleAsync(Arg.Any<AppUser>(), Arg.Any<string>());
  }

  [TestMethod]
  public async Task OnPostToggleManagerRoleAsync_UserCurrentlyManager_RemovesExactlyThePortfolioManagerRole()
  {
    // Arrange
    var user = new AppUser { Id = Guid.NewGuid() };
    var userManager = NewUserManagerSubstitute();
    userManager.IsInRoleAsync(user, Roles.PortfolioManager).Returns(true);
    userManager.RemoveFromRoleAsync(Arg.Any<AppUser>(), Arg.Any<string>()).Returns(IdentityResult.Success);
    var model = NewModel(userManager, Substitute.For<IPortfolioDelegationRepository>(), user);

    // Act
    await model.OnPostToggleManagerRoleAsync();

    // Assert
    await userManager.Received(1).RemoveFromRoleAsync(user, Roles.PortfolioManager);
    await userManager.DidNotReceive().AddToRoleAsync(Arg.Any<AppUser>(), Arg.Any<string>());
  }

  [TestMethod]
  public async Task OnPostGrantAsync_IdentifierResolvesToSelf_RejectsWithoutGrantingAccess()
  {
    // Arrange
    var client = new AppUser { Id = Guid.NewGuid(), UserName = "self", Email = "self@test.local" };
    var userManager = NewUserManagerSubstitute();
    userManager.FindByEmailAsync("self@test.local").Returns(client);
    var delegationRepository = Substitute.For<IPortfolioDelegationRepository>();
    var model = NewModel(userManager, delegationRepository, client);
    model.ManagerIdentifier = "self@test.local";

    // Act
    await model.OnPostGrantAsync();

    // Assert
    await delegationRepository.DidNotReceive().GrantAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
  }

  [TestMethod]
  public async Task OnPostGrantAsync_IdentifierResolvesToUserWithoutManagerRole_RejectsWithoutGrantingAccess()
  {
    // Arrange
    var client = new AppUser { Id = Guid.NewGuid() };
    var nonManager = new AppUser { Id = Guid.NewGuid(), Email = "notamanager@test.local" };
    var userManager = NewUserManagerSubstitute();
    userManager.FindByEmailAsync("notamanager@test.local").Returns(nonManager);
    userManager.IsInRoleAsync(nonManager, Roles.PortfolioManager).Returns(false);
    var delegationRepository = Substitute.For<IPortfolioDelegationRepository>();
    var model = NewModel(userManager, delegationRepository, client);
    model.ManagerIdentifier = "notamanager@test.local";

    // Act
    await model.OnPostGrantAsync();

    // Assert
    await delegationRepository.DidNotReceive().GrantAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
  }

  [TestMethod]
  public async Task OnPostGrantAsync_IdentifierNotFound_RejectsWithoutGrantingAccess()
  {
    // Arrange
    var client = new AppUser { Id = Guid.NewGuid() };
    var userManager = NewUserManagerSubstitute();
    userManager.FindByEmailAsync(Arg.Any<string>()).Returns((AppUser?)null);
    userManager.FindByNameAsync(Arg.Any<string>()).Returns((AppUser?)null);
    var delegationRepository = Substitute.For<IPortfolioDelegationRepository>();
    var model = NewModel(userManager, delegationRepository, client);
    model.ManagerIdentifier = "nobody@test.local";

    // Act
    await model.OnPostGrantAsync();

    // Assert
    await delegationRepository.DidNotReceive().GrantAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
  }

  [TestMethod]
  public async Task OnPostGrantAsync_ValidManagerIdentifier_GrantsAccessForCallerAsTheClient()
  {
    // Arrange
    var client = new AppUser { Id = Guid.NewGuid() };
    var manager = new AppUser { Id = Guid.NewGuid(), Email = "manager@test.local", DisplayName = "Manager" };
    var userManager = NewUserManagerSubstitute();
    userManager.FindByEmailAsync("manager@test.local").Returns(manager);
    userManager.IsInRoleAsync(manager, Roles.PortfolioManager).Returns(true);
    var delegationRepository = Substitute.For<IPortfolioDelegationRepository>();
    var model = NewModel(userManager, delegationRepository, client);
    model.ManagerIdentifier = "manager@test.local";

    // Act
    await model.OnPostGrantAsync();

    // Assert — argument order matters: (manager, client), not the reverse.
    await delegationRepository.Received(1).GrantAsync(manager.Id, client.Id);
  }

  /// <summary>
  /// Hufter-proofing: the handler only takes a managerId parameter — the client half of the pair
  /// must always come from the authenticated caller, never from anything the request could supply,
  /// or a caller could revoke (or, if this pattern were copied elsewhere, grant) access on behalf
  /// of a client they aren't.
  /// </summary>
  [TestMethod]
  public async Task OnPostRevokeGrantAsync_AlwaysScopesRevocationToTheCallerAsClient()
  {
    // Arrange
    var caller = new AppUser { Id = Guid.NewGuid() };
    var someManagerId = Guid.NewGuid();
    var delegationRepository = Substitute.For<IPortfolioDelegationRepository>();
    var model = NewModel(NewUserManagerSubstitute(), delegationRepository, caller);

    // Act
    await model.OnPostRevokeGrantAsync(someManagerId);

    // Assert
    await delegationRepository.Received(1).RevokeAsync(someManagerId, caller.Id);
  }

  [TestMethod]
  public async Task OnPostRevokeClientAsync_AlwaysScopesRevocationToTheCallerAsManager()
  {
    // Arrange
    var caller = new AppUser { Id = Guid.NewGuid() };
    var someClientId = Guid.NewGuid();
    var delegationRepository = Substitute.For<IPortfolioDelegationRepository>();
    var model = NewModel(NewUserManagerSubstitute(), delegationRepository, caller);

    // Act
    await model.OnPostRevokeClientAsync(someClientId);

    // Assert
    await delegationRepository.Received(1).RevokeAsync(caller.Id, someClientId);
  }
}
