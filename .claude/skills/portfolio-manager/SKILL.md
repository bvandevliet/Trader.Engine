---
name: portfolio-manager
description: This skill should be used for ALL work in this automated cryptocurrency portfolio management and rebalancing application. Load whenever the user discusses "portfolio review", "position analysis", "risk assessment", "rebalancing", "asset allocation", "diversification", "portfolio drift", "concentration risk", "position sizing", "target allocation", "risk metrics", "Sharpe ratio", "beta", "drawdown", "HHI", "HOLD/ADD/TRIM/SELL", "tax-loss harvesting", "Bitcoin", "BTC", "Ethereum", "ETH", "altcoins", "stablecoins", "BTC dominance", "crypto allocation", "market cycle", "crypto volatility", or any portfolio management feature, calculation, algorithm, or recommendation in this codebase.
---

# Portfolio Manager

Expert portfolio management knowledge for an automated portfolio management and rebalancing application. This skill provides the analytical framework, decision logic, and domain knowledge that should inform every portfolio-related feature, calculation, and recommendation in this codebase.

## Core Philosophy

**Goal:** Maintain portfolio alignment with investor risk profile through systematic, disciplined, and tax-efficient rebalancing.

**Principles:**
- Asset allocation is the primary driver of long-term returns; get the big picture right first
- Risk management is the primary objective; return enhancement is secondary
- Systematic discipline (not emotion) drives all portfolio decisions; this should be reflected in all automation logic
- Rebalancing enforces "buy low, sell high" mechanically; match this in code
- Always match allocation to investor risk tolerance before optimizing within constraints

## Crypto Asset Context

This application manages **cryptocurrency portfolios**. The general portfolio management frameworks apply, but all thresholds, benchmarks, and risk expectations must be adjusted for crypto-specific characteristics:

- **Volatility is 3–5x higher than equities:** BTC ~60% annualized, ETH ~90%, altcoins ~100–200%+
- **Drawdowns of -50% to -90% are historical norms**, not exceptional events; bear markets for alts routinely reach -90%+
- **Stablecoins replace bonds:** USDC, USDT, DAI, etc. serve as the risk-off, capital-preservation asset class
- **BTC is the benchmark**, not the S&P 500; portfolio beta is measured relative to BTC
- **Intra-crypto correlation is very high**: adding more altcoins does not provide the same diversification benefit as cross-asset diversification in equities; to reduce risk, increase stablecoin allocation
- **24/7 markets**: drift happens continuously; monitoring must be continuous even if rebalancing is periodic
- **Every crypto-to-crypto trade is a taxable event**: rebalancing cost is much higher; use wider drift thresholds (10–15%) to minimize unnecessary tax drag
- **Market cycles** are driven by Bitcoin halving events (~4-year cycles); portfolio construction should reflect cycle phase

**Risk/reward is two-dimensional in crypto:**
- **Tier (market cap)**: sets maximum position size and liquidity/volatility floor (Tier 1 BTC → Tier 5 small-cap speculative)
- **Category (asset type)**: sets sector concentration limits and captures narrative/correlation risk (L1 blockchain, L2, DeFi, Infrastructure, AI, GameFi, RWA, Meme, Stablecoin)

Both apply to every position. A large-cap meme coin (Tier 3 by market cap) should be sized like Tier 4–5 because the category is inherently speculative. A Tier 4 RWA token may warrant larger sizing because it provides genuine diversification benefit.

**Tier quick reference:**
- Tier 1 (BTC): 40–50% max, portfolio anchor
- Tier 2 (Blue Chip: ETH, SOL): 20–30% max per asset
- Tier 3 (Large-cap, >$1B): 10–15% max per position
- Tier 4 (Mid-cap, $100M–$1B): 5–8% max per position
- Tier 5 (Small-cap, <$100M): 1–3% max per position

For full tier × category position sizing rules and category concentration limits, see `references/crypto-asset-context.md`.

## Application Context

This skill applies to every Claude Code execution in this repository. All code, logic, algorithms, and recommendations should align with the frameworks defined here and in the reference files.

**This skill applies to:**
- Portfolio drift detection and threshold logic
- Rebalancing calculation and trade generation
- Risk metric computation (beta, drawdown, Sharpe, HHI)
- Position evaluation (HOLD / ADD / TRIM / SELL decision logic)
- Asset allocation gap analysis and comparison
- Diversification quality assessment
- Risk profile determination and target allocation matching
- Tax-efficient execution ordering and lot selection

