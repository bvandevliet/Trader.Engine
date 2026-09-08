using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Repositories;
using TraderEngine.Web.Services;

namespace TraderEngine.Web.Pages;

/// <summary>
/// Advanced allocation config: tag include/exclude rules and per-asset weighting overrides — a
/// dedicated page for the three <see cref="ConfigReqDto"/> fields the Rebalance page's form never touches.
/// </summary>
public class ConfigModel : TraderEnginePageModelBase
{
  private readonly IConfigRepository _configRepository;
  private readonly IDelegatedAccessResolver _delegatedAccessResolver;

  public ConfigModel(UserManager<AppUser> userManager, IConfigRepository configRepository, IDelegatedAccessResolver delegatedAccessResolver)
    : base(userManager)
  {
    _configRepository = configRepository;
    _delegatedAccessResolver = delegatedAccessResolver;
  }

  /// <summary>
  /// Carries the acting-as-client context across the page's plain form POST (no JS on this page,
  /// unlike Dashboard) — a hidden input, not a query string, since this page's form always POSTs
  /// to its own URL.
  /// </summary>
  [BindProperty(SupportsGet = true)]
  public Guid? ActingAsClientId { get; set; }

  /// <summary>
  /// Null when editing your own config; otherwise the client whose config you're currently
  /// editing as their portfolio manager.
  /// </summary>
  public AppUser? ActingForClient { get; set; }

  [BindProperty]
  public List<string> TagsToInclude { get; set; } = [];

  [BindProperty]
  public List<string> TagsToIgnore { get; set; } = [];

  [BindProperty]
  public List<string> OverrideAssets { get; set; } = [];

  [BindProperty]
  public List<double> OverrideWeights { get; set; } = [];

  public async Task<IActionResult> OnGetAsync()
  {
    var caller = await GetCurrentUserAsync();

    DelegatedAccessContext ctx;

    try
    {
      ctx = await _delegatedAccessResolver.ResolveAsync(caller, ActingAsClientId);
    }
    catch (DelegationAccessDeniedException ex)
    {
      TempData["Error"] = ex.Message;
      return RedirectToPage("/Delegation");
    }

    ActingForClient = ctx.IsDelegated ? ctx.EffectiveUser : null;

    var config = await _configRepository.GetConfig(ctx.EffectiveUser.Id);

    TagsToInclude = config.TagsToInclude;
    TagsToIgnore = config.TagsToIgnore;
    OverrideAssets = config.WeightingOverrides.Keys.ToList();
    OverrideWeights = config.WeightingOverrides.Values.ToList();

    return Page();
  }

  public async Task<IActionResult> OnPostAsync()
  {
    if (!ValidateTagPatterns())
      return Page();

    var caller = await GetCurrentUserAsync();

    DelegatedAccessContext ctx;

    try
    {
      ctx = await _delegatedAccessResolver.ResolveAsync(caller, ActingAsClientId);
    }
    catch (DelegationAccessDeniedException ex)
    {
      TempData["Error"] = ex.Message;
      return RedirectToPage("/Delegation");
    }

    var config = await _configRepository.GetConfig(ctx.EffectiveUser.Id);

    ApplyTo(config);

    await _configRepository.SaveConfig(ctx.EffectiveUser.Id, config);

    TempData["Notice"] = "Configuration updated.";

    return RedirectToPage(new { ActingAsClientId });
  }

  /// <summary>
  /// These patterns are compiled into a live <see cref="Regex"/> by
  /// <c>MarketCapService</c> when ranking assets for a rebalance — not evaluated by the browser's
  /// JS regex engine, which uses different syntax rules. An invalid pattern saved here would
  /// otherwise only surface as an unhandled error later, during a Rebalance simulation, far from
  /// where the mistake was actually made. The client-side check in config.ts is a UX nicety only;
  /// this is the authoritative check against the regex flavor that actually matters.
  /// </summary>
  private bool ValidateTagPatterns()
  {
    var isValid = true;

    void ValidateList(List<string> tags, string fieldKey)
    {
      for (var i = 0; i < tags.Count; i++)
      {
        var tag = tags[i].Trim();

        if (tag.Length == 0)
          continue;

        try
        {
          _ = new Regex(tag);
        }
        catch (ArgumentException)
        {
          ModelState.AddModelError($"{fieldKey}[{i}]", $"\"{tag}\" is not a valid regex pattern.");
          isValid = false;
        }
      }
    }

    ValidateList(TagsToInclude, nameof(TagsToInclude));
    ValidateList(TagsToIgnore, nameof(TagsToIgnore));

    return isValid;
  }

  /// <summary>
  /// Tag patterns lowercased, trimmed, emptied entries dropped, then sorted alphabetically;
  /// weighting asset symbols uppercased, non-numeric/empty rows dropped, weights clamped to a
  /// minimum of 0, final map ordered by weight descending.
  /// </summary>
  private void ApplyTo(ConfigReqDto config)
  {
    config.TagsToInclude = TagsToInclude
      .Select(tag => tag.Trim().ToLowerInvariant())
      .Where(tag => tag.Length > 0)
      .Distinct()
      .OrderBy(tag => tag, StringComparer.Ordinal)
      .ToList();

    config.TagsToIgnore = TagsToIgnore
      .Select(tag => tag.Trim().ToLowerInvariant())
      .Where(tag => tag.Length > 0)
      .Distinct()
      .OrderBy(tag => tag, StringComparer.Ordinal)
      .ToList();

    var pairCount = Math.Min(OverrideAssets.Count, OverrideWeights.Count);

    config.WeightingOverrides = Enumerable.Range(0, pairCount)
      .Select(i => (Asset: OverrideAssets[i].Trim().ToUpperInvariant(), Weighting: OverrideWeights[i]))
      .Where(pair => pair.Asset.Length > 0)
      .GroupBy(pair => pair.Asset)
      .ToDictionary(group => group.Key, group => Math.Max(0, group.Last().Weighting))
      .OrderByDescending(pair => pair.Value)
      .ToDictionary(pair => pair.Key, pair => pair.Value);
  }
}
