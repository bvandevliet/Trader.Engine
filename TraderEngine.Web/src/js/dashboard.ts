import { numberFormat, quoteSymbolFor } from './shared/format';
import { attachLoadingOverlay } from './shared/loading-overlay';

interface BalanceDto {
  quoteSymbol: string;
  amountQuoteAvailable: number;
  amountQuoteTotal: number;
}

interface SummaryDto {
  quoteSymbol: string;
  totalDeposited: number;
  totalWithdrawn: number;
  currentBalance: BalanceDto;
}

const POLL_INTERVAL_MS = 5000;

const balanceSummaryOverlay = attachLoadingOverlay(document.getElementById('balance-summary-wrapper')!);
const quoteSymbolEls = document.querySelectorAll<HTMLElement>('.quote-symbol');

// Populated from the initial /dashboard/summary fetch, then kept up to date so the poll loop
// (which only re-fetches the balance) can keep recomputing cumulative/gain figures from it.
let totalDeposited = 0;
let totalWithdrawn = 0;

function setField (field: string, value: string): void
{
  const cell = document.querySelector<HTMLTableCellElement>(`[data-field='${field}']`);

  if (cell) { cell.textContent = value; }
}

function renderBalance (balance: BalanceDto): void
{
  const cumulative = balance.amountQuoteTotal + totalWithdrawn;
  const gain = cumulative - totalDeposited;
  const gainPercent = totalDeposited === 0 ? 0 : 100 * (cumulative / totalDeposited - 1);

  setField('balance', numberFormat(balance.amountQuoteTotal));
  setField('cumulative', numberFormat(cumulative));
  setField('gain', numberFormat(gain));
  setField('gain-percent', numberFormat(gainPercent));
}

async function refreshBalance (): Promise<void>
{
  const response = await fetch('/dashboard/currentbalance');

  if (!response.ok) { return; }

  renderBalance(await response.json());
}

async function loadSummary (): Promise<void>
{
  balanceSummaryOverlay.show();

  try
  {
    const response = await fetch('/dashboard/summary');

    if (!response.ok) { return; }

    const summary: SummaryDto = await response.json();

    totalDeposited = summary.totalDeposited;
    totalWithdrawn = summary.totalWithdrawn;

    quoteSymbolEls.forEach(el => (el.textContent = quoteSymbolFor(summary.quoteSymbol)));
    setField('deposited', numberFormat(summary.totalDeposited));
    setField('withdrawn', numberFormat(summary.totalWithdrawn));
    renderBalance(summary.currentBalance);
  }
  finally
  {
    balanceSummaryOverlay.hide();
  }
}

loadSummary().then(() => setInterval(refreshBalance, POLL_INTERVAL_MS));