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

  /// <summary>
  /// <inheritdoc cref="List{T}.Find(Predicate{T})"/>
  /// If found, removes the item from the list.
  /// </summary>
  /// <typeparam name="T"><inheritdoc cref="List{T}.Find(Predicate{T})"/></typeparam>
  /// <param name="list"><inheritdoc cref="List{T}.Find(Predicate{T})"/></param>
  /// <param name="predicate"><inheritdoc cref="List{T}.Find(Predicate{T})"/></param>
  /// <returns><inheritdoc cref="List{T}.Find(Predicate{T})"/></returns>
  public static T? FindAndRemove<T>(this List<T> list, Predicate<T> predicate)
  {
    int index = list.FindIndex(predicate);

    if (index >= 0)
    {
      var item = list[index];

      list.RemoveAt(index);

      return item;
    }

    return default;
  }
}
