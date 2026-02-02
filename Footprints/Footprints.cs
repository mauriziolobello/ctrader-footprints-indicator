using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using Footprints.Domain;
using Footprints.Graphics;
using Footprints.Processing;

namespace cAlgo.Indicators;

/// <summary>
/// Footprint Indicator for cTrader
///
/// Displays bid/ask volume distribution within each candlestick using the uptick/downtick rule.
/// Shows Point of Control (POC), Value Area, and volume imbalances.
/// Includes custom candlestick rendering and dynamic Y-axis zoom for optimal readability.
/// Aggregates ticks into equal-sized bins for improved readability.
///
/// Version: 1.6.0
/// </summary>
[Indicator(AccessRights = AccessRights.None, IsOverlay = true)] 
public partial class Footprints : Indicator
{
    // ============================================
    // COMPONENTS
    // ============================================

    private FootprintBarBuilder _barBuilder;
    private FootprintRenderer _renderer;
    private FootprintConfig _config;

    // ============================================
    // STATE MANAGEMENT
    // ============================================

    // Cache: maps bar time to FootprintBar
    private readonly Dictionary<DateTime, FootprintBar> _footprintCache = new Dictionary<DateTime, FootprintBar>();

    // Tracks which bars have been processed
    private readonly HashSet<DateTime> _processedBars = new HashSet<DateTime>();

    // Last render time for throttling
    private DateTime _lastRenderTime = DateTime.MinValue;

    // Current bar being updated
    private DateTime _currentBarTime = DateTime.MinValue;

    // Last time zoom was applied (throttle zoom updates)
    private DateTime _lastZoomTime = DateTime.MinValue;

    // Zoom throttle interval in milliseconds
    private const int ZOOM_THROTTLE_MS = 300;

    // ============================================
    // INITIALIZATION
    // ============================================

    protected override void Initialize()
    {
        try
        {
            Print("\u2554\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2557");
            Print("\u2551  Footprints Indicator v1.6.0     \u2551");
            Print("\u2551  Uptick/Downtick Volume Analysis  \u2551");
            Print("\u255A\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u255D");

        // CRITICAL: Clear all cached data and chart objects on re-initialization
        // (parameter change or Refresh) to prevent phantom candles from old data
        if (_renderer != null)
        {
            _renderer.ClearAll();
            Print("[Init] Cleared all chart objects from previous run");
        }

        _footprintCache.Clear();
        _processedBars.Clear();
        _lastRenderTime = DateTime.MinValue;
        _currentBarTime = DateTime.MinValue;
        _lastZoomTime = DateTime.MinValue;
        Print("[Init] Cleared footprint cache and state from previous run");

        InitializeComponents();

        // Initialize LocalStorage persistence AFTER components
        InitializeStorage();

        Print($"Configuration:");
        Print($"  - Imbalance Threshold: {ImbalanceThreshold}%");
        Print($"  - Value Area: {ValueAreaPercentage}%");
        Print($"  - Max Bars to Display: {MaxBarsToDisplay}");
        Print($"  - Render Throttle: {RenderThrottleMs}ms");
        Print($"  - Custom Candlesticks: {ShowCustomCandlesticks}");
        Print($"  - Auto Zoom: {EnableAutoZoom}");
        Print($"  - Zoom Focus Bars: {AutoZoomFocusBars}");
        Print($"  - Show Legend: {ShowLegend}");
        Print($"  - Font Size: {FontSize}");
        Print($"  - Number of Bins: {NumberOfBins}");
        Print($"  - Show Bin Rectangles: {ShowBinRectangles}");
        Print($"  - Bin Rectangle Opacity: {BinRectangleOpacity}");
        Print($"  - Bar Gap: {BarGapPercentage}%");
        Print($"  - Show Bid/Ask Levels: {ShowBidAskLevels}");
        Print($"  - Show Market Depth: {ShowMarketDepth}");

        // CRITICAL: Force recalculation of recent bars after re-initialization
        // (parameter change or Refresh) to rebuild them from stored ticks.
        // Without this, only the current bar gets processed and historical bars
        // with stored ticks remain invisible until Calculate() is called for them.
        int lastBarIndex = Bars.Count - 1;
        int barsToRecalculate = Math.Min(20, lastBarIndex + 1); // Process last 20 bars or fewer if not enough bars exist
        int startIndex = Math.Max(0, lastBarIndex - barsToRecalculate + 1);

        Print($"[Init] Force recalculating {barsToRecalculate} recent bars from stored ticks...");
        for (int i = startIndex; i <= lastBarIndex; i++)
        {
            DateTime barTime = Bars.OpenTimes[i];
            ProcessBar(i, barTime, forceRebuild: false);
            _processedBars.Add(barTime);
        }
        Print($"[Init] Recalculation complete - {barsToRecalculate} bars processed");

            // CRITICAL FIX: Force Y-axis reset to prevent negative range bug when native candles are hidden
            // This ensures the Y-axis has a valid range even if user hides native candles via "Viewing options"
            ForceInitialYAxisReset();

            Print($"Initialization complete.");
        }
        catch (Exception ex)
        {
            Print($"[CRITICAL] Initialization failed: {ex.Message}");
            Print($"[CRITICAL] Stack trace: {ex.StackTrace}");
            throw; // Re-throw to ensure cTrader knows initialization failed
        }
    }

