import { csrfHeaders } from './shared/csrf';
import { numberFormat, quoteSymbolFor } from './shared/format';
import { attachLoadingOverlay } from './shared/loading-overlay';

interface MarketDto {
  quoteSymbol: string;
  baseSymbol: string;
}

interface AllocationDto {
  market: MarketDto;
  price: number;
  amount: number;
  amountQuote: number;
}

interface BalanceDto {
  quoteSymbol: string;
  allocations: AllocationDto[];
  amountQuoteAvailable: number;
  amountQuoteTotal: number;
}

interface AbsAllocDto {
  market: MarketDto;
  absAlloc: number;
  marketStatus: string;
}

interface OrderDto {
  status: string;
}

interface SimulationDto {
  config: Record<string, unknown>;
  orders: OrderDto[];
  totalFee: number;
  curBalance: BalanceDto;
  newBalance: BalanceDto;
  newAbsAllocs: AbsAllocDto[];
}

interface InitDto {
  totalDeposited: number;
  totalWithdrawn: number;
  simulation: SimulationDto;
}

const POLL_INTERVAL_MS = 5000;

const configInitEl = document.getElementById('config-init');

// The full ConfigReqDto as last saved — the visible form only edits a subset of its fields
// (tag/weighting overrides live on a separate page), so every simulate/rebalance call starts
// from this and overlays just the fields the form actually shows.
const configTemplate: Record<string, unknown> = configInitEl?.textContent
  ? JSON.parse(configInitEl.textContent)
  : {};

let lastSimulation: SimulationDto | null = null;

// Kept from the initial /dashboard/init fetch so the balance poll loop (which only re-fetches
// the current balance, not the totals) can keep recomputing cumulative/gain figures from it.
let totalDeposited = 0;
let totalWithdrawn = 0;

const form = document.getElementById('rebalance-form') as HTMLFormElement;
const rebalanceNowBtn = document.getElementById('rebalance-now-btn') as HTMLButtonElement;
const expectedFeeEl = document.getElementById('expected-fee')!;
const lastRebalanceEl = document.getElementById('last-rebalance')!;
const trackingErrorEl = document.getElementById('tracking-error-quote')!;
const portfolioTableBody = document.querySelector('#portfolio-table tbody')!;
const portfolioTableOverlay = attachLoadingOverlay(document.getElementById('portfolio-table-wrapper')!);
const balanceSummaryOverlay = attachLoadingOverlay(document.getElementById('balance-summary-wrapper')!);
const quoteSymbolEls = document.querySelectorAll<HTMLElement>('.quote-symbol');
const errorEl = document.getElementById('rebalance-error')!;

function buildConfigFromForm (): Record<string, unknown>
{
  const config = { ...configTemplate };

  form.querySelectorAll<HTMLInputElement>('[data-config-field]').forEach(input =>
  {
    const field = input.dataset.configField!;

    if (input.type === 'checkbox')
    {
      config[field] = input.checked;
    }
    else if (input.type === 'number')
    {
      config[field] = input.value === '' ? 0 : parseFloat(input.value);
    }
    else
    {
      config[field] = input.value;
    }
  });

  return config;
}

function addCell (row: HTMLTableRowElement, text: string, className?: string): void
{
  const cell = document.createElement('td');

  cell.textContent = text;

  if (className) { cell.className = className; }

  row.appendChild(cell);
}

function renderPortfolioTable (curBalance: BalanceDto, newBalance: BalanceDto): void
{
  const curByAsset = new Map(curBalance.allocations.map(alloc => [alloc.market.baseSymbol, alloc]));
  const balByAsset = new Map(newBalance.allocations.map(alloc => [alloc.market.baseSymbol, alloc]));
  // Without sorting, preserve API order.
  const baseSymbols = Array.from(new Set([...curByAsset.keys(), ...balByAsset.keys()]));

  portfolioTableBody.replaceChildren(
    ...baseSymbols.map(baseSymbol =>
    {
      const curValue = curByAsset.get(baseSymbol)?.amountQuote ?? 0;
      const balValue = balByAsset.get(baseSymbol)?.amountQuote ?? 0;
      const curAlloc = curBalance.amountQuoteTotal === 0 ? 0 : (100 * curValue) / curBalance.amountQuoteTotal;
      const balAlloc = newBalance.amountQuoteTotal === 0 ? 0 : (100 * balValue) / newBalance.amountQuoteTotal;
      const quoteDiff = balValue - curValue;
      const allocDiff = balAlloc - curAlloc;
      // eslint-disable-next-line func-style
      const diffClass = (n: number) => (n >= 0 ? 'text-end text-success' : 'text-end text-danger');
      // eslint-disable-next-line func-style
      const sign = (n: number) => (n >= 0 ? '+' : '');

      const row = document.createElement('tr');

      addCell(row, baseSymbol);
      addCell(row, numberFormat(curValue), 'text-end');
      addCell(row, numberFormat(curAlloc), 'text-end');
      addCell(row, numberFormat(balValue), 'text-end');
      addCell(row, numberFormat(balAlloc), 'text-end');
      addCell(row, quoteDiff === 0 ? '' : `${sign(quoteDiff)}${numberFormat(quoteDiff)}`, diffClass(quoteDiff));
      addCell(row, quoteDiff === 0 ? '' : `${sign(allocDiff)}${numberFormat(allocDiff)}`, diffClass(allocDiff));

      return row;
    }),
  );
}

