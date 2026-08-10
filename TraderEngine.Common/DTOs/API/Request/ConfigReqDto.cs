using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class ConfigReqDto
{
  [Display(Name = "Quote takeout", Description = "Takeout a given amount of quote currency.")]
  [Range(0, (double)decimal.MaxValue)]
  public decimal QuoteTakeout { get; set; } = 0;

  [Display(Name = "Quote allocation [%]", Description = "Allocate a given percentage to quote currency.")]
  [Range(0, 100)]
  public decimal QuoteAllocation { get; set; } = 0;

  //[Range(0, 10)]
  public Dictionary<string, double> WeightingOverrides { get; set; } = [];

  public List<string> TagsToInclude { get; set; } = [];

  public List<string> TagsToIgnore { get; set; } = ["stablecoin"];

  [Display(Name = "Max asset count [n]", Description = "Max amount of assets from CoinMarketCap listing, ranked by market cap. Excluding quote, and assets that contain ignored tags, i.e. they are not counted.")]
  [Range(0, 100)]
  public int TopRankingCount { get; set; } = 10;

  [Display(Name = "EMA smoothing [hrs]", Description = "Exponential Moving Average period of Market Cap, to smooth out volatility.")]
  [Range(1, 72)]
  public int Smoothing { get; set; } = 8;

  [Display(Name = "Nth root ^(1/[n])", Description = "Nth root of Market Cap EMA, to dampen the effect an individual asset has on the portfolio.")]
  [Range(1, 25)]
  public double NthRoot { get; set; } = 2.5;

  [Display(Name = "Min order size", Description = "The minimum order size in quote required to trigger an automated rebalance.")]
  [Range(0, int.MaxValue)]
  public int MinimumOrderSizeQuote { get; set; } = 5;

  [Display(Name = "Drift [%]", Description = "Minimum portfolio drift required to trigger an automated rebalance.")]
  [Range(0, 100)]
  public double DriftThresholdPercent { get; set; } = 1;

  [Display(Name = "Enable automation", Description = "Automatically perform portfolio rebalance when conditions are met.")]
  public bool AutomationEnabled { get; set; } = false;

  [Display(Name = "Use limit orders", Description = "Place limit orders at the best bid/ask to reduce fees, falling back to a market order for any unfilled remainder.")]
  public bool UseLimitOrders { get; set; } = false;

  [Display(Name = "Interval [hrs]", Description = "Minimum time interval between automated rebalance executions.")]
  [Range(1, 672)] // = 28 days (4 weeks)
  public int IntervalHours { get; set; } = 6;

  [Display(Name = "Held-asset bias mult", Description = "Selection-ranking multiplier applied to currently-held assets. Values above 1 bias toward retaining current holdings, reducing churn on borderline-ranked assets without distorting their computed weights.")]
  [Range(1, double.MaxValue)]
  public double HeldAssetBiasMult { get; set => field = Math.Max(1, value); } = 1.05;

  public DateTime? LastRebalance { get; set; } = null;
}