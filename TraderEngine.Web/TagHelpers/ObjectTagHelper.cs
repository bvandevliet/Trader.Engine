using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace TraderEngine.Web.TagHelpers;

/// <summary>
/// Adds <c>asp-append-version</c> support to &lt;object data="..."&gt;, mirroring the built-in
/// ImageTagHelper/ScriptTagHelper/LinkTagHelper (which only target img/script/link — there is no
/// stock tag helper for object's file-versioning). See "Web frontend theming" in CLAUDE.md.
/// </summary>
[HtmlTargetElement("object", Attributes = AppendVersionAttributeName + "," + DataAttributeName)]
public class ObjectTagHelper : UrlResolutionTagHelper
{
  private const string AppendVersionAttributeName = "asp-append-version";
  private const string DataAttributeName = "data";

  private readonly IFileVersionProvider _fileVersionProvider;

  public ObjectTagHelper(IFileVersionProvider fileVersionProvider, HtmlEncoder htmlEncoder, IUrlHelperFactory urlHelperFactory)
    : base(urlHelperFactory, htmlEncoder)
  {
    _fileVersionProvider = fileVersionProvider;
  }

  // Matches ImageTagHelper/ScriptTagHelper/LinkTagHelper's Order, so this runs before the
  // generic UrlResolutionTagHelper instance that would otherwise resolve "data" first.
  public override int Order => -1000;

  [HtmlAttributeName(DataAttributeName)]
  public string? Data { get; set; }

  [HtmlAttributeName(AppendVersionAttributeName)]
  public bool AppendVersion { get; set; }

  // ViewContext is inherited from UrlResolutionTagHelper — no need to redeclare it.

  public override void Process(TagHelperContext context, TagHelperOutput output)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(output);

    if (AppendVersion && Data is not null)
    {
      // Still "~/..." at this point — AddFileVersionToPath only appends "?v=", tilde
      // resolution happens next via the inherited ProcessUrlAttribute call below.
      output.Attributes.SetAttribute(
        DataAttributeName,
        _fileVersionProvider.AddFileVersionToPath(ViewContext.HttpContext.Request.PathBase, Data));
    }

    ProcessUrlAttribute(DataAttributeName, output);
  }
}
