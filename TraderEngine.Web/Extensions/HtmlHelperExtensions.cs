using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace TraderEngine.Web.Extensions;

public static class HtmlHelperExtensions
{
  /// <summary>
  /// Returns the [Display(Description = "...")] value for the given model property,
  /// the authoritative source for form field tooltips.
  /// </summary>
  public static string DescriptionFor<TModel, TResult>(
    this IHtmlHelper<TModel> html, Expression<Func<TModel, TResult>> expression)
  {
    var provider = html.ViewContext.HttpContext.RequestServices.GetRequiredService<IModelExpressionProvider>();

    var modelExpression = provider.CreateModelExpression(html.ViewData, expression);

    return modelExpression.Metadata.Description ?? string.Empty;
  }
}
