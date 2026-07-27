// Adapted from Bootstrap's official color mode toggler:
// https://getbootstrap.com/docs/5.3/customize/color-modes/
// (here as a single cycle-through button instead of their light/dark/auto
// dropdown, but reusing the same storage/detection mechanics)
//
// Loaded as a plain, non-deferred <script src> at the top of <head> (not via
// site.js, which loads deferred at the end of <body>) per that guidance, so
// the theme is set before first paint and avoids a flash of the wrong theme.

type Theme = 'light' | 'dark' | 'auto';

const themeIcons: Record<Theme, string> = {
  light: 'bi-sun-fill',
  dark: 'bi-moon-stars-fill',
  auto: 'bi-circle-half',
};

const themeCycle: readonly Theme[] = ['light', 'dark', 'auto'];

const prefersDarkMedia = window.matchMedia('(prefers-color-scheme: dark)');

function getStoredTheme (): Theme | null
{
  return localStorage.getItem('theme') as Theme | null;
}

function setStoredTheme (theme: Theme): void
{
  localStorage.setItem('theme', theme);
}

function getPreferredTheme (): Theme
{
  return getStoredTheme() ?? 'auto';
}

// Syncs the navbar's <object>-embedded favicon.svg to the resolved theme —
// see "Web frontend theming" in CLAUDE.md for why this is needed at all.
function syncBrandLogo (resolvedTheme: 'light' | 'dark'): void
{
  const logoObject = document.getElementById('navbar-brand-logo') as HTMLObjectElement | null;
  const svgDoc = logoObject?.contentDocument;
  const lightGroup = svgDoc?.getElementById('light-icon');
  const darkGroup = svgDoc?.getElementById('dark-icon');

  if (!svgDoc?.documentElement || !lightGroup || !darkGroup) { return; }

  lightGroup.style.display = resolvedTheme === 'light' ? 'inline' : 'none';
  darkGroup.style.display = resolvedTheme === 'dark' ? 'inline' : 'none';
}

function setTheme (theme: Theme): void
{
  const resolved = theme === 'auto'
    ? (prefersDarkMedia.matches ? 'dark' : 'light')
    : theme;

  document.documentElement.setAttribute('data-bs-theme', resolved);
  syncBrandLogo(resolved);
}

// Applied immediately (not deferred to DOMContentLoaded) to avoid FOUC.
// The <object> hasn't loaded yet at this point (syncBrandLogo no-ops via its
// null checks) — the "load" listener below re-applies it once it has.
setTheme(getPreferredTheme());

function updateToggleButton (theme: Theme): void
{
  const button = document.querySelector<HTMLButtonElement>('#bd-theme');
  const icon = document.querySelector<HTMLElement>('.theme-icon-active');

  if (!button || !icon) { return; }

  icon.className = `bi ${themeIcons[theme]} theme-icon-active`;
  button.dataset.bsThemeValue = theme;
  button.setAttribute('aria-label', `Toggle theme (currently ${theme})`);
}

prefersDarkMedia.addEventListener('change', () =>
{
  // Only re-resolve automatically while "auto" is active — an explicit
  // light/dark choice should never be overridden by the OS changing.
  if (getPreferredTheme() === 'auto')
  {
    setTheme('auto');
  }
});

function currentResolvedTheme (): 'light' | 'dark'
{
  return document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'dark' : 'light';
}

window.addEventListener('DOMContentLoaded', () =>
{
  const button = document.querySelector<HTMLButtonElement>('#bd-theme');
  const logoObject = document.getElementById('navbar-brand-logo') as HTMLObjectElement | null;

  updateToggleButton(getPreferredTheme());

  // The <object> loads asynchronously and likely isn't ready yet when
  // setTheme() first ran above, so re-apply once its contentDocument exists.
  logoObject?.addEventListener('load', () => syncBrandLogo(currentResolvedTheme()));

  // ...but it may ALSO have already finished loading before this listener was
  // attached — its own fetch can start (and finish) before DOMContentLoaded
  // fires, since the <object> begins loading as soon as its tag is parsed,
  // well before the rest of the document. In that case the 'load' event
  // already happened and won't fire again, silently leaving the logo stuck
  // on whatever favicon.svg's own prefers-color-scheme rule picked. Apply
  // directly using whatever's already there as a fallback for that race.
  if (logoObject?.contentDocument)
  {
    syncBrandLogo(currentResolvedTheme());
  }

  button?.addEventListener('click', () =>
  {
    const current = (button.dataset.bsThemeValue as Theme | undefined) ?? getPreferredTheme();
    const next = themeCycle[(themeCycle.indexOf(current) + 1) % themeCycle.length];

    setStoredTheme(next);
    setTheme(next);
    updateToggleButton(next);
  });
});