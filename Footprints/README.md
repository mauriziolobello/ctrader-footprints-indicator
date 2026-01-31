# Footprints Indicator for cTrader

A professional footprint (volume profile) indicator for cTrader that displays bid/ask volume distribution within each candlestick using the uptick/downtick classification algorithm.

## Features

### Core Functionality
- **Tick Classification**: Uptick/downtick rule categorizes each tick as buy or sell volume
- **Bilateral Display**: Shows volume in "SELL | BUY" format for each price level
- **Point of Control (POC)**: Highlights the price level with highest volume
- **Value Area**: Displays the price range containing 70% of total volume (configurable)
- **Imbalance Detection**: Identifies levels where buy or sell volume dominates significantly

### Visual Elements
- Color-coded volume levels (gradient or binary mode)
- POC horizontal line marker
- Semi-transparent Value Area box
- Imbalance markers with distinct colors
- Delta display (total buy - sell volume)

## Installation

1. Copy the entire `Footprints` folder to your cTrader indicators directory:
   ```
   C:\Users\[YourName]\Documents\cAlgo\Sources\Indicators\
   ```

2. Build the project:
   ```bash
   cd Footprints/Footprints
   dotnet build
   ```

3. The compiled indicator will be available in cTrader as `Footprints.algo`

4. In cTrader, add the indicator to your chart via:
   - Right-click on chart → Add Indicator → Custom → Footprints

## Parameters

### Analysis
| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Imbalance Threshold | double | 300.0 | 150-1000 | Minimum ratio (%) for imbalance detection |
| Value Area | double | 70.0 | 50-90 | Percentage of volume for Value Area |
| Tick Size Override | double | 0.0 | 0+ | Manual tick size (0 = auto) |

### Binning
| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Number of Bins | int | 5 | 3-20 | Equal-sized price bins per bar |
| Show Bin Rectangles | bool | true | - | Show colored background per bin |
| Bin Rectangle Opacity | int | 80 | 20-200 | Background rectangle transparency |

### Display
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| Show POC | bool | true | Display Point of Control line |
| Show Value Area | bool | true | Display Value Area box |
| Show Imbalances | bool | true | Highlight imbalanced levels |
| Show Delta | bool | true | Show delta below bar |
| Use Color Gradient | bool | true | Gradient colors vs binary |
| Font Size | int | 8 | Text size (6-14) |
| Max Bars to Display | int | 100 | Visible bars limit (10-500) |

### Colors
| Parameter | Default | Usage |
|-----------|---------|-------|
| Buy Color | Green | Buy-dominant levels |
| Sell Color | Red | Sell-dominant levels |
| Neutral Color | Gray | Balanced levels |
| Imbalance Buy Color | LimeGreen | Buy imbalance marker |
| Imbalance Sell Color | OrangeRed | Sell imbalance marker |
| POC Color | Yellow | Point of Control line |
| Value Area Color | SkyBlue | Value Area box |

### Performance
| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Render Throttle | int | 500 | 100-2000 | Real-time update delay (ms) |
| Max Cache Size | int | 500 | 100-2000 | In-memory cache limit |

## Usage Guide

### Understanding the Display

**Bin Aggregation Mode** (default, 5 bins):

Instead of showing every individual price level (30-50+ lines of illegible text), the bar's High-Low range is divided into N equal-sized bins. All ticks within each bin are aggregated:

```
Bar: High=83050, Low=82950 (100 pips range)
Bins = 5, bin size = 20 pips each

Bin 5: 83030-83050 →   1340 │ 720
Bin 4: 83010-83030 →    890 │ 1120
Bin 3: 82990-83010 →    640 │ 1890   ← POC (highest volume bin)
Bin 2: 82970-82990 →    780 │ 1420   ◄─ Value Area Box
Bin 1: 82950-82970 →   1250 │ 890
─────────────────────────────────────
Delta: +150
```

Each bin also has a colored background rectangle (heatmap style) based on the buy/sell ratio.

**Legacy Mode** (Number of Bins = 0, shows every tick level):

```
Price    SELL  |  BUY      Markers
─────────────────────────────────────
1.0850    250  |   50    ← ║ Sell Imbalance
1.0849     80  |  120
1.0848    100  |  180    ← ═══ POC (max volume)
1.0847     60  |  140    ◄─ Value Area Box
1.0846     40  |  200    ← ║ Buy Imbalance
─────────────────────────────────────
Delta: +150
```

### Key Concepts

