using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using TraderEngine.Data.Entities;

namespace TraderEngine.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Lets the signed-in user view their email confirmation status, request a new verification
/// email, and change their email (via a confirmation link sent to the new address, handled by
/// the default Identity UI's ConfirmEmailChange page).
/// </summary>
public class EmailModel : PageModel
{
  private readonly UserManager<AppUser> _userManager;
  private readonly IEmailSender<AppUser> _emailSender;

  public EmailModel(UserManager<AppUser> userManager, IEmailSender<AppUser> emailSender)
  {
    _userManager = userManager;
    _emailSender = emailSender;
  }

  public string Email { get; set; } = string.Empty;

  public bool IsEmailConfirmed { get; set; }

  [TempData]
  public string? StatusMessage { get; set; }

  [BindProperty]
  public InputModel Input { get; set; } = new();

  public class InputModel
  {
    [Required]
    [EmailAddress]
    [Display(Name = "New email")]
    public string NewEmail { get; set; } = string.Empty;
  }

  private async Task LoadAsync(AppUser user)
  {
    Email = await _userManager.GetEmailAsync(user) ?? string.Empty;
    IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);

    Input = new InputModel
    {
      NewEmail = Email,
    };
  }

  public async Task<IActionResult> OnGetAsync()
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null)
      return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

    await LoadAsync(user);
    return Page();
  }

  public async Task<IActionResult> OnPostChangeEmailAsync()
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null)
      return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

    if (!ModelState.IsValid)
    {
      await LoadAsync(user);
      return Page();
    }

    var email = await _userManager.GetEmailAsync(user);
    if (Input.NewEmail != email)
    {
      var userId = await _userManager.GetUserIdAsync(user);
      var code = await _userManager.GenerateChangeEmailTokenAsync(user, Input.NewEmail);
      code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
      var callbackUrl = Url.Page(
        "/Account/ConfirmEmailChange",
        pageHandler: null,
        values: new { area = "Identity", userId, email = Input.NewEmail, code },
        protocol: Request.Scheme)!;

      await _emailSender.SendConfirmationLinkAsync(user, Input.NewEmail, HtmlEncoder.Default.Encode(callbackUrl));

      StatusMessage = "Confirmation link to change email sent. Please check your email.";
      return RedirectToPage();
    }

    StatusMessage = "Your email is unchanged.";
    return RedirectToPage();
  }

  public async Task<IActionResult> OnPostSendVerificationEmailAsync()
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null)
      return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

    if (!ModelState.IsValid)
    {
      await LoadAsync(user);
      return Page();
    }

    var userId = await _userManager.GetUserIdAsync(user);
    var email = await _userManager.GetEmailAsync(user);
    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
    var callbackUrl = Url.Page(
      "/Account/ConfirmEmail",
      pageHandler: null,
      values: new { area = "Identity", userId, code },
      protocol: Request.Scheme)!;

    await _emailSender.SendConfirmationLinkAsync(user, email!, HtmlEncoder.Default.Encode(callbackUrl));

    StatusMessage = "Verification email sent. Please check your email.";
    return RedirectToPage();
  }
}
