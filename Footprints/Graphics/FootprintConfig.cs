using cAlgo.API;

namespace Footprints.Graphics;

/// <summary>
/// Configuration for footprint rendering (colors, fonts, display options, candlestick styling, zoom control).
/// </summary>
public class FootprintConfig
{
    // ============================================
    // VOLUME TEXT COLORS
    // ============================================

    /// <summary>
    /// Color for buy-dominant price levels.
    /// </summary>
    public Color BuyColor { get; set; }

    /// <summary>
    /// Color for sell-dominant price levels.
    /// </summary>
    public Color SellColor { get; set; }

    /// <summary>
    /// Color for balanced price levels.
    /// </summary>
    public Color NeutralColor { get; set; }

    /// <summary>
    /// Color for buy imbalance markers.
    /// </summary>
    public Color ImbalanceBuyColor { get; set; }

    /// <summary>
    /// Color for sell imbalance markers.
    /// </summary>
    public Color ImbalanceSellColor { get; set; }

    /// <summary>
    /// Color for Point of Control line.
    /// </summary>
    public Color POCColor { get; set; }

    /// <summary>
    /// Color for Value Area box (semi-transparent).
    /// </summary>
    public Color ValueAreaColor { get; set; }

    // ============================================
    // CANDLESTICK COLORS
    // ============================================

    /// <summary>
    /// Body color for bullish (close > open) candlesticks.
    /// </summary>
    public Color BullishCandleColor { get; set; }

    /// <summary>
    /// Body color for bearish (close &lt; open) candlesticks.
    /// </summary>
    public Color BearishCandleColor { get; set; }

    /// <summary>
    /// Color for candlestick shadows (wicks/tails).
    /// </summary>
    public Color ShadowColor { get; set; }

    // ============================================
    // DISPLAY OPTIONS
    // ============================================

    /// <summary>
    /// Whether to display Point of Control line.
    /// </summary>
    public bool ShowPOC { get; set; }

    /// <summary>
    /// Whether to display Value Area box.
    /// </summary>
    public bool ShowValueArea { get; set; }

    /// <summary>
    /// Whether to highlight imbalanced price levels.
    /// </summary>
    public bool ShowImbalances { get; set; }

    /// <summary>
    /// Whether to show delta below the bar.
    /// </summary>
    public bool ShowDelta { get; set; }

    /// <summary>
    /// Whether to use gradient coloring (vs binary buy/sell).
    /// </summary>
    public bool UseColorGradient { get; set; }

    /// <summary>
    /// Whether to draw custom candlesticks (user should hide native candles).
    /// </summary>
    public bool ShowCustomCandlesticks { get; set; }

    /// <summary>
    /// Whether to enable dynamic Y-axis zoom for better footprint readability.
    /// </summary>
    public bool EnableAutoZoom { get; set; }

    /// <summary>
    /// Whether to display a legend in the top-right corner explaining colors and lines.
    /// </summary>
    public bool ShowLegend { get; set; }

    // ============================================
    // FONT SETTINGS
    // ============================================

    /// <summary>
    /// Font size for footprint text.
    /// </summary>
    public int FontSize { get; set; }

    /// <summary>
    /// Font family for footprint text.
    /// </summary>
    public string FontFamily { get; set; }

    // ============================================
    // RENDERING OPTIONS
    // ============================================

    /// <summary>
    /// Horizontal text offset in pixels.
    /// </summary>
    public int TextOffsetX { get; set; }

    /// <summary>
    /// POC line thickness in pixels.
    /// </summary>
    public int POCLineThickness { get; set; }

    /// <summary>
    /// Value Area rectangle opacity (0-255).
    /// </summary>
    public int ValueAreaOpacity { get; set; }

    /// <summary>
    /// Thickness for Value Area border line (1-5 pixels).
    /// Used when Value Area is drawn as border-only (not filled).
    /// </summary>
    public int ValueAreaBorderThickness { get; set; }

