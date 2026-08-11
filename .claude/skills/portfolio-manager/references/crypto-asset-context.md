# Crypto Asset Context

This document covers the key ways that cryptocurrency portfolio management differs from traditional asset management. All frameworks in this skill apply, but thresholds, benchmarks, and risk expectations must be adjusted for crypto-specific characteristics.

## Two-Dimensional Risk/Reward Framework

Risk and reward in a crypto portfolio are best understood across **two independent dimensions**:

1. **Tier (market cap)**: determines position size limits, liquidity, and volatility floor
2. **Category (asset type)**: determines correlation clustering, narrative risk, and sector concentration limits

Both dimensions apply simultaneously to every position. A large-cap DeFi token and a large-cap L1 are both Tier 3, but they carry different category-level risks and should not both be maxed to the Tier 3 position limit.

---

## Dimension 1: Tiers (Market Cap Classification)

Tiers define the **maximum acceptable position size** and expected volatility range. Market cap is the primary proxy for liquidity and survivability risk.

| Tier | Market Cap | Examples | Max Single Position | Volatility |
|------|-----------|---------|---------------------|------------|
| **Tier 1 (Reserve)** | Unique (BTC only) | Bitcoin | 40–50% of portfolio | ~60% annualized |
| **Tier 2 (Blue Chip)** | Top 5–10, sustained >$50B | ETH, SOL | 20–30% per asset | ~80–100% annualized |
| **Tier 3 (Large-Cap)** | Market cap > $1B | Established L1s, major DeFi | 10–15% per position | ~100–150% annualized |
| **Tier 4 (Mid-Cap)** | Market cap $100M–$1B | Emerging protocols, smaller L1s | 5–8% per position | ~150–250% annualized |
| **Tier 5 (Small-Cap)** | Market cap < $100M | New launches, micro-caps | 1–3% per position | Uncapped downside |

**Tier principle:** Max position size shrinks as tier number increases. Most portfolio value should sit in Tier 1–2 (adjusted by risk profile).

---

## Dimension 2: Categories (Asset Type Classification)

Categories define **what the asset does**, which determines correlation clustering, narrative risk, and sector concentration limits. Two assets in the same tier but different categories carry different portfolio-level risks.

| Category | Description | Examples | BTC Correlation | Key Risks |
|----------|------------|---------|----------------|-----------|
| **L1 Blockchain** | Base-layer protocols | ETH, SOL, AVAX, ADA, DOT | High (0.7–0.9) | L1 competition, tech risk, fee revenue |
| **L2 / Scaling** | Ethereum scaling solutions | ARB, OP, POL, zkSync | High (ETH-correlated) | Ethereum-dependent, winner-takes-most |
| **DeFi** | Decentralized finance protocols | AAVE, UNI, MKR, CRV, GMX | High (ETH TVL-driven) | Smart contract risk, regulatory, hack risk |
| **Infrastructure / Oracle** | Middleware, data feeds | LINK, GRT, API3, PYTH | Moderate (0.6–0.8) | Adoption risk, competition |
| **Exchange Tokens** | CEX native tokens | BNB, OKB, CRO | Moderate | Exchange risk, regulatory, volume dependency |
| **AI / Data** | AI-driven protocols | FET/ASI, RNDR, TAO, WLD | Moderate (narrative-driven) | Narrative rotation, high speculation |
| **GameFi / Metaverse** | Gaming and virtual worlds | AXS, SAND, IMX | Moderate-High | Adoption-dependent, very narrative-driven |
| **RWA** | Real-world asset tokenization | ONDO, MKR (RWA portion) | Potentially lower | Regulatory, custody, adoption |
| **Meme / Sentiment** | Sentiment-driven, no fundamental floor | DOGE, SHIB, PEPE, WIF | Moderate | Pure sentiment, can go to zero |
| **Stablecoins** | Pegged to fiat | USDC, USDT, DAI | ~0 (by design) | Depeg, counterparty, smart contract |

**Category principle:** Avoid over-concentration in any single category regardless of tier. High narrative correlation within a category means a sector-level collapse can hit multiple positions simultaneously.

---

## Position Sizing: Tier × Category Interaction

The tier sets the **ceiling**; the category can lower it based on risk profile and concentration.

