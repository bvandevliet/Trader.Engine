import { csrfHeaders } from './shared/csrf';
import { numberFormat, quoteSymbolFor } from './shared/format';

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

const configInitEl = document.getElementById('config-init');

// The full ConfigReqDto as last saved — the visible form only edits a subset of its fields
// (tag/weighting overrides live on a separate page), so every simulate/rebalance call starts
// from this and overlays just the fields the form actually shows.
const configTemplate: Record<string, unknown> = configInitEl?.textContent
  ? JSON.parse(configInitEl.textContent)
  : {};

// The page never simulates server-side (that call runs the full market-cap ranking + rebalance
// calculation, too slow to block the initial render on) — the table starts empty and this fetches
// the first simulation the same way every later resimulation on input change does.
let lastSimulation: SimulationDto | null = null;

const form = document.getElementById('rebalance-form') as HTMLFormElement;
const rebalanceNowBtn = document.getElementById('rebalance-now-btn') as HTMLButtonElement;
const expectedFeeEl = document.getElementById('expected-fee')!;
const lastRebalanceEl = document.getElementById('last-rebalance')!;
const trackingErrorEl = document.getElementById('tracking-error-quote')!;
const portfolioTableBody = document.querySelector('#portfolio-table tbody')!;
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
      addCell(row, `${sign(quoteDiff)}${numberFormat(quoteDiff)}`, diffClass(quoteDiff));
      addCell(row, `${sign(allocDiff)}${numberFormat(allocDiff)}`, diffClass(allocDiff));

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

async function simulate (): Promise<void>
{
  rebalanceNowBtn.disabled = true;
  rebalanceNowBtn.classList.add('loading');
  errorEl.classList.add('d-none');

  const response = await fetch('/rebalance/simulate', {
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

    if (!lastSimulation) { portfolioTableBody.replaceChildren(); }

    return;
  }

  lastSimulation = await response.json();

  quoteSymbolEls.forEach(el => (el.textContent = quoteSymbolFor(lastSimulation!.curBalance.quoteSymbol)));
  renderPortfolioTable(lastSimulation!.curBalance, lastSimulation!.newBalance);
  expectedFeeEl.textContent = numberFormat(lastSimulation!.totalFee);
  updateTrackingErrorQuote();

  rebalanceNowBtn.disabled = false;
  rebalanceNowBtn.classList.remove('loading');
}

let debounceHandle: ReturnType<typeof setTimeout> | undefined;

form.querySelectorAll<HTMLInputElement>('.config-input').forEach(input =>
{
  input.addEventListener('input', () =>
  {
    updateTrackingErrorQuote();

    if (input.dataset.noResim !== undefined) { return; }

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

    try
    {
      const response = await fetch('/rebalance/rebalancenow', {
        method: 'POST',
        headers: csrfHeaders(),
        body: JSON.stringify({
          config: buildConfigFromForm(),
          newAbsAllocs: lastSimulation.newAbsAllocs,
        }),
      });

      if (!response.ok) { return; }

      const balanceResponse = await fetch('/dashboard/currentbalance');
      const curBalance: BalanceDto = await balanceResponse.json();

      renderPortfolioTable(curBalance, lastSimulation.newBalance);
      lastRebalanceEl.textContent = 'Just now';
      expectedFeeEl.textContent = numberFormat(0);
    }
    finally
    {
      rebalanceNowBtn.classList.remove('loading');
      rebalanceNowBtn.disabled = false;
    }
  })();
});

updateTrackingErrorQuote();
simulate();