    /// <summary>
    /// Thickness of candlestick shadow lines in pixels.
    /// </summary>
    public int ShadowThickness { get; set; }

    /// <summary>
    /// Opacity for candlestick body rectangles (0-255).
    /// Allows footprint text to remain readable on top of candles.
    /// </summary>
    public int CandleBodyOpacity { get; set; }

    /// <summary>
    /// Extra padding percentage added above and below the visible footprint range.
    /// For example, 10.0 means 10% extra padding on each side.
    /// </summary>
    public double ZoomPaddingPercent { get; set; }

    /// <summary>
    /// Minimum number of price levels required before auto-zoom activates.
    /// Prevents unnecessary zooming on bars with very few levels.
    /// </summary>
    public int MinLevelsForAutoZoom { get; set; }

    /// <summary>
    /// Number of recent bars to use for auto-zoom calculation.
    /// Focuses zoom on the most recent bars to avoid issues when old bars
    /// are at vastly different price levels (e.g., $80,000 vs $31,000).
    /// Default: 20 bars. Lower values focus zoom on more recent price action.
    /// </summary>
    public int AutoZoomFocusBars { get; set; }

    // ============================================
    // BINNING OPTIONS
    // ============================================

    /// <summary>
    /// Number of equal-sized price bins to aggregate per bar (3-20).
    /// When greater than 0, price levels are grouped into N bins for improved readability.
    /// Set to 0 to disable binning and use legacy per-tick rendering.
    /// </summary>
    public int NumberOfBins { get; set; }

    /// <summary>
    /// Whether to draw colored background rectangles for each bin (heatmap style).
    /// </summary>
    public bool ShowBinRectangles { get; set; }

    /// <summary>
    /// Opacity for bin background rectangles (0-255).
    /// Lower values make the rectangles more transparent.
    /// </summary>
    public int BinRectangleOpacity { get; set; }

    /// <summary>
    /// Horizontal gap between bars as a percentage (0.0 to 0.30).
    /// Applied on each side of the bar, so 0.10 = 10% gap on each side = 80% bar width.
    /// </summary>
    public double HorizontalGapPercentage { get; set; }

    // ============================================
    // BID/ASK LEVELS OPTIONS
    // ============================================

    /// <summary>
    /// Whether to display Bid and Ask price levels as dashed lines to the right of the last bar.
    /// </summary>
    public bool ShowBidAskLevels { get; set; }

    /// <summary>
    /// Color for the Bid price level line.
    /// </summary>
    public Color BidLevelColor { get; set; }

    /// <summary>
    /// Color for the Ask price level line.
    /// </summary>
    public Color AskLevelColor { get; set; }

    /// <summary>
    /// Thickness in pixels for Bid/Ask level lines (1-5).
    /// </summary>
    public int BidAskLevelThickness { get; set; }

    /// <summary>
    /// Width of Bid/Ask level lines in number of bars.
    /// </summary>
    public int BidAskLevelWidth { get; set; }

    /// <summary>
    /// Gap in number of bars between the last footprint bar and the Bid/Ask level lines.
    /// </summary>
    public int BidAskLevelGap { get; set; }

    // ============================================
    // MARKET DEPTH (DOM) PROPERTIES
    // ============================================

    /// <summary>
    /// Whether to display Market Depth (DOM) next to the last bar.
    /// </summary>
    public bool ShowMarketDepth { get; set; }

    /// <summary>
    /// Maximum number of depth levels to display (bid + ask sides).
    /// </summary>
    public int MaxDepthLevels { get; set; }

    /// <summary>
    /// Width of the Market Depth column in number of bars.
    /// </summary>
    public int DepthColumnWidth { get; set; }

    // ============================================
    // CONSTANTS
    // ============================================

    /// <summary>
    /// Default font size for footprint text.
    /// </summary>
    public const int DEFAULT_FONT_SIZE = 12;

    /// <summary>
    /// Minimum font size allowed.
    /// </summary>
    public const int MIN_FONT_SIZE = 6;