## Risk Profile Framework

Four investor profiles drive all allocation decisions in this application. For crypto portfolios, "bonds" maps to stablecoins, and drawdown expectations are recalibrated to crypto norms:

| Profile | BTC + ETH | Alts (Tier 3–5) | Stablecoins | Max Acceptable Drawdown | BTC Beta Target |
|---------|-----------|-----------------|-------------|------------------------|-----------------|
| **Conservative** | 50–60% | 0–10% | 30–50% | -30% | 0.5–0.7 |
| **Moderate** | 50–65% | 10–25% | 15–30% | -55% | 0.7–0.9 |
| **Growth** | 50–65% | 20–35% | 5–15% | -70% | 0.9–1.1 |
| **Aggressive** | 40–60% | 30–55% | 0–10% | -85% | 1.1–1.5 |

Determine risk profile using the three-factor scoring model (capacity, tolerance, requirement) from `references/risk-profile-questionnaire.md`. When scores conflict, apply the most conservative binding constraint: never exceed emotional risk tolerance regardless of financial capacity or return requirement.

**Critical:** In crypto, the stablecoin allocation is the primary risk-control lever. Increasing stablecoin allocation reduces drawdown exposure more effectively than rotating between crypto assets.

## Core Portfolio Management Workflow

### Step 1: Assess Investor Risk Profile

Before any analysis or rebalancing decision:
1. Score risk capacity (financial factors: time horizon, income stability, net worth percentage, emergency fund, debt)
2. Score risk tolerance (behavioral factors: loss reaction, historical behavior, sleep test, volatility preference)
3. Score risk requirement (goal factors: required return, savings rate, goal flexibility)
4. Assign profile: Conservative / Moderate / Growth / Aggressive
5. Load the corresponding target allocation from `references/target-allocations.md`

Risk profile must precede all recommendations. No allocation or rebalancing advice is meaningful without it.

### Step 2: Calculate Current Portfolio State

For each portfolio analysis, compute:
- Asset class percentages (stocks, bonds, cash, alternatives)
- Sector breakdown within equity allocation
- Geographic allocation (US, International Developed, Emerging Markets)
- Position-level sizes as percentage of total portfolio value
- HHI concentration score: `HHI = Σ(weight_i × 100)²`
- Portfolio beta: weighted average of individual position betas
- Current drawdown vs recent peak

### Step 3: Identify Rebalancing Needs

Compare current state to target allocation using crypto-adjusted drift thresholds (wider than equities because daily moves of 5–15% are normal in crypto):

| Drift Magnitude | Action Required |
|-----------------|-----------------|
| < 5% | Monitor only, no action |
| 5–10% | Rebalancing optional (low priority) |
| 10–20% | Rebalance recommended (medium priority) |
| > 20% | Rebalance immediately (high priority) |
| Any Tier 4–5 position > 15% | Review immediately |
| Stablecoin allocation drops > 10% below target | Rebalance (risk mandate breach) |

**Why wider thresholds:** Each rebalancing trade is a taxable event in crypto. A 5% threshold would trigger constant rebalancing given daily volatility, generating excessive tax drag with minimal risk benefit.

Default methodology: check weekly, rebalance only when drift exceeds 10–15%. See `references/rebalancing-strategies.md` for methodology options and `references/crypto-asset-context.md` for crypto-specific rebalancing considerations.

### Step 4: Evaluate Individual Positions

For positions requiring action, apply the four-factor model from `references/position-evaluation.md`:

1. **Thesis validation (40%)**: Is the investment thesis still intact?
2. **Valuation assessment (30%)**: Is the stock fairly valued vs historical range and peer group?
3. **Position sizing (20%)**: Does size match current conviction level?
4. **Relative opportunity (10%)**: Are there better uses of this capital?

| Decision | Criteria |
|----------|----------|
| **HOLD** | Thesis intact, fair valuation, size within target range |
| **ADD** | Thesis strengthening, undervalued, room to increase |
| **TRIM** | Thesis weakening OR overvalued OR oversized OR better alternatives |
| **SELL** | Thesis broken OR severely overvalued OR much better alternatives |

Never anchor to purchase price. Evaluate every position on current fundamentals and forward outlook only.

### Step 5: Generate Tax-Efficient Rebalancing Plan

