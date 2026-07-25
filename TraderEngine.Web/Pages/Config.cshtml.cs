using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Repositories;

namespace TraderEngine.Web.Pages;

/// <summary>
/// Advanced allocation config: tag include/exclude rules and per-asset weighting overrides — a
/// dedicated page for the three <see cref="ConfigReqDto"/> fields the Rebalance page's form never
/// touches, matching the old WordPress "Trader Configuration" block.
/// </summary>
public class ConfigModel : TraderEnginePageModelBase
{
  private readonly IConfigRepository _configRepository;

  public ConfigModel(UserManager<AppUser> userManager, IConfigRepository configRepository)
    : base(userManager)
  {
    _configRepository = configRepository;
  }

  [BindProperty]
  public List<string> TagsToInclude { get; set; } = [];

  [BindProperty]
  public List<string> TagsToIgnore { get; set; } = [];

  [BindProperty]
  public List<string> WeightingAssets { get; set; } = [];

  [BindProperty]
  public List<double> WeightingValues { get; set; } = [];

  public async Task OnGetAsync()
  {
    var user = await GetCurrentUserAsync();
    var config = await _configRepository.GetConfig(user.Id);

    TagsToInclude = config.TagsToInclude;
    TagsToIgnore = config.TagsToIgnore;
    WeightingAssets = config.AltWeightingFactors.Keys.ToList();
    WeightingValues = config.AltWeightingFactors.Values.ToList();
  }

  public async Task<IActionResult> OnPostAsync()
  {
    var user = await GetCurrentUserAsync();
    var config = await _configRepository.GetConfig(user.Id);

    ApplyTo(config);

    await _configRepository.SaveConfig(user.Id, config);

    TempData["Notice"] = "Configuration updated.";

    return RedirectToPage();
  }

  /// <summary>
  /// Applies the same normalization the previous WordPress block enforced server-side on save:
  /// tag patterns lowercased, trimmed, emptied entries dropped, then sorted alphabetically;
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

    var pairCount = Math.Min(WeightingAssets.Count, WeightingValues.Count);

    config.AltWeightingFactors = Enumerable.Range(0, pairCount)
      .Select(i => (Asset: WeightingAssets[i].Trim().ToUpperInvariant(), Weighting: WeightingValues[i]))
      .Where(pair => pair.Asset.Length > 0)
      .GroupBy(pair => pair.Asset)
      .ToDictionary(group => group.Key, group => Math.Max(0, group.Last().Weighting))
      .OrderByDescending(pair => pair.Value)
      .ToDictionary(pair => pair.Key, pair => pair.Value);
  }
}
