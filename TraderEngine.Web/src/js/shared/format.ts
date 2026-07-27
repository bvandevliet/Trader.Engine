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