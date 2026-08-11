import { Tooltip } from 'bootstrap';
import { localizeTimestamps } from './shared/format';

localizeTimestamps();

// Bootstrap tooltips (e.g. info-tooltip's (i) icons) are opt-in and must be initialized explicitly.
document.querySelectorAll<HTMLElement>('[data-bs-toggle="tooltip"]').forEach(el => new Tooltip(el));

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