function updateTrackingErrorQuote (): void
{
  if (!lastSimulation) { return; }

  const minimumDiffAllocationInput = form.querySelector<HTMLInputElement>('[data-config-field=\'minimumDiffAllocation\']')!;
  const minimumDiffAllocation = parseFloat(minimumDiffAllocationInput.value || '0');
  const quote = (minimumDiffAllocation / 100) * lastSimulation.curBalance.amountQuoteTotal;

  trackingErrorEl.textContent = numberFormat(quote);
}

// Shared by init() and simulate() — both end up rendering a freshly (re)fetched simulation the
// same way, just via different endpoints.
function applySimulation (simulation: SimulationDto): void
{
  lastSimulation = simulation;

  renderPortfolioTable(simulation.curBalance, simulation.newBalance);
  expectedFeeEl.textContent = numberFormat(simulation.totalFee);
  updateTrackingErrorQuote();
}

function setBalanceField (field: string, value: string): void
{
  const cell = document.querySelector<HTMLTableCellElement>(`[data-field='${field}']`);

  if (cell) { cell.textContent = value; }
}

function renderBalanceSummary (balance: BalanceDto): void
{
  const cumulative = balance.amountQuoteTotal + totalWithdrawn;
  const gain = cumulative - totalDeposited;
  const gainPercent = totalDeposited === 0 ? 0 : 100 * (cumulative / totalDeposited - 1);

  setBalanceField('balance', numberFormat(balance.amountQuoteTotal));
  setBalanceField('cumulative', numberFormat(cumulative));
  setBalanceField('gain', numberFormat(gain));
  setBalanceField('gain-percent', numberFormat(gainPercent));
}

// Polled independently of rebalance simulation — the balance can move (deposits, price action)
// without any rebalance parameter changing, so this keeps ticking regardless of form activity.
async function refreshBalance (): Promise<void>
{
  const response = await fetch('/dashboard/currentbalance');

  if (!response.ok) { return; }

  renderBalanceSummary(await response.json());
}

let pollTimeoutHandle: ReturnType<typeof setTimeout> | undefined;

// Reschedules itself only once the previous poll has settled, rather than a fixed setInterval —
// otherwise a slow or hung response could pile up overlapping requests, or a rejected fetch (e.g.
// network drop) would silently kill all future polling since a thrown/rejected setInterval
// callback never gets a "try again" chance.
function pollBalanceLoop (): void
{
  refreshBalance()
    .catch(reason => console.error(reason))
    .finally(() => { pollTimeoutHandle = setTimeout(pollBalanceLoop, POLL_INTERVAL_MS); });
}

// No point polling an exchange balance nobody's looking at — pause while the tab is hidden/backgrounded,
// and refresh immediately (rather than waiting out a stale interval) the moment it becomes visible again.
document.addEventListener('visibilitychange', () =>
{
  clearTimeout(pollTimeoutHandle);

  if (!document.hidden) { pollBalanceLoop(); }
});

