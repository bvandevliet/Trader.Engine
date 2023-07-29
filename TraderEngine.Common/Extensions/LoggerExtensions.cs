using Microsoft.Extensions.Logging;

namespace TraderEngine.Common.Extensions;

public static class LoggerExtensions
{
  /// <summary>
  /// Logs the message of the exception and returns it afterwards.
  /// </summary>
  /// <param name="logger"></param>
  /// <param name="ex">Exception that should be logged and thrown.</param>
  public static TEx LogWarningReturnException<TEx>(this ILogger logger, TEx ex) where TEx : Exception
  {
    logger.LogWarning(ex, ex.Message); return ex;
  }

  /// <summary>
  /// Logs the message of the exception and returns it afterwards.
  /// </summary>
  /// <param name="logger"></param>
  /// <param name="ex">Exception that should be logged and thrown.</param>
  public static TEx LogErrorReturnException<TEx>(this ILogger logger, TEx ex) where TEx : Exception
  {
    logger.LogError(ex, ex.Message); return ex;
  }

  /// <summary>
  /// Logs the message of the exception and returns it afterwards.
  /// </summary>
  /// <param name="logger"></param>
  /// <param name="ex">Exception that should be logged and thrown.</param>
  public static TEx LogCriticalReturnException<TEx>(this ILogger logger, TEx ex) where TEx : Exception
  {
    logger.LogCritical(ex, ex.Message); return ex;
  }
}
