using static TraderEngine.CLI.Helpers.WordPressDbSerializer;

namespace TraderEngine.CLI.DTOs.WordPress;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
[WordPressObject("Trader\\Configuration")]
public class WordPressConfigDto
{
  public decimal quote_takeout { get; set; } = 0;

  public decimal quote_allocation { get; set; } = 0;

  public Dictionary<string, double> alt_weighting_factors { get; set; } = [];

  public bool defensive_mode { get; set; } = false;

  public List<string> tags_to_include { get; set; } = [];

  public List<string> tags_to_ignore { get; set; } = [];

  public int top_ranking_count { get; set; }

  public int smoothing { get; set; }

  public double nth_root { get; set; }

  public int minimum_diff_quote { get; set; }

  public double minimum_diff_allocation { get; set; }

  public bool automation_enabled { get; set; } = false;

  public int interval_hours { get; set; }

  public double current_alloc_weighting_mult { get; set; }

  public DateTime? last_rebalance { get; set; } = null;
}