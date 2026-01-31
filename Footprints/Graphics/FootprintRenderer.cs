using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using Footprints.Domain;

namespace Footprints.Graphics;

/// <summary>
/// Handles rendering of footprint data on the chart using a separated 3-column layout:
/// LEFT column (35%) = SELL volume bins with red gradient rectangles,
/// CENTER column (30%) = Traditional thin candlestick (body + shadows),
/// RIGHT column (35%) = BUY volume bins with green gradient rectangles.
/// Includes dynamic Y-axis zoom control for optimal readability.
/// Tracks all chart objects for proper cleanup.
/// </summary>
public class FootprintRenderer
{
    private readonly Chart _chart;
    private readonly FootprintConfig _config;
    private readonly Symbol _symbol;

    // Tracks all chart objects by bar time for cleanup
    private readonly Dictionary<DateTime, List<ChartObject>> _chartObjects;

    // Reference to Bars data for OHLC access (set via SetBarsReference)
    private Bars _bars;

    // Reference to ChartArea for Y-axis zoom control and pixel calculations (set via SetChartArea)
    private ChartArea _chartArea;

    // Reference to MarketData for Market Depth access (set via SetMarketDataReference)
    private MarketData _marketData;

    // Action delegate for printing debug messages to the cTrader log
    private readonly Action<string> _print;

    /// <summary>
    /// Multiplier applied to font size to calculate pixels needed per price level.
    /// A font of size 10 needs approximately 22 pixels of vertical space per level.
    /// </summary>
    private const double PIXELS_PER_LEVEL_MULTIPLIER = 2.2;

    // ============================================
    // 3-COLUMN LAYOUT CONSTANTS
    // ============================================

    /// <summary>Left edge of the SELL section (10% gap from bar start for visible separation).</summary>
    private const double SELL_SECTION_START = 0.10;

    /// <summary>Right edge of the SELL section (35% of bar width).</summary>
    private const double SELL_SECTION_END = 0.35;

    /// <summary>Left edge of the CANDLE section (37%, 2% gap after SELL).</summary>
    private const double CANDLE_SECTION_START = 0.37;

    /// <summary>Right edge of the CANDLE section (63%, 26% wide).</summary>
    private const double CANDLE_SECTION_END = 0.63;

    /// <summary>Center of the CANDLE section for shadow (wick) lines.</summary>
    private const double CANDLE_SECTION_CENTER = 0.50;

    /// <summary>Left edge of the BUY section (65%, 2% gap after CANDLE).</summary>
    private const double BUY_SECTION_START = 0.65;

    /// <summary>Right edge of the BUY section (90%, 10% gap from bar end for visible separation).</summary>
    private const double BUY_SECTION_END = 0.90;

    /// <summary>
    /// Creates a new footprint renderer.
    /// </summary>
    /// <param name="chart">cTrader Chart API</param>
    /// <param name="config">Rendering configuration</param>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="print">Action delegate for Print() output to cTrader log</param>
    public FootprintRenderer(Chart chart, FootprintConfig config, Symbol symbol, Action<string> print)
    {
        _chart = chart;
        _config = config;
        _symbol = symbol;
        _print = print;
        _chartObjects = new Dictionary<DateTime, List<ChartObject>>();
    }

    /// <summary>
    /// Sets the Bars data reference for OHLC access when drawing candlesticks.
    /// Must be called before RenderFootprint if custom candlesticks are enabled.
    /// </summary>
    /// <param name="bars">Bars data series from the indicator</param>
    public void SetBarsReference(Bars bars)
    {
        _bars = bars;
    }

    /// <summary>
    /// Sets the ChartArea reference for Y-axis zoom control and pixel-based calculations.
    /// Must be called before RenderFootprint if auto-zoom is enabled.
    /// Uses ChartArea (not IndicatorArea) to access Height for pixel-based zoom.
    /// </summary>
    /// <param name="chartArea">The Chart.ChartArea from the main indicator class</param>
    public void SetChartArea(ChartArea chartArea)
    {
        _chartArea = chartArea;
    }

    /// <summary>
    /// Sets the MarketData reference for accessing Market Depth (DOM).
    /// Must be called before DrawMarketDepth if Market Depth display is enabled.
    /// </summary>
    /// <param name="marketData">The MarketData from the main indicator class</param>
    public void SetMarketDataReference(MarketData marketData)
    {
        _marketData = marketData;
    }

    // ============================================
    // BAR DURATION HELPER
    // ============================================

    /// <summary>
    /// Calculates the duration of a bar (distance to the next bar) for DateTime-based positioning.
    /// Falls back to the previous bar interval or a 15-minute default for the last bar.
    /// </summary>
    /// <param name="barIndex">Bar index on the chart</param>
    /// <returns>TimeSpan representing the bar duration</returns>
    private TimeSpan GetBarDuration(int barIndex)
    {
        if (_bars == null)
            return TimeSpan.FromMinutes(15);

        DateTime barStartTime = _bars.OpenTimes[barIndex];

        if (barIndex + 1 < _bars.Count)
        {
            return _bars.OpenTimes[barIndex + 1] - barStartTime;
        }

        // Last bar: estimate from previous bars if possible
        if (_bars.Count >= 2)
        {
            return _bars.OpenTimes[_bars.Count - 1] - _bars.OpenTimes[_bars.Count - 2];
        }

        return TimeSpan.FromMinutes(15); // Fallback
    }

    /// <summary>
    /// Calculates a DateTime position within a bar given a fractional offset (0.0 to 1.0).
    /// Used to position elements within the 3-column layout (SELL | CANDLE | BUY).
    /// </summary>
    /// <param name="barIndex">Bar index on the chart</param>
    /// <param name="fraction">Fractional position within the bar (0.0 = bar start, 1.0 = bar end)</param>
    /// <returns>DateTime at the given fractional position</returns>
    private DateTime GetBarDateTime(int barIndex, double fraction)
    {
        DateTime barStartTime = _bars.OpenTimes[barIndex];
        TimeSpan barDuration = GetBarDuration(barIndex);
        return barStartTime.Add(TimeSpan.FromSeconds(barDuration.TotalSeconds * fraction));
    }

    // ============================================
    // COLOR GRADIENT HELPERS
    // ============================================

