import 'bootstrap';

// Custom JavaScript for confirmation dialogs on elements with the data-confirm attribute.
document.querySelectorAll<HTMLElement>('[data-confirm]').forEach(btn =>
{
  btn.addEventListener('click', e =>
  {
    const message = btn.getAttribute('data-confirm');

    if (message && !confirm(message))
    {
      e.preventDefault();
    }
  });
});