    /// <summary>
    /// Maximum font size allowed.
    /// </summary>
    public const int MAX_FONT_SIZE = 20;

    /// <summary>
    /// Default candlestick body opacity.
    /// </summary>
    public const int DEFAULT_CANDLE_OPACITY = 80;

    /// <summary>
    /// Default shadow thickness.
    /// </summary>
    public const int DEFAULT_SHADOW_THICKNESS = 1;

    /// <summary>
    /// Default zoom padding percentage.
    /// </summary>
    public const double DEFAULT_ZOOM_PADDING = 10.0;

    /// <summary>
    /// Default number of bins per bar.
    /// </summary>
    public const int DEFAULT_NUMBER_OF_BINS = 5;

    /// <summary>
    /// Default bin rectangle opacity.
    /// </summary>
    public const int DEFAULT_BIN_RECTANGLE_OPACITY = 80;

    /// <summary>
    /// Default horizontal gap percentage (10% on each side).
    /// </summary>
    public const double DEFAULT_HORIZONTAL_GAP_PERCENTAGE = 0.10;

    /// <summary>
    /// Default Value Area border thickness in pixels.
    /// </summary>
    public const int DEFAULT_VALUE_AREA_BORDER_THICKNESS = 2;

    /// <summary>
    /// Default number of recent bars used for auto-zoom calculation.
    /// </summary>
    public const int DEFAULT_AUTO_ZOOM_FOCUS_BARS = 20;

    /// <summary>
    /// Default thickness for Bid/Ask level lines.
    /// </summary>
    public const int DEFAULT_BID_ASK_LEVEL_THICKNESS = 2;

    /// <summary>
    /// Default width (in bars) for Bid/Ask level lines.
    /// </summary>
    public const int DEFAULT_BID_ASK_LEVEL_WIDTH = 4;

    /// <summary>
    /// Default gap (in bars) between last footprint bar and Bid/Ask level lines.
    /// </summary>
    public const int DEFAULT_BID_ASK_LEVEL_GAP = 1;

    /// <summary>
    /// Creates default configuration.
    /// </summary>
    public FootprintConfig()
    {
        // Default volume text colors
        BuyColor = Color.Green;
        SellColor = Color.Red;
        NeutralColor = Color.Gray;
        ImbalanceBuyColor = Color.LimeGreen;
        ImbalanceSellColor = Color.OrangeRed;
        POCColor = Color.Yellow;
        ValueAreaColor = Color.FromArgb(60, 135, 206, 250); // Semi-transparent sky blue (increased visibility)

        // Default candlestick colors
        BullishCandleColor = Color.FromArgb(DEFAULT_CANDLE_OPACITY, 0, 180, 0);
        BearishCandleColor = Color.FromArgb(DEFAULT_CANDLE_OPACITY, 200, 0, 0);
        ShadowColor = Color.FromArgb(DEFAULT_CANDLE_OPACITY, 128, 128, 128);

        // Default display options
        ShowPOC = true;
        ShowValueArea = true;
        ShowImbalances = true;
        ShowDelta = true;
        UseColorGradient = true;
        ShowCustomCandlesticks = true;
        EnableAutoZoom = true;
        ShowLegend = true;

        // Default font settings
        FontSize = DEFAULT_FONT_SIZE;
        FontFamily = "Consolas";

        // Default rendering options
        TextOffsetX = 5;
        POCLineThickness = 2;
        ValueAreaOpacity = 60;
        ValueAreaBorderThickness = DEFAULT_VALUE_AREA_BORDER_THICKNESS;
        ShadowThickness = DEFAULT_SHADOW_THICKNESS;
        CandleBodyOpacity = DEFAULT_CANDLE_OPACITY;
        ZoomPaddingPercent = DEFAULT_ZOOM_PADDING;
        MinLevelsForAutoZoom = 3;
        AutoZoomFocusBars = DEFAULT_AUTO_ZOOM_FOCUS_BARS;

        // Default binning options
        NumberOfBins = DEFAULT_NUMBER_OF_BINS;
        ShowBinRectangles = true;
        BinRectangleOpacity = DEFAULT_BIN_RECTANGLE_OPACITY;
        HorizontalGapPercentage = DEFAULT_HORIZONTAL_GAP_PERCENTAGE;

        // Default Bid/Ask levels options
        ShowBidAskLevels = true;
        BidLevelColor = Color.Red;
        AskLevelColor = Color.Green;
        BidAskLevelThickness = DEFAULT_BID_ASK_LEVEL_THICKNESS;
        BidAskLevelWidth = DEFAULT_BID_ASK_LEVEL_WIDTH;
        BidAskLevelGap = DEFAULT_BID_ASK_LEVEL_GAP;

        // Default Market Depth options
        ShowMarketDepth = false;
        MaxDepthLevels = 5;
        DepthColumnWidth = 3;
    }

