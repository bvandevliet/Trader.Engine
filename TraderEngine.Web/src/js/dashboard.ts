import { Tooltip } from 'bootstrap';
import { ApiError, postJson } from './shared/api';
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

interface TargetAllocDto {
  market: MarketDto;
  targetWeight: number;
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
  targetAllocs: TargetAllocDto[];
}

// Base symbol -> CoinMarketCap display name (e.g. "BTC" -> "Bitcoin"), for the portfolio table's
// info tooltips. Absent entries (symbol with no recent market cap record) simply get no tooltip.
type AssetNames = Record<string, string>;

interface InitDto {
  totalDeposited: number;
  totalWithdrawn: number;
  // Null when the simulation leg failed independently of the totals (e.g. market cap data isn't
  // ingested yet) — see OnPostInitAsync's per-leg error handling.
  simulation: SimulationDto | null;
  simulationError: string | null;
  assetNames: AssetNames;
}

interface SimulateResponseDto {
  simulation: SimulationDto;
  assetNames: AssetNames;
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
let lastAssetNames: AssetNames = {};

// Kept from the initial /dashboard/init fetch so the balance poll loop (which only re-fetches
// the current balance, not the totals) can keep recomputing cumulative/gain figures from it.
let totalDeposited = 0;
let totalWithdrawn = 0;

const form = document.getElementById('rebalance-form') as HTMLFormElement;
const saveBtn = form.querySelector<HTMLButtonElement>('button[type=\'submit\']')!;
const rebalanceNowBtn = document.getElementById('rebalance-now-btn') as HTMLButtonElement;
const expectedFeeEl = document.getElementById('expected-fee')!;
const lastRebalanceEl = document.getElementById('last-rebalance')!;
const driftThresholdEl = document.getElementById('drift-threshold-quote')!;
const portfolioTableBody = document.querySelector('#portfolio-table tbody')!;
const portfolioTableOverlay = attachLoadingOverlay(document.getElementById('portfolio-table-wrapper')!);
const balanceSummaryOverlay = attachLoadingOverlay(document.getElementById('balance-summary-wrapper')!);
const quoteSymbolEls = document.querySelectorAll<HTMLElement>('.quote-symbol');

// Scoped to whichever card the failing/succeeding action belongs to, rather than one page-level
// banner: paramsError/paramsNotice sit in the "Rebalance parameters" card (Save, Rebalance now —
// both triggered by buttons in that card), portfolioError sits in the "Current vs. balanced
// allocation" card (init/simulate, which only ever affect that table).
const paramsErrorEl = document.getElementById('params-error')!;
const paramsNoticeEl = document.getElementById('params-notice')!;
const portfolioErrorEl = document.getElementById('portfolio-error')!;

// Deliberately not Bootstrap's built-in data-bs-dismiss — that plugin removes the alert element
// from the DOM entirely once its fade-out finishes, which is fine for _Layout.cshtml's one-shot
// TempData alerts (rendered once, never touched again) but not for these: they're long-lived
// elements this script keeps reusing across every AJAX call, so a user dismissing one would make
// it vanish permanently instead of just hiding until the next message.
function showAlert (alertEl: HTMLElement, message: string): void
{
  alertEl.querySelector('span')!.textContent = message;
  alertEl.classList.add('show');
  alertEl.classList.remove('d-none');
}

function hideAlert (alertEl: HTMLElement): void
{
  alertEl.classList.add('d-none');
  alertEl.classList.remove('show');
  alertEl.querySelector('span')!.textContent = null;
}

[paramsErrorEl, paramsNoticeEl, portfolioErrorEl].forEach(alertEl =>
{
  alertEl.querySelector('.btn-close')!.addEventListener('click', () => hideAlert(alertEl));
});

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

// Mirrors the (i) icon markup InfoTooltipTagHelper renders server-side for static config fields
// (see TagHelpers/InfoTooltipTagHelper.cs) — same classes/attributes, just built client-side since
// the asset name behind it is only known once the simulation response comes back.
function buildNameTooltip (name: string): HTMLElement
{
  const icon = document.createElement('i');

  icon.className = 'bi bi-info-circle text-muted ms-1';
  icon.setAttribute('data-bs-toggle', 'tooltip');
  icon.setAttribute('tabindex', '0');
  icon.title = name;

  // site.ts only wires up tooltips present at initial page load; icons created afterwards (every
  // portfolio table render) need their own Tooltip instance to become interactive.
  // eslint-disable-next-line no-new
  new Tooltip(icon);

  return icon;
}

function renderPortfolioTable (curBalance: BalanceDto, newBalance: BalanceDto, assetNames: AssetNames): void
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
      const quoteDrift = balValue - curValue;
      const allocDrift = balAlloc - curAlloc;
      // eslint-disable-next-line func-style
      const diffClass = (n: number) => (n >= 0 ? 'text-end text-success' : 'text-end text-danger');
      // eslint-disable-next-line func-style
      const sign = (n: number) => (n >= 0 ? '+' : '');