// The page never simulates server-side (that call runs the full market-cap ranking + rebalance
// calculation, too slow to block the initial render on) — both tables start empty/placeholder and
// this fetches everything they need in one round-trip, the same way every later resimulation on
// input change fetches just the (much cheaper) simulation via simulate().
async function init (): Promise<void>
{
  rebalanceNowBtn.disabled = true;
  rebalanceNowBtn.classList.add('loading');
  portfolioTableOverlay.show();
  balanceSummaryOverlay.show();
  errorEl.classList.add('d-none');

  try
  {
    const response = await fetch('/dashboard/init', {
      method: 'POST',
      headers: csrfHeaders(),
      body: JSON.stringify(buildConfigFromForm()),
    });

    if (!response.ok)
    {
      const body = await response.json().catch(() => null);

      errorEl.textContent = body?.error ?? 'Could not load the dashboard.';
      errorEl.classList.remove('d-none');

      return;
    }

    const init: InitDto = await response.json();

    totalDeposited = init.totalDeposited;
    totalWithdrawn = init.totalWithdrawn;

    quoteSymbolEls.forEach(el => (el.textContent = quoteSymbolFor(init.simulation.curBalance.quoteSymbol)));
    setBalanceField('deposited', numberFormat(init.totalDeposited));
    setBalanceField('withdrawn', numberFormat(init.totalWithdrawn));
    renderBalanceSummary(init.simulation.curBalance);
    applySimulation(init.simulation);

    rebalanceNowBtn.disabled = false;
  }
  finally
  {
    rebalanceNowBtn.classList.remove('loading');
    portfolioTableOverlay.hide();
    balanceSummaryOverlay.hide();
  }
}

// Re-simulates only — the balance summary card doesn't depend on rebalance config, so unlike
// init() this never touches it or its overlay.
async function simulate (): Promise<void>
{
  rebalanceNowBtn.disabled = true;
  rebalanceNowBtn.classList.add('loading');
  errorEl.classList.add('d-none');

  const response = await fetch('/dashboard/simulate', {
    method: 'POST',
    headers: csrfHeaders(),
    body: JSON.stringify(buildConfigFromForm()),
  });

  if (!response.ok)
  {
    const body = await response.json().catch(() => null);

    errorEl.textContent = body?.error ?? 'Could not simulate a rebalance.';
    errorEl.classList.remove('d-none');
    rebalanceNowBtn.classList.remove('loading');
    portfolioTableOverlay.hide();

    if (!lastSimulation) { portfolioTableBody.replaceChildren(); }

    return;
  }

  applySimulation(await response.json());

  rebalanceNowBtn.disabled = false;
  rebalanceNowBtn.classList.remove('loading');
  portfolioTableOverlay.hide();
}

let debounceHandle: ReturnType<typeof setTimeout> | undefined;

form.querySelectorAll<HTMLInputElement>('.config-input').forEach(input =>
{
  input.addEventListener('input', () =>
  {
    updateTrackingErrorQuote();

    if (input.dataset.noResim !== undefined) { return; }

    portfolioTableOverlay.show();

    clearTimeout(debounceHandle);
    debounceHandle = setTimeout(simulate, 1000);
  });
});

rebalanceNowBtn.addEventListener('click', () =>
{
  (async () =>
  {
    if (!lastSimulation) { return; }

    if (!confirm('This will perform a portfolio rebalance.\nAre you sure?')) { return; }

    if (
      lastSimulation.newAbsAllocs.length === 0
      && !confirm('Allowing the rebalance will result in a portfolio with zero assets.\nAre you sure?')
    )
    {
      return;
    }

    rebalanceNowBtn.disabled = true;
    rebalanceNowBtn.classList.add('loading');
    portfolioTableOverlay.show();

    let succeeded = false;

    try
    {
      const response = await fetch('/dashboard/rebalancenow', {
        method: 'POST',
        headers: csrfHeaders(),
        body: JSON.stringify({
          config: buildConfigFromForm(),
          newAbsAllocs: lastSimulation.newAbsAllocs,
        }),
      });

      if (!response.ok) { return; }

      // The response already carries the post-trade balance (see OnPostRebalanceNowAsync) — no
      // separate /dashboard/currentbalance round-trip needed to refresh these two.
      const { currentBalance }: { currentBalance: BalanceDto } = await response.json();

      renderPortfolioTable(currentBalance, lastSimulation.newBalance);
      renderBalanceSummary(currentBalance);
      lastRebalanceEl.textContent = 'Just now';
      expectedFeeEl.textContent = numberFormat(0);
      succeeded = true;
    }
    finally
    {
      rebalanceNowBtn.classList.remove('loading');
      portfolioTableOverlay.hide();

      // Deliberately left disabled after a successful rebalance — lastSimulation.newAbsAllocs
      // now describes trades that were just executed, so clicking again immediately would replay
      // a stale target. Only a fresh resimulate (the next config input change) re-enables it. On
      // failure nothing changed, so it's safe to let the user retry right away.
      if (!succeeded) { rebalanceNowBtn.disabled = false; }
    }
  })();
});

updateTrackingErrorQuote();
init().then(() => setTimeout(pollBalanceLoop, POLL_INTERVAL_MS));