    /// <summary>
    /// Computes a red gradient color for the SELL column based on how dominant the sell side is.
    /// Higher sell dominance produces a deeper, more saturated red.
    /// When sell is not dominant (ratio &lt;= 1.0), a muted dark red is used.
    /// </summary>
    /// <param name="sellVolume">Sell volume in the bin</param>
    /// <param name="buyVolume">Buy volume in the bin</param>
    /// <returns>Red-gradient Color for the SELL rectangle</returns>
    private Color GetSellGradientColor(long sellVolume, long buyVolume)
    {
        if (sellVolume == 0)
            return Color.FromArgb(40, 120, 30, 30); // Very faint dark red

        double ratio = buyVolume > 0
            ? (double)sellVolume / buyVolume
            : 3.0; // Cap at 3x if buy is zero

        // Clamp ratio to [1.0, 5.0] for gradient mapping
        ratio = Math.Max(1.0, Math.Min(5.0, ratio));

        // Map ratio 1.0-5.0 to intensity 0.3-1.0
        double intensity = 0.3 + (ratio - 1.0) / 4.0 * 0.7;

        int r = (int)(180 * intensity);
        int g = (int)(30 * (1.0 - intensity * 0.5));
        int b = (int)(30 * (1.0 - intensity * 0.5));

        return Color.FromArgb(255, r, g, b);
    }

    /// <summary>
    /// Computes a green gradient color for the BUY column based on how dominant the buy side is.
    /// Higher buy dominance produces a deeper, more saturated green.
    /// When buy is not dominant (ratio &lt;= 1.0), a muted dark green is used.
    /// </summary>
    /// <param name="buyVolume">Buy volume in the bin</param>
    /// <param name="sellVolume">Sell volume in the bin</param>
    /// <returns>Green-gradient Color for the BUY rectangle</returns>
    private Color GetBuyGradientColor(long buyVolume, long sellVolume)
    {
        if (buyVolume == 0)
            return Color.FromArgb(40, 30, 120, 30); // Very faint dark green

        double ratio = sellVolume > 0
            ? (double)buyVolume / sellVolume
            : 3.0; // Cap at 3x if sell is zero

        // Clamp ratio to [1.0, 5.0] for gradient mapping
        ratio = Math.Max(1.0, Math.Min(5.0, ratio));

        // Map ratio 1.0-5.0 to intensity 0.3-1.0
        double intensity = 0.3 + (ratio - 1.0) / 4.0 * 0.7;

        int r = (int)(30 * (1.0 - intensity * 0.5));
        int g = (int)(180 * intensity);
        int b = (int)(30 * (1.0 - intensity * 0.5));

        return Color.FromArgb(255, r, g, b);
    }

    // ============================================
    // MAIN RENDER ENTRY POINT
    // ============================================

    /// <summary>
    /// Renders a complete footprint bar on the chart using a separated 3-column layout:
    /// SELL bins (left) | Candlestick (center) | BUY bins (right).
    /// When bins are available, renders aggregated bin data with separated columns.
    /// Falls back to legacy rendering when bins are not available.
    /// </summary>
    /// <param name="footprintBar">Footprint bar to render</param>
    /// <param name="barIndex">Bar index on chart</param>
    public void RenderFootprint(FootprintBar footprintBar, int barIndex)
    {
        if (footprintBar == null || footprintBar.PriceLevels.Count == 0)
            return;

        // Draw legend once (static, repositioned on scroll/zoom)
        if (_config.ShowLegend && !_chartObjects.ContainsKey(LEGEND_KEY))
        {
            DrawLegend();
        }

        // Clear previous objects for this bar
        ClearFootprint(footprintBar.BarTime);

        // Initialize object tracking for this bar
        if (!_chartObjects.ContainsKey(footprintBar.BarTime))
            _chartObjects[footprintBar.BarTime] = new List<ChartObject>();

        // Determine if we should use bins or legacy price levels
        bool useBins = footprintBar.Bins != null && footprintBar.Bins.Count > 0;

        if (useBins)
        {
            // === 3-COLUMN BIN-BASED RENDERING ===
            // Rendering order (back to front):
            // 1. Candlestick in CENTER section (background layer)
            // 2. SELL bin rectangles + text in LEFT section
            // 3. BUY bin rectangles + text in RIGHT section
            // 4. Value Area overlay
            // 5. POC line
            // 6. Imbalance markers

            // 1. Draw thin candlestick in CENTER section (background)
            if (_config.ShowCustomCandlesticks && _bars != null)
            {
                DrawCandlestick(barIndex, footprintBar.BarTime);
            }

            // 2. Draw SELL bins in LEFT section
            foreach (FootprintBin bin in footprintBar.Bins)
            {
                DrawSellBinColumn(bin, barIndex, footprintBar.BarTime);
            }

            // 3. Draw BUY bins in RIGHT section
            foreach (FootprintBin bin in footprintBar.Bins)
            {
                DrawBuyBinColumn(bin, barIndex, footprintBar.BarTime);
            }

            // 4. Render value area (semi-transparent overlay)
            if (_config.ShowValueArea)
            {
                DrawBinValueAreaBox(footprintBar.Bins, barIndex, footprintBar.BarTime);
            }

            // 5. Render POC line
            if (_config.ShowPOC)
            {
                FootprintBin pocBin = footprintBar.Bins.FirstOrDefault(b => b.IsPointOfControl);
                if (pocBin != null)
                {
                    DrawBinPOCLine(pocBin, barIndex, footprintBar.BarTime);
                }
            }

            // 6. Render bin imbalance markers
            if (_config.ShowImbalances)
            {
                foreach (FootprintBin bin in footprintBar.Bins)
                {
                    if (bin.HasImbalance)
                    {
                        DrawBinImbalanceMarker(bin, barIndex, footprintBar.BarTime);
                    }
                }
            }
        }
        else
        {
            // === LEGACY PRICE LEVEL RENDERING ===

            // 1. Draw custom candlestick (background layer)
            if (_config.ShowCustomCandlesticks && _bars != null)
            {
                DrawCandlestick(barIndex, footprintBar.BarTime);
            }

            // 2. Render value area (semi-transparent background)
            if (_config.ShowValueArea && footprintBar.ValueAreaHigh > 0 && footprintBar.ValueAreaLow > 0)
            {
                DrawValueAreaBox(footprintBar, barIndex);
            }

            // 3. Render POC line
            if (_config.ShowPOC && footprintBar.PointOfControl != null)
            {
                DrawPOCLine(footprintBar.PointOfControl, barIndex, footprintBar.BarTime);
            }

            // 4. Render price levels (text) - the primary focus
            List<PriceLevel> sortedLevels = footprintBar.GetSortedLevels();
            foreach (PriceLevel level in sortedLevels)
            {
                DrawPriceLevelText(level, barIndex, footprintBar.BarTime);
            }

            // 5. Render imbalance markers
            if (_config.ShowImbalances && footprintBar.ImbalanceLevels.Count > 0)
            {
                foreach (PriceLevel level in footprintBar.ImbalanceLevels)
                {
                    DrawImbalanceMarker(level, barIndex, footprintBar.BarTime);
                }
            }
        }

        // 7. Render delta (below the bar) - same for both modes
        if (_config.ShowDelta)
        {
            DrawDelta(footprintBar, barIndex);
        }
    }