    /// <summary>
    /// Initializes all components (builder, renderer, config).
    /// </summary>
    private void InitializeComponents()
    {
        // Create configuration from parameters
        _config = new FootprintConfig
        {
            // Volume text colors
            BuyColor = BuyColor,
            SellColor = SellColor,
            NeutralColor = NeutralColor,
            ImbalanceBuyColor = ImbalanceBuyColor,
            ImbalanceSellColor = ImbalanceSellColor,
            POCColor = POCColor,
            ValueAreaColor = Color.FromArgb(60, ValueAreaColor.R, ValueAreaColor.G, ValueAreaColor.B),

            // Candlestick colors
            BullishCandleColor = BullishCandleColor,
            BearishCandleColor = BearishCandleColor,
            ShadowColor = CandleShadowColor,

            // Display options
            ShowPOC = ShowPOC,
            ShowValueArea = ShowValueArea,
            ShowImbalances = ShowImbalances,
            ShowDelta = ShowDelta,
            UseColorGradient = UseColorGradient,
            ShowCustomCandlesticks = ShowCustomCandlesticks,
            EnableAutoZoom = EnableAutoZoom,
            ShowLegend = ShowLegend,

            // Font settings
            FontSize = FontSize,

            // Rendering options
            ShadowThickness = ShadowThickness,
            CandleBodyOpacity = CandleBodyOpacity,
            ZoomPaddingPercent = ZoomPaddingPercent,
            AutoZoomFocusBars = AutoZoomFocusBars,
            ValueAreaBorderThickness = ValueAreaBorderThickness,

            // Binning options
            NumberOfBins = NumberOfBins,
            ShowBinRectangles = ShowBinRectangles,
            BinRectangleOpacity = BinRectangleOpacity,
            HorizontalGapPercentage = BarGapPercentage / 100.0,

            // Bid/Ask levels options
            ShowBidAskLevels = ShowBidAskLevels,
            BidLevelColor = BidLevelColor,
            AskLevelColor = AskLevelColor,
            BidAskLevelThickness = BidAskLevelThickness,
            BidAskLevelWidth = BidAskLevelWidth,
            BidAskLevelGap = BidAskLevelGap,

            // Market Depth options
            ShowMarketDepth = ShowMarketDepth,
            MaxDepthLevels = MaxDepthLevels,
            DepthColumnWidth = DepthColumnWidth
        };

        // Create builder and renderer
        _barBuilder = new FootprintBarBuilder(MarketData, Symbol);

        // Wire up tick classification callback for LocalStorage persistence.
        // Each newly classified tick from live MarketData is stored for reaggregation.
        _barBuilder.OnTickClassified = (timestamp, price, tickType) =>
        {
            TickClassification classification = ConvertTickTypeToClassification(tickType);
            StoreProcessedTick(timestamp, price, classification);
        };

        _renderer = new FootprintRenderer(Chart, _config, Symbol, Print);

        // Provide Bars reference for custom candlestick drawing
        _renderer.SetBarsReference(Bars);

        // Provide ChartArea reference for pixel-based dynamic zoom control
        // For overlay indicators, IndicatorArea represents the main chart area and has ChartArea properties
        _renderer.SetChartArea(IndicatorArea);

        // Provide MarketData reference for Market Depth (DOM) access
        _renderer.SetMarketDataReference(MarketData);

        // Subscribe to chart scroll events to re-apply zoom when user scrolls
        Chart.ScrollChanged += OnChartScrollChanged;
        Chart.ZoomChanged += OnChartZoomChanged;
    }

