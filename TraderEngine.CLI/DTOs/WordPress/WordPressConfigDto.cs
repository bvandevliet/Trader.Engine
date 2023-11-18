namespace TraderEngine.CLI.DTOs.WordPress;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
public class WordPressConfigDto
{
  public decimal quote_takeout { get; set; } = 0;

  public decimal quote_allocation { get; set; } = 0;

  public Dictionary<string, double> alt_weighting_factors { get; set; } = new();

  public List<string> tags_to_ignore { get; set; } = new();

  public int top_ranking_count { get; set; }

  public int smoothing { get; set; }

  public double nth_root { get; set; }

  public int minimum_diff_quote { get; set; }

  public double minimum_diff_allocation { get; set; }

  public bool automation_enabled { get; set; } = false;

  public int interval_hours { get; set; }

  public DateTime? last_rebalance { get; set; } = null;
}