#### 1. Bin Aggregation
- Divides each bar's High-Low range into N equal-sized bins (default 5)
- Aggregates all tick volume within each bin
- Reduces 30-50+ illegible lines to 5 readable lines
- POC, Value Area, and imbalances are recalculated from bins
- Colored background rectangles provide heatmap visualization
- Adjustable from 3 to 20 bins per bar

#### 2. Uptick/Downtick Rule
- **Uptick** (price > previous): Classified as **Buy** volume
- **Downtick** (price < previous): Classified as **Sell** volume
- **Zero tick** (price = previous): Inherits previous classification

#### 3. Point of Control (POC)
- Price level (or bin) with the highest total volume
- Often represents a key support/resistance level
- Marked with horizontal yellow line at bin midpoint (bin mode) or price level (legacy)

#### 4. Value Area
- Price range containing 70% of total volume (default)
- Expands from POC upward and downward
- In bin mode: spans from lowest VA bin bottom to highest VA bin top
- Shown as semi-transparent box

#### 5. Imbalances
- Levels/bins where `max(buy, sell) / min(buy, sell) >= threshold`
- Default threshold: 300% (3x ratio)
- Example: 250 buy / 50 sell = 500% >= 300% -> Buy Imbalance
- Marked with vertical bar in imbalance color

#### 6. Delta
- Total buy volume - total sell volume for the entire bar
- Positive delta: More buying pressure
- Negative delta: More selling pressure

### Trading Applications

1. **Support/Resistance**: POC often acts as a strong price level
2. **Reversal Signals**: Large imbalances may indicate exhaustion
3. **Trend Confirmation**: Consistent delta direction confirms trend
4. **Breakout Validation**: High volume at breakout level validates move
5. **Absorption**: High volume with low price movement indicates absorption

## Architecture

### Project Structure
```
Footprints/
├── Domain/
│   ├── FootprintBar.cs      # Core entity representing a bar's footprint
│   ├── FootprintBin.cs      # Aggregated price bin for improved readability
│   └── PriceLevel.cs        # Price level with buy/sell volume
├── Processing/
│   ├── TickClassifier.cs    # Uptick/downtick algorithm
│   └── FootprintBarBuilder.cs # Builds footprint from ticks
├── Graphics/
│   ├── FootprintConfig.cs   # Rendering configuration
│   └── FootprintRenderer.cs # Chart visualization
├── Footprints.cs            # Main indicator class
└── Footprints.Parameters.cs # Parameter definitions
```

### Design Principles
- **SOLID Principles**: Single Responsibility, Dependency Inversion
- **Modular Architecture**: Separate namespaces for domain, processing, graphics
- **Resource Management**: Proper chart object tracking and cleanup
- **Performance**: Caching, throttling, lazy loading

## Performance Considerations

### Optimization Strategies
1. **Caching**: Processed bars stored in-memory (max 500 by default)
2. **Throttling**: Real-time bar updates limited to 500ms intervals
3. **Lazy Loading**: Only visible bars are processed and rendered
4. **Automatic Pruning**: Cache cleaned when exceeding size limit

### Recommended Settings
- **Short timeframes** (M1-M5): Max 50-100 bars displayed
- **Medium timeframes** (M15-H1): Max 100-200 bars displayed
- **Long timeframes** (H4-D1): Max 200-500 bars displayed

### Known Limitations
- Tick history buffer: cTrader keeps ~10k ticks in memory
- First tick: Cannot be classified (no previous reference)
- Large datasets: >200 bars may impact performance
- Tick volume: cTrader doesn't expose tick volume (uses tick count as proxy)

## Troubleshooting

### Indicator Not Loading
- Ensure project compiled successfully (`dotnet build`)
- Check cTrader logs for errors
- Verify all files are in correct directory structure

### No Volume Data Displayed
- Check that symbol has tick data available
- Try increasing Max Bars to Display parameter
- Verify timeframe is not too large (prefer M5-H1)

### Performance Issues
- Reduce Max Bars to Display (try 50)
- Increase Render Throttle (try 1000ms)
- Reduce Max Cache Size (try 250)

### Incorrect Colors
- Check color parameters in indicator settings
- Toggle "Use Color Gradient" to switch modes
- Verify imbalance threshold is appropriate (try 200-400%)

## Version History

See [CHANGELOG.md](CHANGELOG.md) for detailed version history.

## Technical Support

For issues, questions, or feature requests:
- Review CLAUDE.md for development guidelines
- Check CHANGELOG.md for recent changes
- Verify cTrader API compatibility

## License

This indicator is provided as-is for educational and trading purposes.

## Credits

Developed following professional cTrader development guidelines and SOLID principles.

---

**Version**: 1.5.9
**Last Updated**: 2026-01-31
**Compatibility**: cTrader (net6.0)
