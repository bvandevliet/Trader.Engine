// Client-updated figures keep the same locale-aware grouping/decimal style (and don't
// visibly reformat once JS takes over from the server-rendered initial value).

export function numberFormat (value: number, decimals = 2): string
{
  return value.toLocaleString(undefined, {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  });
}

export function quoteSymbolFor (code: string): string
{
  switch (code)
  {
    case 'EUR':
      return '€';

    case 'USD':
      return '$';

    default:
      return code;
  }
}

function pad (n: number): string
{
  return n.toString().padStart(2, '0');
}

function formatLocal (date: Date, dateOnly: boolean, includeSeconds: boolean): string
{
  const datePart = `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;

  if (dateOnly) { return datePart; }

  const timePart = includeSeconds
    ? `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
    : `${pad(date.getHours())}:${pad(date.getMinutes())}`;

  return `${datePart} ${timePart}`;
}

// Server-rendered timestamps are always UTC (or an unambiguous offset) since the server's own
// timezone need not match the viewer's — <time datetime="..."> elements carry the raw instant
// and get rewritten here to the browser's local timezone once the client-side JS takes over.
export function localizeTimestamps (root: ParentNode = document): void
{
  root.querySelectorAll<HTMLTimeElement>('time[datetime]').forEach(el =>
  {
    const date = new Date(el.dateTime);

    if (Number.isNaN(date.getTime())) { return; }

    const dateOnly = el.dataset.utcDateOnly === 'true';
    const withSeconds = el.dataset.utcSeconds === 'true';

    el.textContent = formatLocal(date, dateOnly, withSeconds);

    if (el.dataset.utcTitle === 'true') { el.title = formatLocal(date, false, true); }
  });
}