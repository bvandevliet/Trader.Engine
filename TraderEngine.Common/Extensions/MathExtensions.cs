namespace TraderEngine.Common.Extensions;

public static class MathExtensions
{
  public static decimal Round(this decimal value, int decimals)
  {
    return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
  }

  public static decimal Floor(this decimal value, int decimals)
  {
    int mult = (int)Math.Pow(10, decimals);

    return Math.Floor(value * mult) / mult;
  }

  public static decimal Ceiling(this decimal value, int decimals)
  {
    int mult = (int)Math.Pow(10, decimals);

    return Math.Ceiling(value * mult) / mult;
  }

  public static decimal GainPerc(this decimal value, decimal baseValue, int decimals)
  {
    return baseValue == 0 ? 0 : 100 * (value / baseValue - 1).Round(decimals);
  }
}