    // ============================================
    // CANDLESTICK DRAWING (CENTER COLUMN)
    // ============================================

    /// <summary>
    /// Draws a custom candlestick in the CENTER column (30% of bar width) of the 3-column layout.
    /// The body is a thin rectangle positioned between CANDLE_SECTION_START and CANDLE_SECTION_END.
    /// Shadows (wicks) are drawn as vertical trend lines at CANDLE_SECTION_CENTER.
    /// </summary>
    /// <param name="barIndex">Bar index on the chart</param>
    /// <param name="barTime">Bar opening time for object naming and tracking</param>
    private void DrawCandlestick(int barIndex, DateTime barTime)
    {
        if (barIndex < 0 || barIndex >= _bars.Count)
            return;

        double open = _bars.OpenPrices[barIndex];
        double high = _bars.HighPrices[barIndex];
        double low = _bars.LowPrices[barIndex];
        double close = _bars.ClosePrices[barIndex];

        bool isBullish = close >= open;
        double bodyTop = isBullish ? close : open;
        double bodyBottom = isBullish ? open : close;

        // Select candle color with opacity applied
        Color bodyColor = isBullish
            ? _config.ApplyCandleOpacity(_config.BullishCandleColor)
            : _config.ApplyCandleOpacity(_config.BearishCandleColor);

        Color shadowColor = Color.FromArgb(
            _config.CandleBodyOpacity,
            _config.ShadowColor.R,
            _config.ShadowColor.G,
            _config.ShadowColor.B);

        // Calculate DateTime positions for the CENTER column
        DateTime candleStart = GetBarDateTime(barIndex, CANDLE_SECTION_START);
        DateTime candleEnd = GetBarDateTime(barIndex, CANDLE_SECTION_END);
        DateTime candleCenter = GetBarDateTime(barIndex, CANDLE_SECTION_CENTER);

        // Draw upper shadow (wick): from body top to high
        if (high > bodyTop)
        {
            string upperShadowName = $"FP_Shadow_U_{barTime:yyyyMMddHHmmss}";
            ChartTrendLine upperShadow = _chart.DrawTrendLine(
                upperShadowName, candleCenter, bodyTop, candleCenter, high,
                shadowColor, _config.ShadowThickness, LineStyle.Solid);
            _chartObjects[barTime].Add(upperShadow);
        }

        // Draw lower shadow (tail): from body bottom to low
        if (low < bodyBottom)
        {
            string lowerShadowName = $"FP_Shadow_L_{barTime:yyyyMMddHHmmss}";
            ChartTrendLine lowerShadow = _chart.DrawTrendLine(
                lowerShadowName, candleCenter, bodyBottom, candleCenter, low,
                shadowColor, _config.ShadowThickness, LineStyle.Solid);
            _chartObjects[barTime].Add(lowerShadow);
        }

        // Draw candle body as a filled rectangle in the CENTER section
        // Ensure minimum body height for doji candles (open == close)
        if (bodyTop == bodyBottom)
        {
            bodyTop += _symbol.TickSize * 0.5;
            bodyBottom -= _symbol.TickSize * 0.5;
        }

        string bodyName = $"FP_Body_{barTime:yyyyMMddHHmmss}";

        ChartRectangle body = _chart.DrawRectangle(
            bodyName, candleStart, bodyTop, candleEnd, bodyBottom, bodyColor);
        body.IsFilled = true;
        body.Color = bodyColor;
        _chartObjects[barTime].Add(body);
    }

    // ============================================
    // DYNAMIC ZOOM CONTROL
    // ============================================

