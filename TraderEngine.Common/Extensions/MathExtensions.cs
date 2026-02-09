namespace TraderEngine.Common.Extensions;

public static class MathExtensions
{
  public static string Round(this decimal value, int decimals)
  {
    return Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString($"F{decimals}");
  }

  public static string Floor(this decimal value, int decimals)
  {
    var mult = (int)Math.Pow(10, decimals);

    return (Math.Floor(value * mult) / mult).ToString($"F{decimals}");
  }

  public static string Ceiling(this decimal value, int decimals)
  {
    var mult = (int)Math.Pow(10, decimals);

    return (Math.Ceiling(value * mult) / mult).ToString($"F{decimals}");
  }

  public static string GainPerc(this decimal value, decimal baseValue, int decimals)
  {
    return (baseValue == 0 ? 0 : 100 * (value / baseValue - 1)).Round(decimals);
  }
}