**Practical interaction rules:**

- A **Tier 3 L1** (e.g., AVAX) may warrant up to 10–12%, given established infrastructure and a real ecosystem
- A **Tier 3 Meme** (e.g., DOGE at >$1B market cap) should be treated as Tier 4–5 sizing (3–5%): market cap doesn't reflect the speculative nature
- A **Tier 4 DeFi** protocol has smart contract risk on top of market cap risk; position toward the lower end of the Tier 4 range (3–5%)
- A **Tier 4 RWA** token may warrant slightly more if it provides genuine lower correlation; use as a diversifier

**Rule of thumb:** If the category is inherently speculative (Meme, GameFi, AI narrative), apply one tier lower's position sizing limits regardless of market cap.

---

## Category Concentration Limits

Avoid excessive concentration in any single category, even across different tiers:

| Category | Max % of Total Portfolio |
|----------|------------------------|
| **L1 Blockchains** (ex-BTC/ETH) | 25–30% |
| **L2 / Scaling** | 15–20% |
| **DeFi** | 15–20% |
| **Infrastructure / Oracle** | 10–15% |
| **Exchange Tokens** | 10% |
| **AI / Data** | 10–15% |
| **GameFi / Metaverse** | 5–10% |
| **RWA** | 5–10% |
| **Meme / Sentiment** | 5% max (consider 0–3% for most profiles) |
| **Stablecoins** | Per risk profile (10–50%) |

**Category diversification goal:** At minimum, Tier 3+ positions should span at least 3 different categories. Avoid putting all altcoin allocation into a single narrative sector (e.g., 100% AI tokens).

## Stablecoins as the Bond Equivalent

Stablecoins (USDC, USDT, DAI, USDS, etc.) serve the role of bonds and cash in traditional portfolios:

- **Capital preservation** during bear markets
- **Liquidity** for opportunistic buying on drawdowns
- **Yield** via lending protocols (DeFi yield, CEX earn programs)
- **Volatility dampening**: stablecoin allocation reduces overall portfolio beta

**Key difference from bonds:** Stablecoins maintain constant nominal value but earn yield through deployment. They do not appreciate. Treat them as 0% growth, yield-generating cash equivalents.

**Stablecoin risk:** Not risk-free. Depeg risk (UST/LUNA 2022), smart contract risk (DeFi), counterparty risk (USDT). Diversify across stablecoins; limit any single stablecoin to <50% of stablecoin allocation.

## Volatility Profiles

Crypto is structurally more volatile than any traditional asset class:

| Asset | Approximate Annualized Volatility | Max Historical Drawdown |
|-------|---------------------------------|------------------------|
| S&P 500 | ~16% | -57% (2008) |
| Bitcoin (BTC) | ~55-70% | -83% (2017-2018), -77% (2021-2022) |
| Ethereum (ETH) | ~80-100% | -94% (2017-2018) |
| Altcoins (Tier 3) | ~100-150% | -90-98% typical in bear markets |
| Small-cap alts | ~200%+ | -99%+ possible (many go to zero) |

**Implication:** All risk thresholds in this application must be calibrated to crypto volatility, not equity volatility. A 10% daily move in BTC is noise; a 10% daily move in equities is a crash.

## Market Cycle Awareness

Crypto operates on recognizable market cycles, primarily driven by Bitcoin halving events (approximately every 4 years):

**Cycle Phases:**
1. **Accumulation (Post-bear bottom):** BTC dominance high, altcoins depressed, smart money accumulating
2. **BTC Bull Run:** BTC leads, dominance increases, alts lag or decline in BTC terms
3. **Alt Season:** BTC dominance drops, capital rotates to ETH then altcoins, highest returns but also highest risk
4. **Distribution / Top Formation:** Parabolic price action, euphoria, new retail inflows
5. **Bear Market:** Extended decline (-70-90%+), lasts 1-2 years, alts suffer most

**Portfolio implications:**
- Rebalancing toward stablecoins during distribution phases is risk management
- Altcoin allocation should reflect cycle phase: reduce in late-cycle, increase post-capitulation
- BTC dominance > 55% generally signals early/mid cycle; < 45% often signals late-cycle alt season