    /// <summary>
    /// Calculates the optimal Y-axis range using pixel-based calculation so that
    /// each price level (or bin) in the footprint gets enough vertical pixels for readable text.
    /// Uses ChartArea.Height to determine available vertical space.
    /// When bins are used, the number of levels is the bin count (much smaller).
    /// Focuses on RECENT bars only (configurable via AutoZoomFocusBars) to avoid zoom issues
    /// when old bars are at vastly different price levels from current price action.
    /// </summary>
    /// <param name="chartArea">ChartArea providing pixel Height for calculation</param>
    /// <param name="visibleFootprintBars">List of footprint bars currently visible on the chart</param>
    /// <returns>Tuple of (bottomY, topY) for the optimal range, or null if zoom should not be applied</returns>
    public (double BottomY, double TopY)? CalculateOptimalYRange(ChartArea chartArea, List<FootprintBar> visibleFootprintBars)
    {
        if (visibleFootprintBars == null || visibleFootprintBars.Count == 0)
            return null;

        if (chartArea == null)
            return null;

        // IMPORTANT: Focus on RECENT bars only to prevent zoom issues when there are
        // distant old bars in the visible area (e.g., one bar at $80,000 and another at $31,000).
        int maxBarsForZoom = Math.Min(_config.AutoZoomFocusBars, visibleFootprintBars.Count);

        // Take the most recent bars (sorted by time descending, then take N)
        List<FootprintBar> recentBars = visibleFootprintBars
            .OrderByDescending(b => b.BarTime)
            .Take(maxBarsForZoom)
            .ToList();

        // _print($"[AutoZoom] Using {recentBars.Count} recent bars (out of {visibleFootprintBars.Count} visible) for zoom calculation");

        // ---- OUTLIER DETECTION ----
        // Calculate median price of recent bars and filter out bars >10% away from the median.
        // This prevents distant bars (from stale stored ticks) from distorting the zoom range.
        if (recentBars.Count >= 3)
        {
            List<double> midPrices = recentBars
                .Select(b =>
                {
                    bool hasBins = b.Bins != null && b.Bins.Count > 0;
                    double high = hasBins ? b.Bins.Max(bin => bin.PriceTop) : b.GetHighestPrice();
                    double low = hasBins ? b.Bins.Min(bin => bin.PriceBottom) : b.GetLowestPrice();
                    return (high + low) / 2.0;
                })
                .OrderBy(p => p)
                .ToList();

            double medianPrice = midPrices[midPrices.Count / 2];
            double outlierThreshold = medianPrice * 0.10; // 10% from median

            List<FootprintBar> filteredBars = recentBars.Where(b =>
            {
                bool hasBins = b.Bins != null && b.Bins.Count > 0;
                double high = hasBins ? b.Bins.Max(bin => bin.PriceTop) : b.GetHighestPrice();
                double low = hasBins ? b.Bins.Min(bin => bin.PriceBottom) : b.GetLowestPrice();
                double barMidPrice = (high + low) / 2.0;
                return Math.Abs(barMidPrice - medianPrice) <= outlierThreshold;
            }).ToList();

            int outlierCount = recentBars.Count - filteredBars.Count;
            if (outlierCount > 0)
            {
                // _print($"[AutoZoom] Filtered {outlierCount} outlier bars (>{outlierThreshold:F2} from median {medianPrice:F2})");
            }

            // Use filtered bars if we still have at least 1 bar left; otherwise keep original set
            if (filteredBars.Count > 0)
            {
                recentBars = filteredBars;
            }
        }

        // Find the footprint bar with the most price levels/bins and track overall high/low
        double globalHigh = double.MinValue;
        double globalLow = double.MaxValue;
        int maxLevelCount = 0;

        foreach (FootprintBar fpBar in recentBars)
        {
            if (fpBar == null || fpBar.PriceLevels.Count == 0)
                continue;

            // Determine level count: use bins if available, otherwise price levels
            bool hasBins = fpBar.Bins != null && fpBar.Bins.Count > 0;
            int levelCount = hasBins ? fpBar.Bins.Count : fpBar.PriceLevels.Count;

            // Determine high/low: use OHLC High/Low if bins present, otherwise from price levels
            double barHigh;
            double barLow;
            if (hasBins)
            {
                barHigh = fpBar.Bins.Max(b => b.PriceTop);
                barLow = fpBar.Bins.Min(b => b.PriceBottom);
            }
            else
            {
                barHigh = fpBar.GetHighestPrice();
                barLow = fpBar.GetLowestPrice();
            }

            if (barHigh > globalHigh) globalHigh = barHigh;
            if (barLow < globalLow) globalLow = barLow;

            if (levelCount > maxLevelCount)
                maxLevelCount = levelCount;
        }

        // Guard: no valid data
        if (globalHigh == double.MinValue || globalLow == double.MaxValue)
            return null;

        // Guard: too few levels to warrant auto-zoom
        if (maxLevelCount < _config.MinLevelsForAutoZoom)
            return null;

        // Step 1: Get available vertical space in pixels
        double chartHeight = chartArea.Height;

        // Step 2: Calculate pixels needed per price level based on font size
        double pixelsPerLevel = _config.FontSize * PIXELS_PER_LEVEL_MULTIPLIER;

        // Step 3: Calculate how many levels can fit in the available screen space
        int maxLevelsVisible = (int)(chartHeight / pixelsPerLevel);

        // Step 4: Determine the actual number of levels to display
        int actualLevelCount = maxLevelCount;
        double tickSize = _symbol.TickSize;

        // The price range needed to contain all footprint levels
        double footprintPriceRange = globalHigh - globalLow;

        // Step 5: Center the range on the footprint data midpoint
        double midPrice = (globalHigh + globalLow) / 2.0;
        double halfRange = footprintPriceRange / 2.0;

        // Step 6: Add small padding for visual breathing room
        double paddingPercent = _config.ZoomPaddingPercent / 100.0;
        double padding = footprintPriceRange * paddingPercent;

        // Add extra padding below for delta text if enabled
        double extraBottomPadding = 0;
        if (_config.ShowDelta)
        {
            extraBottomPadding = tickSize * 5;
        }

        double bottomY = midPrice - halfRange - padding - extraBottomPadding;
        double topY = midPrice + halfRange + padding;

        // Log diagnostic information (commented out - enable for debugging)
        // double totalRange = topY - bottomY;
        // _print($"[AutoZoom] ChartArea.Height={chartHeight:F0}px, PixelsPerLevel={pixelsPerLevel:F1}, MaxLevelsVisible={maxLevelsVisible}");
        // _print($"[AutoZoom] ActualLevels={actualLevelCount}, TickSize={tickSize}, FootprintRange={footprintPriceRange:F5}");
        // _print($"[AutoZoom] MidPrice={midPrice:F5}, BottomY={bottomY:F5}, TopY={topY:F5}, TotalRange={totalRange:F5}");

        return (bottomY, topY);
    }

    /// <summary>
    /// Applies dynamic Y-axis zoom based on the visible footprint bars.
    /// Uses pixel-based calculation via ChartArea.Height to maximize footprint text readability.
    /// </summary>
    /// <param name="visibleFootprintBars">List of currently visible footprint bars</param>
    public void ApplyDynamicZoom(List<FootprintBar> visibleFootprintBars)
    {
        if (!_config.EnableAutoZoom || _chartArea == null)
            return;

        (double BottomY, double TopY)? optimalRange = CalculateOptimalYRange(_chartArea, visibleFootprintBars);

        if (optimalRange == null)
            return;

        // Apply the calculated range to the chart Y-axis
        _chartArea.SetYRange(optimalRange.Value.BottomY, optimalRange.Value.TopY);
    }

    // ============================================
    // 3-COLUMN BIN RENDERING
    // ============================================

