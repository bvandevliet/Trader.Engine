interface BalanceDto {
  quoteSymbol: string;
  amountQuoteAvailable: number;
  amountQuoteTotal: number;
}

const POLL_INTERVAL_MS = 5000;

function setField (field: string, value: string): void
{
  const cell = document.querySelector<HTMLTableCellElement>(`[data-field='${field}']`);

  if (cell) { cell.textContent = value; }
}

async function refreshBalance (): Promise<void>
{
  const response = await fetch('/dashboard/currentbalance');

  if (!response.ok) { return; }

  const balance: BalanceDto = await response.json();

  const depositedText = document.querySelector<HTMLTableCellElement>('[data-field=\'deposited\']')?.textContent ?? '0';
  const withdrawnText = document.querySelector<HTMLTableCellElement>('[data-field=\'withdrawn\']')?.textContent ?? '0';
  const deposited = parseFloat(depositedText.replace(/,/gu, ''));
  const withdrawn = parseFloat(withdrawnText.replace(/,/gu, ''));
  const cumulative = balance.amountQuoteTotal + withdrawn;
  const gain = cumulative - deposited;
  const gainPercent = deposited === 0 ? 0 : 100 * (cumulative / deposited - 1);

  setField('balance', balance.amountQuoteTotal.toFixed(2));
  setField('cumulative', cumulative.toFixed(2));
  setField('gain', gain.toFixed(2));
  setField('gain-percent', gainPercent.toFixed(2));
}

setInterval(refreshBalance, POLL_INTERVAL_MS);