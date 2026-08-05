namespace TraderEngine.Data.Entities;

/// <summary>
/// Per-user rebalancing configuration. <see cref="UserId"/> is both primary key and
/// foreign key to <see cref="AppUser"/> (one-to-one).
/// </summary>
public class RebalancingConfiguration
{
  public Guid UserId { get; set; }

  public AppUser User { get; set; } = null!;

  public decimal QuoteTakeout { get; set; }

  public decimal QuoteAllocation { get; set; }

  public Dictionary<string, double> AltWeightingFactors { get; set; } = [];

  public List<string> TagsToInclude { get; set; } = [];

  public List<string> TagsToIgnore { get; set; } = [];

  public int TopRankingCount { get; set; }

  public int Smoothing { get; set; }

  public double NthRoot { get; set; }

  public int MinimumDiffQuote { get; set; }

  public double MinimumDiffAllocation { get; set; }

  public bool AutomationEnabled { get; set; }

  public int IntervalHours { get; set; }

  public double CurrentAllocWeightingMult { get; set; }

  public DateTime? LastRebalance { get; set; }
}