    /// <summary>
    /// Draws a SELL volume bin in the LEFT column (first 35% of bar width).
    /// Renders a red-gradient background rectangle sized to the sell volume,
    /// and a sell volume text number in the sell color.
    /// Bins with zero sell volume are skipped entirely.
    /// </summary>
    /// <param name="bin">The footprint bin to draw sell data for</param>
    /// <param name="barIndex">Bar index on chart</param>
    /// <param name="barTime">Bar opening time for object naming and tracking</param>
    private void DrawSellBinColumn(FootprintBin bin, int barIndex, DateTime barTime)
    {
        if (_bars == null)
            return;

        // Always draw the rectangle background (even for zero volume, for visual consistency)
        DateTime sellStart = GetBarDateTime(barIndex, SELL_SECTION_START);
        DateTime sellEnd = GetBarDateTime(barIndex, SELL_SECTION_END);

        // Vertical gap for separation between adjacent bins
        double binHeight = bin.PriceTop - bin.PriceBottom;
        double verticalGap = binHeight * 0.05;

        // Compute red gradient color based on sell dominance
        Color sellRectColor = GetSellGradientColor(bin.SellVolume, bin.BuyVolume);
        sellRectColor = Color.FromArgb(_config.BinRectangleOpacity, sellRectColor.R, sellRectColor.G, sellRectColor.B);

        if (_config.ShowBinRectangles)
        {
            string rectName = $"FP_SellBin_{barTime:yyyyMMddHHmmss}_{bin.PriceBottom:F5}";

            ChartRectangle rect = _chart.DrawRectangle(
                rectName,
                sellStart, bin.PriceTop - verticalGap,
                sellEnd, bin.PriceBottom + verticalGap,
                sellRectColor);
            rect.IsFilled = true;
            rect.Thickness = 0;

            _chartObjects[barTime].Add(rect);
        }

        // Draw sell volume text (skip if zero)
        if (bin.SellVolume > 0)
        {
            string sellText = $"{bin.SellVolume}";
            string sellTextName = $"FP_SellText_{barTime:yyyyMMddHHmmss}_{bin.PriceBottom:F5}";

            // Determine text color
            Color textColor;
            if (bin.HasImbalance && bin.SellVolume > bin.BuyVolume)
            {
                textColor = _config.ImbalanceSellColor;
            }
            else
            {
                textColor = _config.SellColor;
            }

            // Position text using DateTime for the center of the SELL section
            DateTime sellTextTime = GetBarDateTime(barIndex, (SELL_SECTION_START + SELL_SECTION_END) / 2.0);

            ChartText sellTextObj = _chart.DrawText(
                sellTextName, sellText, sellTextTime, bin.PriceMid, textColor);
            sellTextObj.FontSize = _config.FontSize;
            sellTextObj.HorizontalAlignment = HorizontalAlignment.Center;
            sellTextObj.VerticalAlignment = VerticalAlignment.Center;

            _chartObjects[barTime].Add(sellTextObj);
        }
    }

    /// <summary>
    /// Draws a BUY volume bin in the RIGHT column (last 35% of bar width).
    /// Renders a green-gradient background rectangle sized to the buy volume,
    /// and a buy volume text number in the buy color.
    /// Bins with zero buy volume are skipped entirely.
    /// </summary>
    /// <param name="bin">The footprint bin to draw buy data for</param>
    /// <param name="barIndex">Bar index on chart</param>
    /// <param name="barTime">Bar opening time for object naming and tracking</param>
    private void DrawBuyBinColumn(FootprintBin bin, int barIndex, DateTime barTime)
    {
        if (_bars == null)
            return;

        DateTime buyStart = GetBarDateTime(barIndex, BUY_SECTION_START);
        DateTime buyEnd = GetBarDateTime(barIndex, BUY_SECTION_END);

        // Vertical gap for separation between adjacent bins
        double binHeight = bin.PriceTop - bin.PriceBottom;
        double verticalGap = binHeight * 0.05;

        // Compute green gradient color based on buy dominance
        Color buyRectColor = GetBuyGradientColor(bin.BuyVolume, bin.SellVolume);
        buyRectColor = Color.FromArgb(_config.BinRectangleOpacity, buyRectColor.R, buyRectColor.G, buyRectColor.B);

        if (_config.ShowBinRectangles)
        {
            string rectName = $"FP_BuyBin_{barTime:yyyyMMddHHmmss}_{bin.PriceBottom:F5}";

            ChartRectangle rect = _chart.DrawRectangle(
                rectName,
                buyStart, bin.PriceTop - verticalGap,
                buyEnd, bin.PriceBottom + verticalGap,
                buyRectColor);
            rect.IsFilled = true;
            rect.Thickness = 0;

            _chartObjects[barTime].Add(rect);
        }

        // Draw buy volume text (skip if zero)
        if (bin.BuyVolume > 0)
        {
            string buyText = $"{bin.BuyVolume}";
            string buyTextName = $"FP_BuyText_{barTime:yyyyMMddHHmmss}_{bin.PriceBottom:F5}";

            // Determine text color
            Color textColor;
            if (bin.HasImbalance && bin.BuyVolume > bin.SellVolume)
            {
                textColor = _config.ImbalanceBuyColor;
            }
            else
            {
                textColor = _config.BuyColor;
            }

            // Position text using DateTime for the center of the BUY section
            DateTime buyTextTime = GetBarDateTime(barIndex, (BUY_SECTION_START + BUY_SECTION_END) / 2.0);

            ChartText buyTextObj = _chart.DrawText(
                buyTextName, buyText, buyTextTime, bin.PriceMid, textColor);
            buyTextObj.FontSize = _config.FontSize;
            buyTextObj.HorizontalAlignment = HorizontalAlignment.Center;
            buyTextObj.VerticalAlignment = VerticalAlignment.Center;

            _chartObjects[barTime].Add(buyTextObj);
        }
    }

    /// <summary>
    /// Draws a POC line spanning the full bar width at the midpoint of the POC bin.
    /// Uses DateTime coordinates for precise positioning across all three columns.
    /// Uses DotsRare line style to avoid overlapping with volume text.
    /// </summary>
    /// <param name="pocBin">The bin identified as Point of Control</param>
    /// <param name="barIndex">Bar index on chart</param>
    /// <param name="barTime">Bar opening time for object naming and tracking</param>
    private void DrawBinPOCLine(FootprintBin pocBin, int barIndex, DateTime barTime)
    {
        string objectName = $"FP_POC_{barTime:yyyyMMddHHmmss}";

        // POC line spans the full bar width (from sell start to buy end)
        DateTime lineStart = GetBarDateTime(barIndex, SELL_SECTION_START);
        DateTime lineEnd = GetBarDateTime(barIndex, BUY_SECTION_END);

        ChartTrendLine line = _chart.DrawTrendLine(
            objectName, lineStart, pocBin.PriceMid, lineEnd, pocBin.PriceMid,
            _config.POCColor, _config.POCLineThickness, LineStyle.DotsRare);

        _chartObjects[barTime].Add(line);
    }