    // ============================================
    // CALCULATE (MAIN ENTRY POINT)
    // ============================================

    public override void Calculate(int index)
    {
        // Skip if outside visible range
        if (!IsBarVisible(index))
            return;

        DateTime barTime = Bars.OpenTimes[index];

        // Check if this is the current bar (real-time updates)
        bool isCurrentBar = index == Bars.Count - 1;

        if (isCurrentBar)
        {
            // Throttle real-time updates
            if (!ShouldRenderCurrentBar())
                return;

            _currentBarTime = barTime;
            ProcessBar(index, barTime, forceRebuild: true);
            _lastRenderTime = DateTime.Now;

            // Redraw Bid/Ask levels (they move with current bar and live prices)
            _renderer.RedrawBidAskLevels();

            // Apply dynamic zoom after rendering the current bar
            ApplyZoomIfNeeded();

            // Save storage periodically (the StoreProcessedTick callback handles
            // intermediate saves every SAVE_INTERVAL_TICKS, but also save on
            // each rendered update of the current bar for data safety)
            SaveStorageData();
        }
        else
        {
            // Historical bar - process once
            if (_processedBars.Contains(barTime))
                return;

            ProcessBar(index, barTime, forceRebuild: false);
            _processedBars.Add(barTime);
        }

        
        
    }

    // ============================================
    // PROCESSING
    // ============================================

    /// <summary>
    /// Processes a single bar: builds footprint and renders it.
    /// </summary>
    /// <param name="index">Bar index</param>
    /// <param name="barTime">Bar opening time</param>
    /// <param name="forceRebuild">Force rebuild from ticks (for real-time bar)</param>
    private void ProcessBar(int index, DateTime barTime, bool forceRebuild)
    {
        // Get or build footprint
        FootprintBar footprintBar = GetOrBuildFootprint(index, barTime, forceRebuild);

        if (footprintBar == null || footprintBar.PriceLevels.Count == 0)
            return;

        // Render footprint (includes custom candlestick if enabled)
        _renderer.RenderFootprint(footprintBar, index);
    }