      const row = document.createElement('tr');
      const assetCell = document.createElement('td');

      assetCell.append(baseSymbol);

      const name = assetNames[baseSymbol];

      if (name) { assetCell.append(buildNameTooltip(name)); }

      row.appendChild(assetCell);

      addCell(row, numberFormat(curValue), 'text-end');
      addCell(row, numberFormat(curAlloc), 'text-end');
      addCell(row, numberFormat(balValue), 'text-end');
      addCell(row, numberFormat(balAlloc), 'text-end');
      addCell(row, quoteDrift === 0 ? '' : `${sign(quoteDrift)}${numberFormat(quoteDrift)}`, diffClass(quoteDrift));
      addCell(row, quoteDrift === 0 ? '' : `${sign(allocDrift)}${numberFormat(allocDrift)}`, diffClass(allocDrift));

      return row;
    }),
  );
}

function updateDriftThresholdQuote (): void
{
  if (!lastSimulation) { return; }

  const driftThresholdPercentInput = form.querySelector<HTMLInputElement>('[data-config-field=\'driftThresholdPercent\']')!;
  const driftThresholdPercent = parseFloat(driftThresholdPercentInput.value || '0');
  const quote = (driftThresholdPercent / 100) * lastSimulation.curBalance.amountQuoteTotal;

  driftThresholdEl.textContent = numberFormat(quote);
}

// Shared by init() and simulate() — both end up rendering a freshly (re)fetched simulation the
// same way, just via different endpoints.
function applySimulation (simulation: SimulationDto, assetNames: AssetNames): void
{
  lastSimulation = simulation;
  lastAssetNames = assetNames;

  renderPortfolioTable(simulation.curBalance, simulation.newBalance, assetNames);
  expectedFeeEl.textContent = numberFormat(simulation.totalFee);
  updateDriftThresholdQuote();
}

