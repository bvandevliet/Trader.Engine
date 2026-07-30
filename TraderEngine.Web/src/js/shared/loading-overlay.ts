// Reusable "spinning brand logo" overlay for any AJAX-refreshed block (rebalance table,
// dashboard balance summary, ...). Injected via JS rather than hand-written per page so the
// markup/behavior only lives in one place.

export interface LoadingOverlay {
  show(): void;
  hide(): void;
}

export function attachLoadingOverlay (host: HTMLElement): LoadingOverlay
{
  host.classList.add('position-relative');

  const overlay = document.createElement('div');

  overlay.className = 'loading-overlay d-none';

  const spinner = document.createElement('img');

  spinner.src = '/images/favicon.svg';
  spinner.width = 48;
  spinner.height = 48;
  spinner.alt = '';
  spinner.className = 'loading-overlay-spinner';

  overlay.appendChild(spinner);
  host.appendChild(overlay);

  return {
    show: () => overlay.classList.remove('d-none'),
    hide: () => overlay.classList.add('d-none'),
  };
}