## BTC Dominance as a Portfolio Metric

BTC dominance (BTC market cap / total crypto market cap) is a key indicator with no traditional equivalent:

| BTC Dominance | Market Signal | Portfolio Implication |
|---------------|--------------|----------------------|
| **> 55%** | Early/mid bull or bear recovery | Favor BTC over alts; risk-off in crypto terms |
| **50-55%** | Mid cycle, balanced | Normal diversified allocation |
| **45-50%** | Alt season approaching | Selectively increase Tier 2-3 alts |
| **< 45%** | Peak alt season / late cycle | Reduce alt exposure, increase stables/BTC |
| **Rising rapidly** | Risk-off rotation | Reduce alts, increase BTC/stables |
| **Falling rapidly** | Risk-on rotation | Alts outperforming, rebalance may be needed |

## Correlation Characteristics

**The key difference from equity diversification:**

Within crypto, almost everything is highly correlated to BTC. During risk-off events, intra-crypto correlations approach 1.0; all assets fall together. This is structurally different from equity diversification where sectors and geographies diverge.

**Typical intra-crypto correlations:**
- ETH vs BTC: 0.75-0.90
- Large-cap alts vs BTC: 0.70-0.85
- Mid-cap alts vs BTC: 0.65-0.80
- Small-cap alts vs BTC: 0.50-0.75 (higher noise, lower signal)

**Implication:** Crypto "diversification" primarily means tier diversification and stablecoin allocation, not true cross-asset diversification. To reduce portfolio beta, increase stablecoin allocation, not by adding more altcoins.

**True diversification benefit:** Small allocations to assets with genuine low BTC correlation (some DeFi tokens, RWA tokens, specific utility tokens). Still modest benefit.

## Rebalancing in Crypto Context

**Key differences from traditional rebalancing:**

**1. Every rebalance is a taxable event (in most jurisdictions)**
- Crypto-to-crypto trades are taxable (unlike equity rebalancing in tax-advantaged accounts)
- No tax-advantaged crypto accounts equivalent to IRA/401k (at time of writing)
- Tax cost of rebalancing is significantly higher than in equities
- Implication: Use wider drift thresholds (10-15% vs 5% for equities) to minimize tax drag

**2. Wider drift thresholds are appropriate**
- Daily moves of 5-15% are normal in crypto
- A 5% drift threshold would trigger constant rebalancing in normal market conditions
- Recommended drift thresholds for crypto:
  - < 5%: Monitor only
  - 5-10%: Rebalancing optional
  - 10-20%: Rebalancing recommended
  - > 20%: Rebalance immediately
  - Single position > 25%: Review (not necessarily trim; BTC/ETH may warrant this)

**3. 24/7 markets**
- Price discovery is continuous; drift happens overnight, on weekends, during holidays
- Scheduled "quarterly review" still applies, but monitor continuously for threshold breaches
- Rebalancing can execute at any time; no market hours constraint

**4. Stablecoin rebalancing is preferred over cross-crypto swaps**
- Selling volatile asset → stablecoin is simpler tax lot tracking than crypto-to-crypto
- Rebalancing via stablecoin as intermediate: sell overweight asset → USDC → buy underweight asset
- May generate two taxable events but improves clarity

**5. On-chain vs CEX rebalancing**
- CEX rebalancing: faster, simpler, but custodial risk
- DEX/on-chain: self-custodied, transparent, but gas costs and slippage
- Application must account for slippage, gas fees, and liquidity when generating trade recommendations

## Risk Management in Crypto

**Position-level risk adjusted for crypto:**

| Position | Traditional Max | Crypto Adjustment |
|----------|----------------|-------------------|
| BTC (Tier 1) | 15% single stock max | 40-50% acceptable as portfolio anchor |
| ETH (Tier 2) | 10-15% | 20-30% acceptable |
| Tier 3 alt | 10% | 10-15% max |
| Tier 4 alt | 5-7% | 5-8% max |
| Tier 5 / speculative | 2-3% | 1-3% max (lottery position sizing) |

**Drawdown risk management:**
- Set stablecoin floor based on risk profile; this is the primary risk lever in crypto
- Increase stablecoin allocation as portfolio drawdown deepens (de-risking rule)
- Consider partial exit to stablecoin if portfolio hits maximum acceptable drawdown
- BTC and ETH typically recover from bear markets; small-cap alts often do not