    /// <summary>
    /// Gets footprint from cache or builds it from ticks.
    /// When stored ticks are available from LocalStorage, they are passed to the
    /// builder for reaggregation, enriching the bar with historical data that
    /// may not be available in the live tick buffer.
    /// </summary>
    /// <param name="index">Bar index</param>
    /// <param name="barTime">Bar opening time</param>
    /// <param name="forceRebuild">Force rebuild from ticks</param>
    /// <returns>FootprintBar object</returns>
    private FootprintBar GetOrBuildFootprint(int index, DateTime barTime, bool forceRebuild)
    {
        // Check cache (skip for force rebuild)
        if (!forceRebuild && _footprintCache.ContainsKey(barTime))
        {
            return _footprintCache[barTime];
        }

        // Retrieve stored ticks for this bar's time range (if available)
        IEnumerable<FootprintTickData> storedTicks = null;
        if (_tickStorage != null && _tickStorage.Count > 0)
        {
            DateTime barCloseTime = index + 1 < Bars.Count
                ? Bars.OpenTimes[index + 1]
                : barTime.AddMinutes(EstimateBarDurationMinutes());

            storedTicks = _tickStorage.GetTicksForBar(barTime, barCloseTime);

            // Validate stored ticks: discard if time gap is too large
            if (storedTicks != null && !AreStoredTicksValid(barTime, storedTicks))
            {
                storedTicks = null;
            }
        }

        // Build from ticks (pass NumberOfBins for bin aggregation, stored ticks for reaggregation)
        FootprintBar footprintBar = _barBuilder.BuildFromBar(Bars, index, ImbalanceThreshold, ValueAreaPercentage, NumberOfBins, storedTicks);

        // Update cache
        _footprintCache[barTime] = footprintBar;

        // Prune cache if too large
        PruneCacheIfNeeded();

        return footprintBar;
    }

    /// <summary>
    /// Estimates bar duration in minutes from consecutive bars.
    /// Used when the next bar time is not available (last bar).
    /// </summary>
    /// <returns>Estimated bar duration in minutes</returns>
    private int EstimateBarDurationMinutes()
    {
        if (Bars.Count >= 2)
        {
            TimeSpan duration = Bars.OpenTimes[1] - Bars.OpenTimes[0];
            return (int)duration.TotalMinutes;
        }

        return 1; // Default fallback
    }

    // ============================================
    // ZOOM CONTROL
    // ============================================

    /// <summary>
    /// Applies dynamic zoom if enough time has passed since last zoom update.
    /// Collects visible footprint bars and delegates to renderer.
    /// </summary>
    private void ApplyZoomIfNeeded()
    {
        if (!EnableAutoZoom)
            return;

        // Throttle zoom updates to avoid performance issues
        if (_lastZoomTime != DateTime.MinValue)
        {
            TimeSpan elapsed = DateTime.Now - _lastZoomTime;
            if (elapsed.TotalMilliseconds < ZOOM_THROTTLE_MS)
                return;
        }

        List<FootprintBar> visibleBars = GetVisibleFootprintBars();
        _renderer.ApplyDynamicZoom(visibleBars);
        _lastZoomTime = DateTime.Now;
    }

    /// <summary>
    /// Collects all cached footprint bars that are currently visible on the chart.
    /// </summary>
    /// <returns>List of visible footprint bars</returns>
    private List<FootprintBar> GetVisibleFootprintBars()
    {
        List<FootprintBar> visibleBars = new List<FootprintBar>();

        int firstVisible = Chart.FirstVisibleBarIndex;
        int lastVisible = Chart.LastVisibleBarIndex;

        // Clamp to valid range
        firstVisible = Math.Max(0, firstVisible);
        lastVisible = Math.Min(Bars.Count - 1, lastVisible);

        for (int i = firstVisible; i <= lastVisible; i++)
        {
            DateTime barTime = Bars.OpenTimes[i];
            if (_footprintCache.ContainsKey(barTime))
            {
                FootprintBar fpBar = _footprintCache[barTime];
                if (fpBar != null && fpBar.PriceLevels.Count > 0)
                {
                    visibleBars.Add(fpBar);
                }
            }
        }

        return visibleBars;
    }

    // ============================================
    // EVENT HANDLERS
    // ============================================

    /// <summary>
    /// Handles chart scroll events to re-apply dynamic zoom and redraw legend after scrolling.
    /// Also forces redraw of visible bars to update text positioning after Y-axis changes.
    /// </summary>
    private void OnChartScrollChanged(ChartScrollEventArgs args)
    {
        ApplyZoomIfNeeded();

        // Redraw legend to update position after scroll
        if (ShowLegend)
        {
            _renderer.RedrawLegend();
        }

        // Force redraw of visible bars to update Delta text offset after Y-axis zoom
        ForceRedrawVisibleBars();
    }

