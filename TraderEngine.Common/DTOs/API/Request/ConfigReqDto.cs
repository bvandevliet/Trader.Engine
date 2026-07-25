using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class ConfigReqDto
{
  [Display(Name = "Quote takeout")]
  [Range(0, (double)decimal.MaxValue)]
  public decimal QuoteTakeout { get; set; } = 0;

  [Display(Name = "Quote allocation [%]")]
  [Range(0, 100)]
  public decimal QuoteAllocation { get; set; } = 0;

  //[Range(0, 10)]
  public Dictionary<string, double> AltWeightingFactors { get; set; } = [];

  public List<string> TagsToInclude { get; set; } = [];

  public List<string> TagsToIgnore { get; set; } = ["stablecoin"];

  [Display(Name = "Top ranking count [n]")]
  [Range(0, 100)]
  public int TopRankingCount { get; set; } = 10;

  [Display(Name = "Smoothing [hours]")]
  [Range(1, 72)]
  public int Smoothing { get; set; } = 8;

  [Display(Name = "Nth root ^(1/[n])")]
  [Range(1, 25)]
  public double NthRoot { get; set; } = 2.5;

  [Display(Name = "Minimum order size")]
  [Range(0, int.MaxValue)]
  public int MinimumDiffQuote { get; set; } = 5;

  [Display(Name = "Tracking error [%]")]
  [Range(0, 100)]
  public double MinimumDiffAllocation { get; set; } = 1;

  [Display(Name = "Automation")]
  public bool AutomationEnabled { get; set; } = false;

  [Display(Name = "Rebalance interval [hrs]")]
  [Range(1, 672)] // = 28 days (4 weeks)
  public int IntervalHours { get; set; } = 6;

  [Display(Name = "Current alloc weighting mult")]
  [Range(1, double.MaxValue)]
  public double CurrentAllocWeightingMult
  {
    get => _currentAllocWeightingMult;
    set => _currentAllocWeightingMult = Math.Max(1, value);
  }
  private double _currentAllocWeightingMult = 1.05;

  public DateTime? LastRebalance { get; set; } = null;
}