When rebalancing trades are required:
- Prioritize execution in tax-advantaged accounts (IRA, 401k, Roth) before taxable accounts
- Identify tax-loss harvesting opportunities to offset realized gains from trims
- Select highest-cost-basis tax lots first when selling (minimize taxable gain)
- Target long-term capital gains treatment (hold > 1 year) when drift urgency permits
- Use new contributions to rebalance underweight positions where available (no tax impact)
- Calculate pre-execution tax impact: `tax_cost = gain × rate` before recommending any sell

### Step 6: Compute Portfolio Risk Metrics

For every portfolio review, calculate and interpret (with crypto-adjusted benchmarks):

- **BTC Beta:** Portfolio beta relative to BTC (not S&P 500); target range by risk profile in the Risk Profile Framework table above
- **Stablecoin Ratio:** % of portfolio in stablecoins vs target floor; primary risk-control metric
- **Altcoin Ratio:** % in Tier 3–5 alts vs target ceiling; higher = more aggressive
- **Standard Deviation:** Annualized; BTC baseline ~60% (not S&P 500's ~16%)
- **Maximum Drawdown:** Compare to investor's acceptable drawdown; -50% is normal in crypto, -80%+ is severe
- **Current Drawdown:** Flag if > 20% (correction), > 40% (bear market territory for crypto)
- **Sharpe Ratio:** Risk-free rate = stablecoin yield (currently ~4–6%); target > 1.0 (vs ~0.4 for S&P 500)
- **Sortino Ratio:** Preferred given crypto's asymmetric downside volatility
- **BTC Dominance:** External metric (BTC market cap / total crypto market cap); informs cycle phase and tier allocation

Full metric formulas and interpretation tables are in `references/portfolio-risk-metrics.md`. Crypto-specific benchmarks and interpretations are in `references/crypto-asset-context.md`.

**Performance snapshot:** Alongside risk metrics, surface a lightweight performance readout for the review period: top 3 and bottom 3 positions by return, portfolio return vs. BTC return over the same period, and win/loss ratio (count of positions positive vs. negative over the period). This is diagnostic context for Step 4 position evaluation, not a basis for rebalancing decisions on its own; a position underperforming BTC is not automatically a TRIM/SELL candidate if its thesis is still intact.

## Concentration Thresholds (Crypto-Adjusted)

Traditional equity concentration thresholds do not apply directly to crypto. BTC and ETH serve as portfolio anchors and may legitimately hold large allocations.

**BTC (Tier 1):**
- Up to 40–50% of total portfolio: Normal anchor allocation
- > 60%: Very conservative / effectively a BTC-only portfolio

**ETH (Tier 2):**
- Up to 20–30% of total portfolio: Normal blue-chip allocation
- > 35%: Elevated single-asset risk, review

**Tier 3 alts (individual positions):**
- Single position > 10%: Monitor closely
- Single position > 15%: Trim recommended
- Single position > 20%: Urgent trim required

**Tier 4–5 alts (individual positions):**
- Single position > 5%: Monitor closely
- Single position > 8%: Trim recommended
- Single position > 12%: Urgent trim required

**Stablecoin concentration:**
- Stablecoin allocation below target floor: Risk mandate breach, rebalance
- No single stablecoin > 50% of total stablecoin allocation (depeg risk)

**HHI (portfolio-level):**
- Crypto portfolios will naturally have higher HHI due to BTC anchor; HHI alone is less diagnostic than tier balance
- Focus instead on: (1) stablecoin % vs target, (2) Tier 3–5 % vs target, (3) BTC beta

## Diversification Quality Dimensions (Crypto)

Crypto diversification works differently from equities; intra-crypto correlation is very high. Assess across these dimensions:

1. **Tier balance:** Allocation distributed across Tier 1–3 per risk profile; avoid over-concentration in Tier 4–5
2. **Category balance:** Tier 3+ positions span at least 3 different categories (L1, DeFi, Infrastructure, etc.); no single category at or above its concentration limit
3. **Stablecoin floor:** Stablecoin allocation meets or exceeds the risk-profile minimum
4. **Single-asset concentration:** BTC/ETH within acceptable ranges; Tier 3+ positions sized by tier ceiling, adjusted down for speculative categories (Meme, GameFi → apply one tier lower's limits)
5. **BTC beta:** Portfolio beta to BTC within target range for risk profile

**Crypto diversification warning:** Holding 20 altcoins does not provide meaningful diversification; they are highly correlated to BTC and to each other. The primary diversification mechanism is **stablecoin allocation**, not altcoin count.

False diversification in crypto = many altcoins with near-identical BTC correlation. Flag and recommend stablecoin rebalancing instead.

**Pairwise correlation flagging:** When reviewing held positions, estimate pairwise return correlation among Tier 3+ holdings (proxy: same category plus similar BTC beta implies high correlation absent contrary data). If two held Tier 3+ positions in the same category both correlate > 0.85 to BTC, treat the smaller position as redundant for sizing purposes: it is not contributing diversification and should be a TRIM/consolidation candidate ahead of a similarly-sized position in an underrepresented category.

See `references/diversification-principles.md` for the full diversification framework and `references/crypto-asset-context.md` for crypto correlation characteristics.

## Rebalancing Trade Calculation

For each rebalancing trade:
```
target_value = total_portfolio_value × target_weight_pct
trade_amount = current_value − target_value  (positive = trim, negative = add)
shares = abs(trade_amount) / current_price
```

For partial rebalancing, apply 50% of the drift correction. For gradual rebalancing (large positions), split into weekly tranches.

## Reference Documentation

Load these files when implementing specific features or working through detailed scenarios:

| Reference | Purpose | Load When |
|-----------|---------|-----------|
| **`references/crypto-asset-context.md`** | Crypto taxonomy, volatility profiles, market cycles, BTC dominance, stablecoin role, crypto rebalancing, tax treatment, crypto-adjusted metrics | Any crypto-specific feature, threshold logic, or when general frameworks need crypto calibration; **load first** |
| **`references/risk-profile-questionnaire.md`** | Three-factor risk scoring, profile assignment, conflict resolution | Building onboarding, risk assessment, profile logic |
| **`references/target-allocations.md`** | Model portfolio structures by profile, position sizing limits, lifecycle rules | Generating targets, gap analysis, position size validation (adapt equity-specific values to crypto tiers) |
| **`references/asset-allocation.md`** | Asset class characteristics, tactical overlays, market cycle adjustments | Allocation modeling, cycle-aware adjustments (use BTC cycle context from crypto-asset-context.md alongside) |
| **`references/rebalancing-strategies.md`** | Full rebalancing methodologies, tax optimization, implementation patterns | Rebalancing engine, trade generation, tax-aware execution |
| **`references/portfolio-risk-metrics.md`** | Beta, std dev, drawdown, VaR, Sharpe/Sortino/Calmar formulas, composite risk scoring | Risk calculation modules, risk assessment reporting (use crypto benchmarks from crypto-asset-context.md) |
| **`references/diversification-principles.md`** | HHI calculation, correlation analysis, diversification framework | Diversification scoring, concentration alerts (crypto correlation context in crypto-asset-context.md) |
| **`references/position-evaluation.md`** | Thesis validation, valuation assessment, HOLD/ADD/TRIM/SELL decision matrix | Position-level review logic, recommendation generation |

## Critical Implementation Notes

**Rebalancing is risk management, not return enhancement.** Frame all rebalancing output in terms of maintaining target risk level, not improving returns.

**Drift monitoring must be continuous.** Unmonitored drift violates the investor's risk mandate. Build threshold breach alerting into the core monitoring loop.

**Tax impact must precede execution.** Always estimate tax cost before recommending a sell. Tax drag can eliminate rebalancing benefits for small drifts in taxable accounts; use the break-even analysis from `references/rebalancing-strategies.md`.

**Thesis state must be tracked per position.** The HOLD/ADD/TRIM/SELL framework requires knowing the original investment thesis. Design data models to store thesis, entry rationale, and thesis status per position.

**Use the right risk profile.** Never apply a generic allocation without confirming the investor's risk profile first. A conservative portfolio requires fundamentally different logic than an aggressive one across every decision point.

**Stablecoin allocation is the primary risk lever in crypto.** When portfolio risk needs to decrease, the default action is increasing stablecoin allocation, not rotating between crypto assets, which remain highly correlated. Build this into all risk-reduction logic.

**Traditional thresholds do not apply directly.** A 5% portfolio drift is noise in crypto (could happen in hours). A -20% drawdown is a minor correction, not a crisis. Always apply the crypto-adjusted thresholds defined in this skill and in `references/crypto-asset-context.md`.