    /// <summary>
    /// Draws the Value Area box as a BORDER ONLY (not filled) for clarity.
    /// Spans from the lowest value-area bin bottom to the highest value-area bin top,
    /// across the full bar width. Uses a thin dotted border with low opacity
    /// so it does not obscure other chart elements.
    /// </summary>
    /// <param name="bins">All bins for the bar</param>
    /// <param name="barIndex">Bar index on chart</param>
    /// <param name="barTime">Bar opening time for object naming and tracking</param>
    private void DrawBinValueAreaBox(List<FootprintBin> bins, int barIndex, DateTime barTime)
    {
        List<FootprintBin> valueAreaBins = bins.Where(b => b.IsInValueArea).ToList();
        if (valueAreaBins.Count == 0)
            return;

        double vaHigh = valueAreaBins.Max(b => b.PriceTop);
        double vaLow = valueAreaBins.Min(b => b.PriceBottom);

        string objectName = $"FP_ValueArea_{barTime:yyyyMMddHHmmss}";

        // BORDER ONLY with low opacity - not filled to avoid obscuring other elements
        Color vaColor = Color.FromArgb(
            100,
            _config.ValueAreaColor.R,
            _config.ValueAreaColor.G,
            _config.ValueAreaColor.B);

        // Value Area box spans the full bar width
        DateTime vaStart = GetBarDateTime(barIndex, SELL_SECTION_START);
        DateTime vaEnd = GetBarDateTime(barIndex, BUY_SECTION_END);

        ChartRectangle rect = _chart.DrawRectangle(
            objectName, vaStart, vaHigh, vaEnd, vaLow, vaColor);
        rect.IsFilled = false;  // BORDER ONLY, NOT FILLED
        rect.Thickness = _config.ValueAreaBorderThickness;
        rect.LineStyle = LineStyle.DotsRare;  // Dotted line for subtlety

        _chartObjects[barTime].Add(rect);
    }

    /// <summary>
    /// Draws an imbalance marker for a bin. For the 3-column layout, the marker
    /// is positioned at the edge of the dominant side's column.
    /// </summary>
    /// <param name="bin">The imbalanced bin</param>
    /// <param name="barIndex">Bar index on chart</param>
    /// <param name="barTime">Bar opening time for object naming and tracking</param>
    private void DrawBinImbalanceMarker(FootprintBin bin, int barIndex, DateTime barTime)
    {
        string objectName = $"FP_BinImb_{barTime:yyyyMMddHHmmss}_{bin.PriceBottom:F5}";

        Color markerColor = _config.GetImbalanceColor(bin.BuyVolume, bin.SellVolume);

        // Position the imbalance marker at the outer edge of the dominant column
        DateTime markerTime;
        HorizontalAlignment alignment;
        if (bin.BuyVolume > bin.SellVolume)
        {
            // Buy imbalance: marker at the right edge of the BUY column
            markerTime = GetBarDateTime(barIndex, BUY_SECTION_END);
            alignment = HorizontalAlignment.Right;
        }
        else
        {
            // Sell imbalance: marker at the left edge of the SELL column
            markerTime = GetBarDateTime(barIndex, SELL_SECTION_START);
            alignment = HorizontalAlignment.Left;
        }

        ChartText marker = _chart.DrawText(objectName, "\u2551", markerTime, bin.PriceMid, markerColor);
        marker.FontSize = _config.FontSize + 2;
        marker.HorizontalAlignment = alignment;
        marker.VerticalAlignment = VerticalAlignment.Center;

        _chartObjects[barTime].Add(marker);
    }

    // ============================================
    // LEGACY FOOTPRINT TEXT RENDERING
    // ============================================

    /// <summary>
    /// Draws text for a single price level showing "SELL | BUY" format.
    /// Uses monospace font for aligned columns and increased font size for readability.
    /// </summary>
    private void DrawPriceLevelText(PriceLevel level, int barIndex, DateTime barTime)
    {
        string text = $"{level.SellVolume,4} \u2502 {level.BuyVolume,-4}";

        // Choose color
        Color textColor = level.HasImbalance
            ? _config.GetImbalanceColor(level.BuyVolume, level.SellVolume)
            : _config.GetLevelColor(level.BuyVolume, level.SellVolume);

        // Create unique name
        string objectName = $"FP_Level_{barTime:yyyyMMddHHmmss}_{level.Price}";

        // Draw text at price level
        ChartText textObj = _chart.DrawText(objectName, text, barIndex, level.Price, textColor);
        textObj.FontSize = _config.FontSize;
        textObj.HorizontalAlignment = HorizontalAlignment.Center;
        textObj.VerticalAlignment = VerticalAlignment.Center;

        // Track object
        _chartObjects[barTime].Add(textObj);
    }

    /// <summary>
    /// Draws a horizontal line at the Point of Control.
    /// Uses DotsRare line style to avoid overlapping with volume text.
    /// </summary>
    private void DrawPOCLine(PriceLevel poc, int barIndex, DateTime barTime)
    {
        string objectName = $"FP_POC_{barTime:yyyyMMddHHmmss}";

        // Draw horizontal line across the bar
        ChartTrendLine line = _chart.DrawTrendLine(
            objectName, barIndex, poc.Price, barIndex + 1, poc.Price,
            _config.POCColor, _config.POCLineThickness, LineStyle.DotsRare);

        // Track object
        _chartObjects[barTime].Add(line);
    }

    /// <summary>
    /// Draws a semi-transparent rectangle for the Value Area.
    /// </summary>
    private void DrawValueAreaBox(FootprintBar footprintBar, int barIndex)
    {
        string objectName = $"FP_ValueArea_{footprintBar.BarTime:yyyyMMddHHmmss}";

        // Draw rectangle from ValueAreaLow to ValueAreaHigh
        ChartRectangle rect = _chart.DrawRectangle(
            objectName, barIndex, footprintBar.ValueAreaHigh,
            barIndex + 1, footprintBar.ValueAreaLow, _config.ValueAreaColor);
        rect.IsFilled = true;

        // Track object
        _chartObjects[footprintBar.BarTime].Add(rect);
    }