**Stop-loss in crypto:**
- Traditional stop-losses fire constantly due to crypto volatility
- Time-based or thesis-based stop-losses more effective than price-based
- Ask: "Is the thesis broken?" not "Is it down X%?"

## Tax Treatment (General Principles)

**Note: Tax law varies by jurisdiction. These are general US-centric principles.**

- **Crypto-to-crypto trades are taxable events**: selling BTC to buy ETH triggers capital gains
- **Stablecoin swaps are taxable**: selling BTC for USDC triggers capital gains
- **Short-term capital gains** (held < 1 year): taxed at ordinary income rates (up to 37%)
- **Long-term capital gains** (held > 1 year): taxed at preferential rates (0%, 15%, or 20%)
- **DeFi complexity:** Yield farming rewards, liquidity provision, and protocol interactions may have different tax treatment
- **Wash sale rule:** Does NOT apply to crypto in the US (as of 2024); can sell and immediately repurchase for tax-loss harvesting

**Tax-efficient rebalancing strategies for crypto:**
1. Harvest losses aggressively (wash sale rule doesn't apply; can repurchase immediately)
2. Hold Tier 1-2 assets > 1 year before trimming (long-term rates)
3. Use LIFO lot selection for overweight Tier 3-5 positions (sell highest-cost lots first if underwater; lowest-cost if gains needed for offset)
4. Rebalance via new contributions where possible (new capital into underweight positions avoids selling)
5. Minimize unnecessary rebalancing; every trade has tax cost

## Crypto-Specific Risk Metrics

**Adapt traditional metrics for crypto context:**

**Beta (vs BTC, not S&P 500):**
- Portfolio beta relative to BTC is the primary risk measure
- Higher alt allocation = higher beta to BTC
- Stablecoin allocation reduces beta to BTC
- Target beta by risk profile:
  - Conservative: ~0.5-0.7 BTC beta
  - Moderate: ~0.7-0.9 BTC beta
  - Growth: ~0.9-1.1 BTC beta
  - Aggressive: ~1.1-1.5 BTC beta

**Stablecoin ratio:**
- Portfolio percentage in stablecoins = primary de-risking lever
- Conservative: 30-50% stables; Aggressive: 0-10% stables

**Altcoin ratio:**
- Percentage of crypto exposure in Tier 3-5 alts (excluding BTC/ETH)
- Higher alt ratio = higher volatility, higher potential return, higher drawdown risk

**BTC/ETH concentration:**
- What % of total crypto exposure is in BTC+ETH?
- Higher = more conservative; lower = more aggressive

**Sharpe Ratio (crypto-adjusted):**
- Use BTC as risk-free benchmark alternative, or use stablecoin yield (4-6%) as risk-free rate
- Crypto portfolios targeting > 1.0 Sharpe (vs ~0.4 for S&P 500)
- Due to high volatility, Sharpe > 2.0 in crypto is exceptional

## Summary: Key Framework Adjustments for Crypto

| Traditional Framework | Crypto Adjustment |
|----------------------|------------------|
| Bonds = risk-off asset | Stablecoins = risk-off asset |
| S&P 500 = benchmark | BTC = benchmark |
| 5% drift threshold | 10–15% drift threshold |
| 15% single position max | BTC/ETH up to 40–50% acceptable; Tier 3+ per position limits |
| Market cap tiers (size risk) | **Two dimensions: Tier (market cap) + Category (asset type)** |
| 6+ sector diversification | Category diversification across L1, DeFi, AI, Infrastructure, RWA, Stables |
| HHI < 1000 target | Crypto HHI naturally higher due to BTC anchor; evaluate tier + category balance instead |
| Annual/quarterly review | Continuous monitoring + weekly check |
| Tax-advantaged rebalancing | No crypto tax-advantaged accounts; wider thresholds to minimize tax drag |
| -50% = severe bear market | -50% = normal crypto bear market; -80%+ = severe |
| Sharpe > 0.5 target | Sharpe > 1.0 target (compensate for higher risk) |
| Sector concentration (equities) | Category concentration limits per category table above |