    /// <summary>
    /// Handles chart zoom events to re-apply dynamic zoom and redraw legend after user zooms.
    /// Forces redraw of all visible bars to update text positioning with new zoom level.
    /// </summary>
    private void OnChartZoomChanged(ChartZoomEventArgs args)
    {
        ApplyZoomIfNeeded();

        // Redraw legend to update position after zoom
        if (ShowLegend)
        {
            _renderer.RedrawLegend();
        }

        // Force redraw of visible bars to update Delta text offset with new zoom level
        ForceRedrawVisibleBars();
    }

    /// <summary>
    /// Forces redraw of all visible bars by clearing and re-rendering them.
    /// This ensures text positioning (Delta/POC/OHLC) updates correctly after zoom.
    /// </summary>
    private void ForceRedrawVisibleBars()
    {
        List<int> visibleIndices = new List<int>();

        // Collect visible bar indices
        for (int i = 0; i < Bars.Count; i++)
        {
            if (IsBarVisible(i))
            {
                visibleIndices.Add(i);
            }
        }

        // Redraw each visible bar immediately
        foreach (int index in visibleIndices)
        {
            DateTime barTime = Bars.OpenTimes[index];

            // Clear existing chart objects for this bar
            _renderer.ClearFootprint(barTime);

            // Remove from processed set
            _processedBars.Remove(barTime);

            // Force immediate redraw with updated offset
            ProcessBar(index, barTime, forceRebuild: false);

            // Mark as processed again
            _processedBars.Add(barTime);
        }
    }

    // ============================================
    // HELPERS
    // ============================================

    /// <summary>
    /// Checks if a bar is within the visible range on the chart.
    /// </summary>
    /// <param name="index">Bar index</param>
    /// <returns>True if visible</returns>
    private bool IsBarVisible(int index)
    {
        // Check if within max bars limit
        int currentIndex = Bars.Count - 1;
        if (currentIndex - index > MaxBarsToDisplay)
            return false;

        // Process all recent bars (within MaxBarsToDisplay) to allow stored ticks
        // from LocalStorage to rebuild historical bars after indicator reload.
        // Previously we also checked Chart.FirstVisibleBarIndex/LastVisibleBarIndex,
        // but that prevented historical bars from being rebuilt if they weren't
        // visible on screen when the indicator loaded.
        return true;
    }

    /// <summary>
    /// Determines if current bar should be rendered (throttling check).
    /// </summary>
    /// <returns>True if enough time has passed since last render</returns>
    private bool ShouldRenderCurrentBar()
    {
        if (_lastRenderTime == DateTime.MinValue)
            return true;

        TimeSpan elapsed = DateTime.Now - _lastRenderTime;
        return elapsed.TotalMilliseconds >= RenderThrottleMs;
    }

    /// <summary>
    /// Prunes cache to keep it within size limit.
    /// Removes oldest entries first.
    /// </summary>
    private void PruneCacheIfNeeded()
    {
        if (_footprintCache.Count <= MaxCacheSize)
            return;

        // Find oldest entries to remove
        var sortedEntries = new List<KeyValuePair<DateTime, FootprintBar>>(_footprintCache);
        sortedEntries.Sort((a, b) => a.Key.CompareTo(b.Key));

        int toRemove = _footprintCache.Count - MaxCacheSize;
        for (int i = 0; i < toRemove; i++)
        {
            var entry = sortedEntries[i];
            _footprintCache.Remove(entry.Key);
            _processedBars.Remove(entry.Key);
        }

        Print($"Cache pruned: removed {toRemove} old entries (size: {_footprintCache.Count}/{MaxCacheSize})");
    }