    /// <summary>
    /// Draws a marker (vertical line symbol) for imbalanced levels.
    /// </summary>
    private void DrawImbalanceMarker(PriceLevel level, int barIndex, DateTime barTime)
    {
        string objectName = $"FP_Imbalance_{barTime:yyyyMMddHHmmss}_{level.Price}";

        // Draw a small vertical line to highlight the imbalance
        Color markerColor = _config.GetImbalanceColor(level.BuyVolume, level.SellVolume);

        // Use a symbol (\u2551 = double vertical line) for visual emphasis
        ChartText marker = _chart.DrawText(objectName, "\u2551", barIndex, level.Price, markerColor);
        marker.FontSize = _config.FontSize + 2;
        marker.HorizontalAlignment = HorizontalAlignment.Right;
        marker.VerticalAlignment = VerticalAlignment.Center;

        // Track object
        _chartObjects[barTime].Add(marker);
    }

    /// <summary>
    /// Draws the delta (buy - sell volume) below the bar.
    /// </summary>
    private void DrawDelta(FootprintBar footprintBar, int barIndex)
    {
        string objectName = $"FP_Delta_{footprintBar.BarTime:yyyyMMddHHmmss}";

        string deltaText = $"\u0394 {footprintBar.Delta:+#;-#;0}";
        Color deltaColor = footprintBar.Delta > 0 ? _config.BuyColor :
                          footprintBar.Delta < 0 ? _config.SellColor :
                          _config.NeutralColor;

        // Use bin bottom or lowest price level for delta positioning
        double lowestPrice;
        if (footprintBar.Bins != null && footprintBar.Bins.Count > 0)
        {
            lowestPrice = footprintBar.Bins.Min(b => b.PriceBottom);
        }
        else
        {
            lowestPrice = footprintBar.GetLowestPrice();
        }

        double offset = _symbol.TickSize * 12; // Offset below lowest price (well separated from bins)

        ChartText deltaObj = _chart.DrawText(objectName, deltaText, barIndex, lowestPrice - offset, deltaColor);
        deltaObj.FontSize = _config.FontSize;
        deltaObj.HorizontalAlignment = HorizontalAlignment.Center;
        deltaObj.VerticalAlignment = VerticalAlignment.Top;

        // Track object
        _chartObjects[footprintBar.BarTime].Add(deltaObj);
    }

    // ============================================
    // LEGEND
    // ============================================

    /// <summary>
    /// Special key used to track legend chart objects in the _chartObjects dictionary.
    /// DateTime.MinValue is used because it will never collide with a real bar time.
    /// </summary>
    private static readonly DateTime LEGEND_KEY = DateTime.MinValue;

    /// <summary>
    /// Draws a legend in the top-right corner of the chart using DrawStaticText.
    /// Static positioning ensures the legend stays fixed regardless of scroll/zoom.
    /// Uses a compact multi-line format with larger font for readability.
    /// Updated to reflect the 3-column layout.
    /// </summary>
    private void DrawLegend()
    {
        // Clear any existing legend objects
        if (_chartObjects.ContainsKey(LEGEND_KEY))
        {
            ClearFootprint(LEGEND_KEY);
        }

        _chartObjects[LEGEND_KEY] = new List<ChartObject>();

        // Larger font for legend readability
        int legendFontSize = _config.FontSize + 2;

        // Compact multi-line legend text reflecting the 3-column layout
        string legendText = "FOOTPRINT\nLeft=Sell | Right=Buy\nYellow=POC | Blue=VA";

        // Use DrawStaticText for fixed top-right positioning (independent of bar/price coordinates)
        ChartStaticText legend = _chart.DrawStaticText(
            "FP_Legend",
            legendText,
            VerticalAlignment.Top,
            HorizontalAlignment.Right,
            Color.FromArgb(220, 255, 255, 255)); // Slightly transparent white

        legend.FontSize = legendFontSize;

        _chartObjects[LEGEND_KEY].Add(legend);
    }

    /// <summary>
    /// Ensures the legend exists on the chart. Since DrawStaticText uses fixed
    /// screen positioning (top-right corner), it does not need repositioning
    /// on scroll or zoom changes. Only draws if not already present.
    /// </summary>
    public void RedrawLegend()
    {
        if (_config.ShowLegend && !_chartObjects.ContainsKey(LEGEND_KEY))
        {
            DrawLegend();
        }
    }

    // ============================================
    // BID/ASK LEVELS
    // ============================================

    /// <summary>
    /// Special key used to track Bid/Ask level chart objects in the _chartObjects dictionary.
    /// DateTime.MaxValue is used because it will never collide with a real bar time or the legend key.
    /// </summary>
    private static readonly DateTime BID_ASK_KEY = DateTime.MaxValue;

