import { numberFormat } from './shared/format';

interface BalanceDto {
  quoteSymbol: string;
  amountQuoteAvailable: number;
  amountQuoteTotal: number;
}

interface BalanceInit {
  totalDeposited: number;
  totalWithdrawn: number;
}

const POLL_INTERVAL_MS = 5000;

const balanceInitEl = document.getElementById('balance-init');

// Kept as real numbers from the server-rendered initial state —
// never re-derived by parsing the (locale-formatted) rendered table text back out.
const { totalDeposited, totalWithdrawn }: BalanceInit = balanceInitEl?.textContent
  ? JSON.parse(balanceInitEl.textContent)
  : { totalDeposited: 0, totalWithdrawn: 0 };

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

  const cumulative = balance.amountQuoteTotal + totalWithdrawn;
  const gain = cumulative - totalDeposited;
  const gainPercent = totalDeposited === 0 ? 0 : 100 * (cumulative / totalDeposited - 1);

  setField('balance', numberFormat(balance.amountQuoteTotal));
  setField('cumulative', numberFormat(cumulative));
  setField('gain', numberFormat(gain));
  setField('gain-percent', numberFormat(gainPercent));
}

setInterval(refreshBalance, POLL_INTERVAL_MS);