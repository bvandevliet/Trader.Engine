using Microsoft.AspNetCore.Identity;
using NSubstitute;
using TraderEngine.Data.Constants;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Repositories;
using TraderEngine.Data.Services;

namespace TraderEngine.Data.Tests.Services;

/// <summary>
/// Covers <see cref="DelegationAuthorizationService.IsAuthorizedAsync"/> — the single check both
/// TraderEngine.Web (pre-check) and TraderEngine.API (<c>DelegatedAccessHandler</c>, independent
/// re-check) rely on. Every branch must fail closed: any missing/invalid input returns
/// <see langword="false"/>, never throws, and never authorizes on partial evidence (an active
/// grant row alone is not enough without live role membership, and vice versa).
/// </summary>
[TestClass]
public class DelegationAuthorizationServiceTests
{
  private static UserManager<AppUser> NewUserManagerSubstitute()
  {
    var store = Substitute.For<IUserStore<AppUser>>();

    return Substitute.For<UserManager<AppUser>>(store, null, null, null, null, null, null, null, null);
  }

  [TestMethod]
  public async Task IsAuthorizedAsync_ManagerEqualsClient_ReturnsFalse_WithoutQueryingTheGrantRepository()
  {
    // Arrange
    var userId = Guid.NewGuid();
    var delegationRepository = Substitute.For<IPortfolioDelegationRepository>();
    var userManager = NewUserManagerSubstitute();
    var service = new DelegationAuthorizationService(delegationRepository, userManager);

    // Act
    var isAuthorized = await service.IsAuthorizedAsync(userId, userId);

    // Assert — a self-grant must never be authorized even if a stray row somehow existed for it;
    // the check must reject on identity alone, not rely on the grant repository to catch it.
    Assert.IsFalse(isAuthorized);
    await delegationRepository.DidNotReceive().HasActiveGrantAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
  }

  [TestMethod]
  public async Task IsAuthorizedAsync_NoActiveGrant_ReturnsFalse_WithoutCheckingRoleMembership()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    var delegationRepository = Substitute.For<IPortfolioDelegationRepository>();
    delegationRepository.HasActiveGrantAsync(managerId, clientId).Returns(false);
    var userManager = NewUserManagerSubstitute();
    var service = new DelegationAuthorizationService(delegationRepository, userManager);

    // Act
    var isAuthorized = await service.IsAuthorizedAsync(managerId, clientId);

    // Assert — fails closed on the cheaper check first; never looks up the manager at all.
    Assert.IsFalse(isAuthorized);
    await userManager.DidNotReceive().FindByIdAsync(Arg.Any<string>());
  }

  [TestMethod]
  public async Task IsAuthorizedAsync_ActiveGrantButManagerAccountNoLongerExists_ReturnsFalse()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    var delegationRepository = Substitute.For<IPortfolioDelegationRepository>();
    delegationRepository.HasActiveGrantAsync(managerId, clientId).Returns(true);
    var userManager = NewUserManagerSubstitute();
    userManager.FindByIdAsync(managerId.ToString()).Returns((AppUser?)null);
    var service = new DelegationAuthorizationService(delegationRepository, userManager);

    // Act
    var isAuthorized = await service.IsAuthorizedAsync(managerId, clientId);

    // Assert
    Assert.IsFalse(isAuthorized);
  }

  /// <summary>
  /// The live-role-recheck scenario this service exists for: a manager's role was removed
  /// (self opt-out, or an admin force-demoting them) after a grant was made — the grant row alone
  /// must not be enough to authorize.
  /// </summary>
  [TestMethod]
  public async Task IsAuthorizedAsync_ActiveGrantButManagerNoLongerHoldsPortfolioManagerRole_ReturnsFalse()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    var manager = new AppUser
    {
      Id = managerId,
      UserName = "manager"
    };
    var delegationRepository = Substitute.For<IPortfolioDelegationRepository>();
    delegationRepository.HasActiveGrantAsync(managerId, clientId).Returns(true);
    var userManager = NewUserManagerSubstitute();
    userManager.FindByIdAsync(managerId.ToString()).Returns(manager);
    userManager.IsInRoleAsync(manager, Roles.PortfolioManager).Returns(false);
    var service = new DelegationAuthorizationService(delegationRepository, userManager);

    // Act
    var isAuthorized = await service.IsAuthorizedAsync(managerId, clientId);

    // Assert
    Assert.IsFalse(isAuthorized);
  }

  [TestMethod]
  public async Task IsAuthorizedAsync_ActiveGrantAndManagerHoldsRole_ReturnsTrue()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    var manager = new AppUser
    {
      Id = managerId,
      UserName = "manager"
    };
    var delegationRepository = Substitute.For<IPortfolioDelegationRepository>();
    delegationRepository.HasActiveGrantAsync(managerId, clientId).Returns(true);
    var userManager = NewUserManagerSubstitute();
    userManager.FindByIdAsync(managerId.ToString()).Returns(manager);
    userManager.IsInRoleAsync(manager, Roles.PortfolioManager).Returns(true);
    var service = new DelegationAuthorizationService(delegationRepository, userManager);

    // Act
    var isAuthorized = await service.IsAuthorizedAsync(managerId, clientId);

    // Assert
    Assert.IsTrue(isAuthorized);
  }
}