    /// <summary>
    /// Draws Bid and Ask price levels as dashed horizontal lines to the right of the last bar.
    /// Lines are repositioned on every update to reflect current live Bid/Ask prices.
    /// The lines start after a configurable gap from the last bar and span a configurable width.
    /// Also displays Bid/Ask values and spread information as text labels.
    /// </summary>
    private void DrawBidAskLevels()
    {
        if (!_config.ShowBidAskLevels)
            return;

        if (_bars == null || _bars.Count == 0)
            return;

        // Clear previous Bid/Ask lines
        if (_chartObjects.ContainsKey(BID_ASK_KEY))
        {
            ClearFootprint(BID_ASK_KEY);
        }

        _chartObjects[BID_ASK_KEY] = new List<ChartObject>();

        // Get current bar index (last bar)
        int lastBarIndex = _bars.Count - 1;

        // Calculate line positions: gap + width in bar indices (half of configured width)
        int lineStartBar = lastBarIndex + _config.BidAskLevelGap;
        int lineEndBar = lineStartBar + (_config.BidAskLevelWidth / 2);

        // Get current Bid and Ask prices from the symbol
        double bidPrice = _symbol.Bid;
        double askPrice = _symbol.Ask;

        // Calculate spread in pips
        double spreadInPips = (askPrice - bidPrice) / _symbol.PipSize;

        // Draw Bid line (dashed, red by default)
        string bidName = "FP_BidLevel";
        ChartTrendLine bidLine = _chart.DrawTrendLine(
            bidName,
            lineStartBar, bidPrice,
            lineEndBar, bidPrice,
            _config.BidLevelColor,
            _config.BidAskLevelThickness,
            LineStyle.DotsRare);
        _chartObjects[BID_ASK_KEY].Add(bidLine);

        // Draw Ask line (dashed, green by default)
        string askName = "FP_AskLevel";
        ChartTrendLine askLine = _chart.DrawTrendLine(
            askName,
            lineStartBar, askPrice,
            lineEndBar, askPrice,
            _config.AskLevelColor,
            _config.BidAskLevelThickness,
            LineStyle.DotsRare);
        _chartObjects[BID_ASK_KEY].Add(askLine);

        // Draw Bid/Ask values and spread as a single multi-line text label
        // Positioned below the Bid line, at the end of the Bid/Ask lines
        string bidAskSpreadText = $"Bid={bidPrice.ToString($"F{_symbol.Digits}")} - Ask={askPrice.ToString($"F{_symbol.Digits}")}\nSpread={spreadInPips:F1} pips";
        string bidAskSpreadTextName = "FP_BidAskInfo";

        ChartText bidAskSpreadTextObj = _chart.DrawText(
            bidAskSpreadTextName,
            bidAskSpreadText,
            lineEndBar,
            bidPrice,
            Color.White);
        bidAskSpreadTextObj.FontSize = _config.FontSize + 2;
        bidAskSpreadTextObj.HorizontalAlignment = HorizontalAlignment.Left;
        bidAskSpreadTextObj.VerticalAlignment = VerticalAlignment.Bottom;
        _chartObjects[BID_ASK_KEY].Add(bidAskSpreadTextObj);
    }

    /// <summary>
    /// Public method to redraw Bid/Ask levels. Called from the main indicator
    /// on every current-bar update so the lines track live prices and move
    /// with the advancing last bar.
    /// </summary>
    public void RedrawBidAskLevels()
    {
        if (_config.ShowBidAskLevels)
        {
            DrawBidAskLevels();
        }

        if (_config.ShowMarketDepth)
        {
            DrawMarketDepth();
        }
    }

    /// <summary>
    /// Draws Market Depth (DOM) data showing passive orders at bid and ask levels.
    /// Displays bid entries below the current bid and ask entries above the current ask,
    /// formatted as "VOLUME @ PRICE". Positioned immediately after the Bid/Ask level lines.
    /// Updates on every tick.
    /// </summary>
    private void DrawMarketDepth()
    {
        if (!_config.ShowMarketDepth || _marketData == null)
            return;

        if (_bars == null || _bars.Count == 0)
            return;

        // Get market depth data
        MarketDepth depth = _marketData.GetMarketDepth(_symbol.Name);
        if (depth == null)
            return;

        // Calculate positioning: immediately after the Bid/Ask level lines (which are half-width)
        int lastBarIndex = _bars.Count - 1;
        int lineStartBar = lastBarIndex + _config.BidAskLevelGap;
        int lineEndBar = lineStartBar + (_config.BidAskLevelWidth / 2);
        int depthStartBar = lineEndBar + 1;

        // Draw Bid side (bid entries) - limited to MaxDepthLevels
        if (depth.BidEntries != null && depth.BidEntries.Count > 0)
        {
            int bidCount = Math.Min(_config.MaxDepthLevels, depth.BidEntries.Count);
            for (int i = 0; i < bidCount; i++)
            {
                MarketDepthEntry entry = depth.BidEntries[i];
                string text = $"{entry.Volume} @ {entry.Price.ToString($"F{_symbol.Digits}")}";
                string objectName = $"FP_DOM_Bid_{i}";

                ChartText textObj = _chart.DrawText(
                    objectName,
                    text,
                    depthStartBar,
                    entry.Price,
                    _config.BidLevelColor);
                textObj.FontSize = _config.FontSize;
                textObj.HorizontalAlignment = HorizontalAlignment.Left;
                textObj.VerticalAlignment = VerticalAlignment.Center;

                _chartObjects[BID_ASK_KEY].Add(textObj);
            }
        }

        // Draw Ask side (ask entries) - limited to MaxDepthLevels
        if (depth.AskEntries != null && depth.AskEntries.Count > 0)
        {
            int askCount = Math.Min(_config.MaxDepthLevels, depth.AskEntries.Count);
            for (int i = 0; i < askCount; i++)
            {
                MarketDepthEntry entry = depth.AskEntries[i];
                string text = $"{entry.Volume} @ {entry.Price.ToString($"F{_symbol.Digits}")}";
                string objectName = $"FP_DOM_Ask_{i}";

                ChartText textObj = _chart.DrawText(
                    objectName,
                    text,
                    depthStartBar,
                    entry.Price,
                    _config.AskLevelColor);
                textObj.FontSize = _config.FontSize;
                textObj.HorizontalAlignment = HorizontalAlignment.Left;
                textObj.VerticalAlignment = VerticalAlignment.Center;

                _chartObjects[BID_ASK_KEY].Add(textObj);
            }
        }
    }

    // ============================================
    // CLEANUP
    // ============================================

    /// <summary>
    /// Clears all chart objects for a specific bar time.
    /// CRITICAL: Must be called before redrawing to prevent memory leaks.
    /// </summary>
    /// <param name="barTime">Bar time to clear</param>
    public void ClearFootprint(DateTime barTime)
    {
        if (!_chartObjects.ContainsKey(barTime))
            return;

        foreach (ChartObject obj in _chartObjects[barTime])
        {
            _chart.RemoveObject(obj.Name);
        }

        _chartObjects[barTime].Clear();
    }

    /// <summary>
    /// Clears all footprint objects from the chart.
    /// </summary>
    public void ClearAll()
    {
        foreach (DateTime barTime in _chartObjects.Keys)
        {
            ClearFootprint(barTime);
        }

        _chartObjects.Clear();
    }

    /// <summary>
    /// Gets the count of tracked chart objects (for debugging/monitoring).
    /// </summary>
    /// <returns>Total number of tracked chart objects across all bars</returns>
    public int GetObjectCount()
    {
        int count = 0;
        foreach (List<ChartObject> list in _chartObjects.Values)
        {
            count += list.Count;
        }
        return count;
    }
}
