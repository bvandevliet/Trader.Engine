using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace TraderEngine.Web.TagHelpers;

/// <summary>
/// Renders an (i) icon carrying a Bootstrap tooltip populated from the target property's
/// [Display(Description = "...")], initialized client-side in site.ts. Used instead of a plain
/// title attribute on the label/input itself, since native title tooltips don't work on touch
/// devices (no hover), unlike Bootstrap's tooltip component.
/// </summary>
public class InfoTooltipTagHelper : TagHelper
{
  [HtmlAttributeName("asp-for")]
  public ModelExpression For { get; set; } = default!;

  public override void Process(TagHelperContext context, TagHelperOutput output)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(output);

    output.TagName = "i";
    output.TagMode = TagMode.StartTagAndEndTag;
    output.Attributes.SetAttribute("class", "bi bi-info-circle text-muted ms-1");
    output.Attributes.SetAttribute("data-bs-toggle", "tooltip");
    output.Attributes.SetAttribute("tabindex", "0");
    output.Attributes.SetAttribute("title", For.Metadata.Description ?? string.Empty);
  }
}