    /// <summary>
    /// Forces Y-axis reset using OHLC data to prevent negative range bug.
    /// Called during initialization to ensure valid Y-axis range even if native candles are hidden.
    /// </summary>
    private void ForceInitialYAxisReset()
    {
        try
        {
            if (Bars == null || Bars.Count == 0 || IndicatorArea == null)
                return;

            // Calculate Y range from last 50 bars OHLC data (not footprints)
            int barsToCheck = Math.Min(50, Bars.Count);
            int startIdx = Math.Max(0, Bars.Count - barsToCheck);

            double high = double.MinValue;
            double low = double.MaxValue;

            for (int i = startIdx; i < Bars.Count; i++)
            {
                if (Bars.HighPrices[i] > high) high = Bars.HighPrices[i];
                if (Bars.LowPrices[i] < low) low = Bars.LowPrices[i];
            }

            if (high > low)
            {
                // Add 5% padding
                double range = high - low;
                double padding = range * 0.05;
                double bottomY = low - padding;
                double topY = high + padding;

                IndicatorArea.SetYRange(bottomY, topY);
                Print($"[Init] Y-axis reset: {bottomY:F2} to {topY:F2} (range: {topY - bottomY:F2})");
            }
        }
        catch (Exception ex)
        {
            Print($"[Init] Y-axis reset failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates configuration from parameters (called when parameters change).
    /// </summary>
    private void UpdateConfigFromParameters()
    {
        // Volume text colors
        _config.BuyColor = BuyColor;
        _config.SellColor = SellColor;
        _config.NeutralColor = NeutralColor;
        _config.ImbalanceBuyColor = ImbalanceBuyColor;
        _config.ImbalanceSellColor = ImbalanceSellColor;
        _config.POCColor = POCColor;
        _config.ValueAreaColor = Color.FromArgb(60, ValueAreaColor.R, ValueAreaColor.G, ValueAreaColor.B);

        // Candlestick colors
        _config.BullishCandleColor = BullishCandleColor;
        _config.BearishCandleColor = BearishCandleColor;
        _config.ShadowColor = CandleShadowColor;

        // Display options
        _config.ShowPOC = ShowPOC;
        _config.ShowValueArea = ShowValueArea;
        _config.ShowImbalances = ShowImbalances;
        _config.ShowDelta = ShowDelta;
        _config.UseColorGradient = UseColorGradient;
        _config.ShowCustomCandlesticks = ShowCustomCandlesticks;
        _config.EnableAutoZoom = EnableAutoZoom;
        _config.ShowLegend = ShowLegend;

        // Font settings
        _config.FontSize = FontSize;

        // Rendering options
        _config.ShadowThickness = ShadowThickness;
        _config.CandleBodyOpacity = CandleBodyOpacity;
        _config.ZoomPaddingPercent = ZoomPaddingPercent;
        _config.AutoZoomFocusBars = AutoZoomFocusBars;
        _config.ValueAreaBorderThickness = ValueAreaBorderThickness;

        // Binning options
        _config.NumberOfBins = NumberOfBins;
        _config.ShowBinRectangles = ShowBinRectangles;
        _config.BinRectangleOpacity = BinRectangleOpacity;
        _config.HorizontalGapPercentage = BarGapPercentage / 100.0;

        // Bid/Ask levels options
        _config.ShowBidAskLevels = ShowBidAskLevels;
        _config.BidLevelColor = BidLevelColor;
        _config.AskLevelColor = AskLevelColor;
        _config.BidAskLevelThickness = BidAskLevelThickness;
        _config.BidAskLevelWidth = BidAskLevelWidth;
        _config.BidAskLevelGap = BidAskLevelGap;

        // Market Depth options
        _config.ShowMarketDepth = ShowMarketDepth;
        _config.MaxDepthLevels = MaxDepthLevels;
        _config.DepthColumnWidth = DepthColumnWidth;
    }
}
