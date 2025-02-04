using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class ConfigReqDto
{
  [Range(0, (double)decimal.MaxValue)]
  public decimal QuoteTakeout { get; set; } = 0;

  [Range(0, 100)]
  public decimal QuoteAllocation { get; set; } = 0;

  //[Range(0, 10)]
  public Dictionary<string, double> AltWeightingFactors { get; set; } = [];

  public bool DefensiveMode { get; set; } = false;

  public List<string> TagsToInclude { get; set; } = [];

  public List<string> TagsToIgnore { get; set; } = ["stablecoin"];

  [Range(0, 100)]
  public int TopRankingCount { get; set; } = 10;

  [Range(1, 72)]
  public int Smoothing { get; set; } = 8;

  [Range(1, 25)]
  public double NthRoot { get; set; } = 2.5;

  [Range(0, int.MaxValue)]
  public int MinimumDiffQuote { get; set; } = 5;

  [Range(0, 100)]
  public double MinimumDiffAllocation { get; set; } = 1;

  public bool AutomationEnabled { get; set; } = false;

  [Range(1, 672)] // = 28 days (4 weeks)
  public int IntervalHours { get; set; } = 6;

  [Range(1, double.MaxValue)]
  public double CurrentAllocWeightingMult
  {
    get => _currentAllocWeightingMult;
    set => _currentAllocWeightingMult = Math.Max(1, value);
  }
  private double _currentAllocWeightingMult = 1.05;

  public DateTime? LastRebalance { get; set; } = null;
}