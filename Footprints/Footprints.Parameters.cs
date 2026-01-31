using cAlgo.API;

namespace cAlgo.Indicators;

/// <summary>
/// Footprints indicator - Parameters partial class.
/// Contains all configurable parameters for the footprint indicator.
/// </summary>
public partial class Footprints : Indicator
{
    // ============================================
    // ANALYSIS PARAMETERS
    // ============================================

    [Parameter("Imbalance Threshold (%)", DefaultValue = 300.0, MinValue = 150.0, MaxValue = 1000.0, Group = "Analysis")]
    public double ImbalanceThreshold { get; set; }

    [Parameter("Value Area (%)", DefaultValue = 70.0, MinValue = 50.0, MaxValue = 90.0, Group = "Analysis")]
    public double ValueAreaPercentage { get; set; }

    [Parameter("Tick Size Override", DefaultValue = 0.0, MinValue = 0.0, Group = "Analysis")]
    public double TickSizeOverride { get; set; }

    // ============================================
    // DISPLAY PARAMETERS
    // ============================================

    [Parameter("Show POC", DefaultValue = true, Group = "Display")]
    public bool ShowPOC { get; set; }

    [Parameter("Show Value Area", DefaultValue = true, Group = "Display")]
    public bool ShowValueArea { get; set; }

    [Parameter("Show Imbalances", DefaultValue = true, Group = "Display")]
    public bool ShowImbalances { get; set; }

    [Parameter("Show Delta", DefaultValue = true, Group = "Display")]
    public bool ShowDelta { get; set; }

    [Parameter("Use Color Gradient", DefaultValue = true, Group = "Display")]
    public bool UseColorGradient { get; set; }

    [Parameter("Font Size", DefaultValue = 12, MinValue = 6, MaxValue = 20, Group = "Display")]
    public int FontSize { get; set; }

    [Parameter("Show Legend", Group = "Display", DefaultValue = true)]
    public bool ShowLegend { get; set; }

    [Parameter("Value Area Border Thickness", Group = "Display", DefaultValue = 2, MinValue = 1, MaxValue = 5)]
    public int ValueAreaBorderThickness { get; set; }

    [Parameter("Max Bars to Display", DefaultValue = 100, MinValue = 10, MaxValue = 500, Group = "Display")]
    public int MaxBarsToDisplay { get; set; }

    // ============================================
    // CANDLESTICK PARAMETERS
    // ============================================

    [Parameter("Show Custom Candlesticks", DefaultValue = true, Group = "Candlesticks")]
    public bool ShowCustomCandlesticks { get; set; }

    [Parameter("Bullish Candle Color", DefaultValue = "Green", Group = "Candlesticks")]
    public Color BullishCandleColor { get; set; }

    [Parameter("Bearish Candle Color", DefaultValue = "Red", Group = "Candlesticks")]
    public Color BearishCandleColor { get; set; }

    [Parameter("Shadow Color", DefaultValue = "Gray", Group = "Candlesticks")]
    public Color CandleShadowColor { get; set; }

    [Parameter("Shadow Thickness", DefaultValue = 1, MinValue = 1, MaxValue = 3, Group = "Candlesticks")]
    public int ShadowThickness { get; set; }

    [Parameter("Candle Body Opacity", DefaultValue = 80, MinValue = 20, MaxValue = 200, Group = "Candlesticks")]
    public int CandleBodyOpacity { get; set; }

    // ============================================
    // BINNING PARAMETERS
    // ============================================

    [Parameter("Number of Bins", DefaultValue = 5, MinValue = 3, MaxValue = 20, Group = "Binning")]
    public int NumberOfBins { get; set; }

    [Parameter("Show Bin Rectangles", DefaultValue = true, Group = "Binning")]
    public bool ShowBinRectangles { get; set; }

    [Parameter("Bin Rectangle Opacity", DefaultValue = 80, MinValue = 20, MaxValue = 200, Group = "Binning")]
    public int BinRectangleOpacity { get; set; }