// Shared by every handler that calls postJson() — ApiError carries the server's own message
// (see Dashboard.cshtml.cs), anything else (network failure, unexpected response shape) falls
// back to a generic per-call message instead of leaking a raw exception to the user.
function showError (alertEl: HTMLElement, err: unknown, fallback: string): void
{
  showAlert(alertEl, err instanceof ApiError ? err.message : fallback);
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
  const gainPercent = totalDeposited === 0 ? 0 : 100 * ((cumulative / totalDeposited) - 1);

  // Set here rather than only once in init() — refreshBalance() also needs it, since init() may
  // finish without ever receiving a curBalance (see the null-simulation branch below).
  quoteSymbolEls.forEach(el => (el.textContent = quoteSymbolFor(balance.quoteSymbol)));
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
  hideAlert(portfolioErrorEl);

  try
  {
    const initData = await postJson<InitDto>('/dashboard/init', buildConfigFromForm());

    ({ totalDeposited, totalWithdrawn } = initData);

    setBalanceField('deposited', numberFormat(initData.totalDeposited));
    setBalanceField('withdrawn', numberFormat(initData.totalWithdrawn));

    if (initData.simulation)
    {
      renderBalanceSummary(initData.simulation.curBalance);
      applySimulation(initData.simulation, initData.assetNames);

      rebalanceNowBtn.disabled = false;
    }
    else
    {
      // Totals loaded fine but the simulation leg failed on its own (e.g. market cap data isn't
      // ingested yet) — surface that in the portfolio card without hiding the totals above, and
      // fall back to the plain balance endpoint so the summary card and quote symbol still populate.
      showAlert(portfolioErrorEl, initData.simulationError ?? 'Could not load rebalance simulation.');
      await refreshBalance();
    }
  }
  catch (err)
  {
    showError(portfolioErrorEl, err, 'Could not load the dashboard.');
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
  hideAlert(portfolioErrorEl);

  try
  {
    const { simulation, assetNames } = await postJson<SimulateResponseDto>('/dashboard/simulate', buildConfigFromForm());

    applySimulation(simulation, assetNames);

    rebalanceNowBtn.disabled = false;
  }
  catch (err)
  {
    showError(portfolioErrorEl, err, 'Could not simulate a rebalance.');

    if (!lastSimulation) { portfolioTableBody.replaceChildren(); }
  }
  finally
  {
    rebalanceNowBtn.classList.remove('loading');
    portfolioTableOverlay.hide();
  }
}

let noticeFadeHandle: ReturnType<typeof setTimeout> | undefined;

async function saveConfig (): Promise<void>
{
  saveBtn.disabled = true;
  saveBtn.classList.add('loading');
  hideAlert(paramsErrorEl);
  clearTimeout(noticeFadeHandle);
  hideAlert(paramsNoticeEl);

  try
  {
    await postJson('/dashboard/save', buildConfigFromForm());

    showAlert(paramsNoticeEl, 'Configuration updated.');

    // Clears it after a while so it doesn't linger indefinitely if the user doesn't dismiss it
    // themselves via the alert's own close button.
    noticeFadeHandle = setTimeout(() => hideAlert(paramsNoticeEl), 5000);
  }
  catch (err)
  {
    showError(paramsErrorEl, err, 'Could not save the configuration.');
  }
  finally
  {
    saveBtn.disabled = false;
    saveBtn.classList.remove('loading');
  }
}

form.addEventListener('submit', e =>
{
  e.preventDefault();

  saveConfig();
});

let debounceHandle: ReturnType<typeof setTimeout> | undefined;

form.querySelectorAll<HTMLInputElement>('.config-input').forEach(input =>
{
  input.addEventListener('input', () =>
  {
    updateDriftThresholdQuote();

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
      lastSimulation.targetAllocs.length === 0
      && !confirm('Allowing the rebalance will result in a portfolio with zero assets.\nAre you sure?')
    )
    {
      return;
    }

    rebalanceNowBtn.disabled = true;
    rebalanceNowBtn.classList.add('loading');
    portfolioTableOverlay.show();
    hideAlert(paramsErrorEl);

    let succeeded = false;

    try
    {
      // The response already carries the post-trade balance (see OnPostRebalanceNowAsync) — no
      // separate /dashboard/currentbalance round-trip needed to refresh these two.
      const { currentBalance } = await postJson<{ currentBalance: BalanceDto }>('/dashboard/rebalancenow', {
        config: buildConfigFromForm(),
        targetAllocs: lastSimulation.targetAllocs,
      });

      renderPortfolioTable(currentBalance, lastSimulation.newBalance, lastAssetNames);
      renderBalanceSummary(currentBalance);
      lastRebalanceEl.textContent = 'Just now';
      expectedFeeEl.textContent = numberFormat(0);
      succeeded = true;
    }
    catch (err)
    {
      showError(paramsErrorEl, err, 'Could not perform the rebalance.');
    }
    finally
    {
      rebalanceNowBtn.classList.remove('loading');
      portfolioTableOverlay.hide();

      // Deliberately left disabled after a successful rebalance — lastSimulation.targetAllocs
      // now describes trades that were just executed, so clicking again immediately would replay
      // a stale target. Only a fresh resimulate (the next config input change) re-enables it. On
      // failure nothing changed, so it's safe to let the user retry right away.
      if (!succeeded) { rebalanceNowBtn.disabled = false; }
    }
  })();
});

updateDriftThresholdQuote();
init().then(() => setTimeout(pollBalanceLoop, POLL_INTERVAL_MS));