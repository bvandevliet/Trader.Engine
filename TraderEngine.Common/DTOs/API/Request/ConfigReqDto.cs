using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class ConfigReqDto
{
  [Required]
  public string QuoteCurrency { get; set; } = null!;

  [Range(0, (double)decimal.MaxValue)]
  public decimal QuoteTakeout { get; set; } = 0;

  [Range(0, 100)]
  public decimal QuoteAllocation { get; set; } = 0;

  public Dictionary<string, decimal> AltWeightingFactors { get; set; } = new();

  public List<string> TagsToIgnore { get; set; } = new() { "stablecoin" };

  [Range(0, 70)]
  public int TopRankingCount { get; set; } = 10;

  [Range(1, 40)]
  public decimal Smoothing { get; set; } = 4;

  [Range(1, 25)]
  public decimal NthRoot { get; set; } = 2.5m;

  [Range(1, (double)decimal.MaxValue)]
  public decimal IntervalHours { get; set; } = 6;

  [Range(0, (double)decimal.MaxValue)]
  public decimal MinimumDiffQuote { get; set; } = 5;

  [Range(0, 100)]
  public decimal MinimumDiffAllocation { get; set; } = 1.2m;

  public bool AutomationEnabled { get; set; } = false;

  public DateTime? LastRebalance { get; set; } = null;
}