    [Parameter("Bar Gap (%)", Group = "Display", DefaultValue = 10.0, MinValue = 0.0, MaxValue = 30.0)]
    public double BarGapPercentage { get; set; }

    // ============================================
    // ZOOM PARAMETERS
    // ============================================

    [Parameter("Enable Auto Zoom", DefaultValue = true, Group = "Zoom")]
    public bool EnableAutoZoom { get; set; }

    [Parameter("Zoom Padding (%)", DefaultValue = 10.0, MinValue = 0.0, MaxValue = 50.0, Group = "Zoom")]
    public double ZoomPaddingPercent { get; set; }

    [Parameter("Zoom Focus Bars", DefaultValue = 20, MinValue = 5, MaxValue = 100, Group = "Zoom")]
    public int AutoZoomFocusBars { get; set; }

    // ============================================
    // COLOR PARAMETERS
    // ============================================

    [Parameter("Buy Color", DefaultValue = "Green", Group = "Colors")]
    public Color BuyColor { get; set; }

    [Parameter("Sell Color", DefaultValue = "Red", Group = "Colors")]
    public Color SellColor { get; set; }

    [Parameter("Neutral Color", DefaultValue = "Gray", Group = "Colors")]
    public Color NeutralColor { get; set; }

    [Parameter("Imbalance Buy Color", DefaultValue = "LimeGreen", Group = "Colors")]
    public Color ImbalanceBuyColor { get; set; }

    [Parameter("Imbalance Sell Color", DefaultValue = "OrangeRed", Group = "Colors")]
    public Color ImbalanceSellColor { get; set; }

    [Parameter("POC Color", DefaultValue = "Yellow", Group = "Colors")]
    public Color POCColor { get; set; }

    [Parameter("Value Area Color", DefaultValue = "SkyBlue", Group = "Colors")]
    public Color ValueAreaColor { get; set; }

    // ============================================
    // BID/ASK LEVELS PARAMETERS
    // ============================================

    [Parameter("Show Bid/Ask Levels", Group = "Bid/Ask Levels", DefaultValue = true)]
    public bool ShowBidAskLevels { get; set; }

    [Parameter("Bid Level Color", Group = "Bid/Ask Levels", DefaultValue = "Red")]
    public Color BidLevelColor { get; set; }

    [Parameter("Ask Level Color", Group = "Bid/Ask Levels", DefaultValue = "Green")]
    public Color AskLevelColor { get; set; }

    [Parameter("Line Thickness", Group = "Bid/Ask Levels", DefaultValue = 2, MinValue = 1, MaxValue = 5)]
    public int BidAskLevelThickness { get; set; }

    [Parameter("Line Width (bars)", Group = "Bid/Ask Levels", DefaultValue = 4, MinValue = 2, MaxValue = 10)]
    public int BidAskLevelWidth { get; set; }

    [Parameter("Gap from Last Bar", Group = "Bid/Ask Levels", DefaultValue = 1, MinValue = 0, MaxValue = 5)]
    public int BidAskLevelGap { get; set; }

    // ============================================
    // MARKET DEPTH (DOM) PARAMETERS
    // ============================================

    [Parameter("Show Market Depth", Group = "Market Depth", DefaultValue = false)]
    public bool ShowMarketDepth { get; set; }

    [Parameter("Max Depth Levels", Group = "Market Depth", DefaultValue = 5, MinValue = 3, MaxValue = 10)]
    public int MaxDepthLevels { get; set; }

    [Parameter("Depth Column Width (bars)", Group = "Market Depth", DefaultValue = 3, MinValue = 2, MaxValue = 5)]
    public int DepthColumnWidth { get; set; }

    // ============================================
    // PERFORMANCE PARAMETERS
    // ============================================

    [Parameter("Render Throttle (ms)", DefaultValue = 500, MinValue = 100, MaxValue = 2000, Group = "Performance")]
    public int RenderThrottleMs { get; set; }

    [Parameter("Max Cache Size", DefaultValue = 500, MinValue = 100, MaxValue = 2000, Group = "Performance")]
    public int MaxCacheSize { get; set; }
}