    /// <summary>
    /// Gets color for a price level based on buy/sell volume and gradient settings.
    /// </summary>
    /// <param name="buyVolume">Buy volume at level</param>
    /// <param name="sellVolume">Sell volume at level</param>
    /// <returns>Color for the level text</returns>
    public Color GetLevelColor(long buyVolume, long sellVolume)
    {
        if (buyVolume == 0 && sellVolume == 0)
            return NeutralColor;

        if (!UseColorGradient)
        {
            // Simple binary coloring
            if (buyVolume > sellVolume)
                return BuyColor;
            if (sellVolume > buyVolume)
                return SellColor;
            return NeutralColor;
        }

        // Gradient coloring based on ratio
        long total = buyVolume + sellVolume;
        if (total == 0)
            return NeutralColor;

        double buyRatio = (double)buyVolume / total;

        // Interpolate between sell and buy colors
        if (buyRatio > 0.6)
        {
            // Strong buy
            return InterpolateColor(BuyColor, NeutralColor, (buyRatio - 0.6) / 0.4);
        }
        else if (buyRatio < 0.4)
        {
            // Strong sell
            return InterpolateColor(SellColor, NeutralColor, (0.4 - buyRatio) / 0.4);
        }
        else
        {
            // Neutral zone
            return NeutralColor;
        }
    }

    /// <summary>
    /// Gets color for an imbalanced price level.
    /// </summary>
    /// <param name="buyVolume">Buy volume at level</param>
    /// <param name="sellVolume">Sell volume at level</param>
    /// <returns>Imbalance color</returns>
    public Color GetImbalanceColor(long buyVolume, long sellVolume)
    {
        return buyVolume > sellVolume ? ImbalanceBuyColor : ImbalanceSellColor;
    }

    /// <summary>
    /// Creates a color with the configured candlestick body opacity applied.
    /// </summary>
    /// <param name="baseColor">Base color without opacity adjustment</param>
    /// <returns>Color with CandleBodyOpacity applied</returns>
    public Color ApplyCandleOpacity(Color baseColor)
    {
        return Color.FromArgb(CandleBodyOpacity, baseColor.R, baseColor.G, baseColor.B);
    }

    /// <summary>
    /// Interpolates between two colors based on a factor (0.0 to 1.0).
    /// </summary>
    /// <param name="color1">First color</param>
    /// <param name="color2">Second color</param>
    /// <param name="factor">Interpolation factor (0.0 = color1, 1.0 = color2)</param>
    /// <returns>Interpolated color</returns>
    private Color InterpolateColor(Color color1, Color color2, double factor)
    {
        factor = System.Math.Max(0, System.Math.Min(1, factor)); // Clamp to [0, 1]

        int r = (int)(color1.R + (color2.R - color1.R) * factor);
        int g = (int)(color1.G + (color2.G - color1.G) * factor);
        int b = (int)(color1.B + (color2.B - color1.B) * factor);
        int a = (int)(color1.A + (color2.A - color1.A) * factor);

        return Color.FromArgb(a, r, g, b);
    }
}
