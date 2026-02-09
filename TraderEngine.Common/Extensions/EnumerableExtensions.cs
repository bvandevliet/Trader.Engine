namespace TraderEngine.Common.Extensions;

public static class EnumerableExtensions
{
  /// <summary>
  /// Performs the specified <paramref name="action"/> on each element of the <paramref name="enumerable"/>./>.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <param name="enumerable"></param>
  /// <param name="action">The delegate to perform on each element of the <paramref name="enumerable"/></param>
  /// <returns></returns>
  public static void Each<T>(this IEnumerable<T> enumerable, Action<T> action)
  {
    foreach (var item in enumerable) { action(item); }
  }

  /// <inheritdoc cref="List{T}.Find(Predicate{T})"/>
  public static T? FindAndRemove<T>(this List<T> list, Predicate<T> match)
  {
    var index = list.FindIndex(match);

    if (index >= 0)
    {
      var item = list[index];

      list.RemoveAt(index);

      return item;
    }

    return default;
  }
}
