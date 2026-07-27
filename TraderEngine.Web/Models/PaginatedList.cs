namespace TraderEngine.Web.Models;

public class PaginatedList<T> : List<T>
{
  public int PageIndex { get; private set; }
  public int TotalPages { get; private set; }

  public bool HasPreviousPage => PageIndex > 1;
  public bool HasNextPage => PageIndex < TotalPages;

  private PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
  {
    PageIndex = pageIndex;
    TotalPages = (int)Math.Ceiling(count / (double)pageSize);
    AddRange(items);
  }

  public static PaginatedList<T> Create(List<T> items, int totalCount, int pageIndex, int pageSize)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(pageIndex, 1);
    ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

    return new PaginatedList<T>(items, totalCount, pageIndex, pageSize);
  }
}
