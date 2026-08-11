using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace TraderEngine.Web.TagHelpers;

/// <summary>
/// Inspired by Blazor's NavLink/NavLinkMatch: <see cref="All"/> requires an exact match with the
/// current page. <see cref="Prefix"/> adapts the idea to Razor Pages' flat page namespace (which
/// has no href-hierarchy for Blazor's literal URL-prefix matching to use) by matching any page in
/// the same folder instead, so a "section" link (e.g. "/Admin/Users") stays active on sibling
/// pages in that folder (e.g. "/Admin/EditUser").
/// </summary>
public enum NavLinkMatch
{
  All,
  Prefix,
}

/// <summary>
/// Marks an anchor's "nav-link" class as active, with aria-current="page", when its
/// asp-page target matches the currently executing Razor Page. Scoped to class="nav-link"
/// so it can't unintentionally tag unrelated asp-page links elsewhere on a page.
/// </summary>
[HtmlTargetElement("a", Attributes = "asp-page,class")]
public class NavLinkTagHelper : TagHelper
{
  [HtmlAttributeName("asp-page")]
  public string? Page { get; set; }

  [HtmlAttributeName("nav-link-match")]
  public NavLinkMatch Match { get; set; } = NavLinkMatch.All;

  [ViewContext]
  public ViewContext ViewContext { get; set; } = default!;

  public override void Process(TagHelperContext context, TagHelperOutput output)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(output);

    var cssClass = output.Attributes["class"].Value.ToString()!;

    if (!cssClass.Split(' ').Contains("nav-link"))
    {
      return;
    }

    var currentPage = ViewContext.RouteData.Values["page"]?.ToString();

    if (currentPage is null || Page is null)
    {
      return;
    }

    var isActive = Match == NavLinkMatch.Prefix
      ? string.Equals(GetFolder(currentPage), GetFolder(Page), StringComparison.OrdinalIgnoreCase)
      : string.Equals(currentPage, Page, StringComparison.OrdinalIgnoreCase);

    if (isActive)
    {
      output.Attributes.SetAttribute("class", $"{cssClass} active");
      output.Attributes.SetAttribute("aria-current", "page");
    }
  }

  // Root-level pages (e.g. "/Dashboard") have no folder to share, so a root-level
  // Prefix link would otherwise never match; treat their own page path as the folder.
  private static string GetFolder(string page)
  {
    var lastSlash = page.LastIndexOf('/');

    return lastSlash > 0 ? page[..lastSlash] : page;
  }
}
