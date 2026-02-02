# Changelog - Footprints Indicator

All notable changes to the Footprints indicator will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.7.0] - 2026-02-02

### Added
- **Comprehensive Bar Information Display**: Delta text now includes Ask/Bid totals, POC price, OHLC values, and timestamp in a single consolidated display below each candlestick
  - Delta value with color-coded indicator (green/red/neutral)
  - Ask total volume (sum of all buy volumes across the bar)
  - Bid total volume (sum of all sell volumes across the bar)
  - POC (Point of Control) price for quick reference
  - Complete OHLC values (Open, High, Low, Close) formatted to symbol digits
  - Timestamp in compact format (ddMMyy HH:mm) for precise bar identification
  - All information centered under the candlestick section for clarity
  - Font size optimized (FontSize) for readability alongside other chart elements

### Changed
- **POC Line Width**: POC line now limited to central candlestick section only (between sell and buy columns) instead of spanning the full bar width
  - Prevents overlap with volume numbers in bin columns
  - Changed from `DotsRare` back to `Solid` line style for better visibility
  - Improves chart readability by keeping POC line contained to price action area
- **Delta Text Positioning**: Enhanced positioning system using pixel-based offset for consistent visual spacing
  - Offset calculated as fixed 100 pixels converted to price at current zoom level
  - Maintains constant visual distance regardless of Y-axis zoom changes
  - Positioned exactly centered under the candlestick section using DateTime coordinates
  - Uses `GetBarDateTime(barIndex, (SELL_SECTION_END + BUY_SECTION_START) / 2.0)` for precise centering
- **Market Depth (DOM) Layout**: Refined positioning for better visual hierarchy
  - Grid spacing calculated as 0.08% of current price for scale independence
  - Oblique connector lines from actual price levels to fixed grid positions
  - Text positioned with 1-bar offset after connector lines for clear separation
  - DOM block positioned closer to Bid/Ask lines (removed gap)
  - Shorter diagonal connectors (2 bars instead of 3) for more compact layout
  - Uses `VolumeInUnits` instead of deprecated `Volume` property

### Fixed
- **Zoom-Induced Text Overlap**: Fixed critical issue where Delta/POC/OHLC text would overlap with candlesticks during Y-axis zoom operations
  - Added `ForceRedrawVisibleBars()` method that clears and re-renders all visible bars
  - Subscribed to `Chart.ScrollChanged` event (in addition to existing `ZoomChanged`) to capture vertical zoom operations
  - When zoom occurs, all visible bars are immediately redrawn with updated pixel-to-price offset
  - Offset recalculated per bar using current zoom level via `Math.Abs(_chart.PixelsToPrice(100))`
  - Ensures text maintains 100-pixel visual distance from candlestick across all zoom levels
- **Y-Axis Reset on Initialization**: Fixed issue where chart could display with incorrect Y-axis range after refresh/restart
  - `ForceInitialYAxisReset()` method calculates Y range from last 50 bars' OHLC data
  - Forces `SetYRange()` during initialization independent of footprints or auto-zoom
  - Resolves problem where cTrader loses Y-axis reference for overlay indicators when native candles are hidden
  - Works reliably across different broker installations and cTrader configurations

### Technical Details
- `FootprintRenderer.DrawDelta()` now builds multi-line text using `StringBuilder`:
  - Line 1: Delta with Unicode Delta symbol (`\u0394`)
  - Line 2: Ask total volume (`footprintBar.TotalBuyVolume`)
  - Line 3: Bid total volume (`footprintBar.TotalSellVolume`)
  - Line 4: POC price formatted to symbol digits
  - Lines 5-8: OHLC values, each on separate line
  - Line 9: Timestamp in compact format `ddMMyy HH:mm`
- `FootprintRenderer.DrawBinPOCLine()` and `DrawPOCLine()` modified:
  - Line start/end changed from `SELL_SECTION_START/BUY_SECTION_END` to `SELL_SECTION_END/BUY_SECTION_START`
  - `LineStyle` changed from `DotsRare` to `Solid`
  - Removed POC price text that was previously drawn above the line
- `FootprintRenderer.DrawDelta()` offset calculation:
  - Changed from fixed price-based offset (`_symbol.PipSize * X`) to pixel-based: `Math.Abs(_chart.PixelsToPrice(100))`
  - Uses `footprintBar.Low` directly instead of calculating from bins (more reliable)
  - Text positioning uses `GetBarDateTime()` for precise centering under candlestick section
- `Footprints.cs` zoom event handling:
  - New `ForceRedrawVisibleBars()` method identifies and redraws all visible bars
  - Iterates visible bar indices, calls `ProcessBar()` for each with `forceRebuild: false`
  - Clears chart objects before redraw, removes from `_processedBars`, then re-adds
  - Called from both `OnChartScrollChanged()` and `OnChartZoomChanged()` event handlers
- `Footprints.cs` now uses `Chart.ScrollChanged` event:
  - Captures Y-axis changes (topY/bottomY) that occur during vertical zoom
  - Complements existing `Chart.ZoomChanged` event for complete zoom coverage
- `FootprintRenderer.DrawMarketDepth()` refinements:
  - `gridSpacing = basePrice * 0.0008` (0.08% of current price)
  - `depthStartBar = lineEndBar` (removed gap)
  - `depthTextBar = depthStartBar + 2` (shorter lines)
  - Text at `depthTextBar + 1` with `HorizontalAlignment.Left`
  - Uses `entry.VolumeInUnits` instead of deprecated `entry.Volume`
- Added `using System.Text;` for `StringBuilder` support
- Added `using LibraryExtensions;` for extension methods (user-added)
- Added `using System.Net.Security;` (user-added, may be unused)

### Performance
- Redraw operations now limited to visible bars only for efficiency
- `ForceRedrawVisibleBars()` uses `IsBarVisible()` check before processing
- Zoom event throttling remains at 300ms to prevent excessive redraws

## [1.6.0] - 2026-01-31

### Added
- **Market Depth (DOM) Display**: Shows real-time order book depth (passive orders at bid and ask levels) next to the last footprint bar, providing insight into where limit orders are stacked in the market.
  - Bid entries (buy orders) displayed below the current bid price in red
  - Ask entries (sell orders) displayed above the current ask price in green
  - Format: "VOLUME @ PRICE" for each depth level
  - Positioned to the right of Bid/Ask level lines in the same column
  - Updates on every tick with live market depth data
  - Configurable maximum number of levels to display (default 5, range 3-10)
  - Uses `MarketData.GetMarketDepth(Symbol.Name)` API to retrieve DOM data from broker
  - Toggle on/off via `Show Market Depth` parameter (default false - opt-in feature)
- **New Parameters (Market Depth group)**:
  - `Show Market Depth` (bool, default false) - toggle DOM display
  - `Max Depth Levels` (int, 3-10, default 5) - maximum number of bid/ask levels to show
  - `Depth Column Width (bars)` (int, 2-5, default 3) - horizontal width reserved for depth display

### Technical Details
- New `FootprintConfig` properties: `ShowMarketDepth`, `MaxDepthLevels`, `DepthColumnWidth`
- New `FootprintRenderer._marketData` private field for MarketData reference
- New `FootprintRenderer.SetMarketDataReference(MarketData)` method for dependency injection
- New `FootprintRenderer.DrawMarketDepth()` private method retrieves and renders DOM data
- `FootprintRenderer.RedrawBidAskLevels()` now calls `DrawMarketDepth()` when enabled
- Market depth chart objects tracked under existing `BID_ASK_KEY` for cleanup with Bid/Ask lines
- `Footprints.cs` calls `_renderer.SetMarketDataReference(MarketData)` in `InitializeComponents()`
- `Footprints.Parameters.cs` three new parameters in "Market Depth" group
- `Footprints.cs` wires all three Market Depth parameters to `_config` in both `InitializeComponents()` and `UpdateConfigFromParameters()`
- DOM entries limited to `Math.Min(MaxDepthLevels, depth.BidEntries.Count)` and `Math.Min(MaxDepthLevels, depth.AskEntries.Count)` to respect user preference
- Text positioned at `depthStartBar = lastBarIndex + BidAskLevelGap + BidAskLevelWidth + 1` (right of Bid/Ask lines)

## [1.5.9] - 2026-01-31

### Changed
- **Delta Text Positioning**: Further increased Delta text spacing below bar groups for better visual separation
  - Offset doubled from `TickSize * 6` to `TickSize * 12`
  - Delta now clearly separated from bottom bin with significant whitespace
  - Prevents any visual overlap or crowding

### Technical Details
- `FootprintRenderer.DrawDelta()`: offset changed from `_symbol.TickSize * 6` to `_symbol.TickSize * 12`

## [1.5.8] - 2026-01-31

### Changed
- **Delta Text Positioning**: Moved Delta text lower below the bar group for better spacing and readability
  - Offset increased from `TickSize * 3` to `TickSize * 6`
  - Prevents overlap with bottom bin volume numbers

### Reverted
- **Auto-Zoom Default**: Reverted `Enable Auto Zoom` default back to `true` (from v1.5.7 temporary change to `false`)
  - The stacking issue in Trade environment was specific to one cTrader installation, not a universal problem
  - Other cTrader installations (including broker-branded versions) work correctly with auto-zoom enabled
  - Issue appears to be environment-specific (graphics drivers, display scaling, or cTrader cache)
- **Removed Warning**: Removed auto-zoom warning from initialization log (added in v1.5.7, no longer needed)

### Technical Details
- `FootprintRenderer.DrawDelta()`: offset changed from `_symbol.TickSize * 3` to `_symbol.TickSize * 6`
- `Footprints.Parameters.cs`: `EnableAutoZoom` DefaultValue reverted from `false` to `true`
- `Footprints.cs` `Initialize()`: Removed conditional warning for auto-zoom

## [1.5.7] - 2026-01-31

### Fixed
- **Trade Environment Compatibility**: Fixed issue where indicator displayed incorrectly in cTrader Trade environment with all bars stacked vertically. This is due to a cTrader limitation where `ChartArea.SetYRange()` is ignored in Trade but works in Algo (Automate) environment.
  - Changed `Enable Auto Zoom` default value from `true` to **`false`** for Trade environment compatibility
  - Added warning in initialization log when auto-zoom is enabled: "WARNING: Auto Zoom may not work in Trade environment"
  - Users in Algo environment can manually enable auto-zoom if desired
  - Same limitation previously encountered with Cumulative Volume Delta indicator

### Added
- **CLAUDE.md Documentation**: Added section documenting `ChartArea.SetYRange()` limitation between Algo and Trade environments with code examples and best practices

### Technical Details
- `Footprints.Parameters.cs`: `EnableAutoZoom` DefaultValue changed from `true` to `false`
- `Footprints.cs` `Initialize()`: Added conditional warning when `EnableAutoZoom` is `true`
- `CLAUDE.md`: New section "ChartArea.SetYRange() - Limitazione Trade Environment" with detailed explanation

## [1.5.6] - 2026-01-31

### Changed
- **Reduced Log Verbosity**: Commented out debug logging for `[Storage]` and `[AutoZoom]` operations to reduce log clutter. Error logs are still active for troubleshooting.
  - `[Storage]` informational logs commented out: key generation, loading, saving, gap detection
  - `[Storage]` error logs kept active: loading errors, saving errors
  - `[AutoZoom]` diagnostic logs commented out: bar count, chart height, pixel calculations, zoom range
  - Logs can be re-enabled by uncommenting the respective Print statements for debugging

### Technical Details
- `Footprints.Storage.cs`: Commented out 8 informational Print statements, kept 2 error Print statements
- `FootprintRenderer.cs`: Commented out 5 AutoZoom diagnostic Print statements
- All commented logs include inline comments: "Keep error logs" or "enable for debugging"

## [1.5.5] - 2026-01-31

### Fixed
- **Critical: LocalStorage Key Validation Error**: Fixed storage error "Le chiavi possono contenere solamente caratteri latini, numeri e spazi" (Keys can only contain Latin characters, numbers and spaces) that prevented tick data from being saved to LocalStorage. The underscore character `_` in the storage key was not allowed by cTrader's LocalStorage validation.
  - Changed storage key format from `"Footprint_BTCUSD"` to `"Footprint BTCUSD"` (space instead of underscore)
  - Tick data can now be successfully persisted to LocalStorage without errors
  - Historical bars will rebuild correctly from stored ticks after indicator reload
  - Error log spam eliminated: `[Storage] Error saving: Le chiavi possono contenere solamente caratteri latini, numeri e spazi...`

### Technical Details
- `FootprintTickStorage.GenerateStorageKey()` changed separator from underscore to space
- Storage key example: `"EUR/USD"` → `"Footprint EURUSD"` (was `"Footprint_EURUSD"`)
- Comment updated: "Uses space separator as required by cTrader LocalStorage (underscores not allowed)"

## [1.5.4] - 2026-01-31

### Fixed
- **Historical Bars Missing After Parameter Change**: Fixed critical issue where only the current bar would remain visible after changing a parameter, despite having stored ticks in LocalStorage for multiple historical bars. This occurred because cTrader only calls `Calculate()` for the current bar after a parameter change, leaving historical bars unprocessed even though their stored ticks were available.
  - Added forced recalculation of the last 20 bars at the end of `Initialize()` after LocalStorage is loaded
  - Historical bars are now immediately rebuilt from stored ticks when the indicator is re-initialized
  - Example: After running for 15+ minutes on M5 timeframe, changing any parameter now preserves all visible bars instead of showing only 1
  - Diagnostic logging: `[Init] Force recalculating N recent bars from stored ticks...` and `[Init] Recalculation complete - N bars processed`

### Changed
- **POC Line Style**: Changed POC (Point of Control) line from `LineStyle.Solid` to `LineStyle.DotsRare` to prevent overlap with volume numbers, improving readability
  - Applies to both bin-based mode (`DrawBinPOCLine()`) and legacy mode (`DrawPOCLine()`)
  - The dotted line is less visually intrusive and doesn't obscure the volume text

### Technical Details
- `Footprints.cs` `Initialize()` now performs forced recalculation AFTER `InitializeStorage()`:
  - Iterates through the last 20 bars (or fewer if not enough bars exist)
  - Calls `ProcessBar(i, barTime, forceRebuild: false)` for each bar to rebuild from stored ticks
  - Adds each processed bar to `_processedBars` to prevent double-processing in `Calculate()`
  - Uses `Math.Max(0, lastBarIndex - 19)` to handle cases with fewer than 20 bars
- `FootprintRenderer.DrawBinPOCLine()` `LineStyle` changed from `Solid` to `DotsRare`
- `FootprintRenderer.DrawPOCLine()` `LineStyle` changed from `Solid` to `DotsRare`

## [1.5.3] - 2026-01-31

### Changed
- **Increased Bar Group Spacing**: Increased horizontal gap between consecutive bar groups (bins+candlestick aggregates) for better visual separation
  - Left gap increased from 2% to 10% (`SELL_SECTION_START` from 0.02 to 0.10)
  - Right gap increased from 2% to 10% (`BUY_SECTION_END` from 0.98 to 0.90)
  - Total gap between consecutive bars is now 20% (10% at the end of one bar + 10% at the start of the next)
  - Improves visual clarity and makes it easier to distinguish individual bar groups

### Technical Details
- `FootprintRenderer.cs` constants updated:
  - `SELL_SECTION_START` changed from 0.02 to 0.10
  - `BUY_SECTION_END` changed from 0.98 to 0.90
- Each bar now uses 80% of its time width (10%-90%) instead of 96% (2%-98%), creating visible spacing between bars

## [1.5.2] - 2026-01-31

### Fixed
- **Phantom Candle on Parameter Change/Refresh**: Fixed issue where changing a parameter or forcing a manual Refresh would display a "phantom candle" at a distant price level (e.g., a bar far away from current price action). This occurred because the footprint cache (`_footprintCache`) and processed bar tracking (`_processedBars`) were not being cleared on re-initialization, causing old cached bars from previous runs to be re-rendered at their original (stale) price levels.
  - Added explicit cache clearing at the start of `Initialize()` before components are re-initialized
  - All chart objects from the previous run are now cleared via `_renderer.ClearAll()` on re-initialization
  - State variables (`_lastRenderTime`, `_currentBarTime`, `_lastZoomTime`) are reset to `DateTime.MinValue`
  - The issue did NOT occur on initial compilation because caches started empty; it only manifested on parameter changes or Refresh operations where the indicator instance persisted
  - This fix complements the existing gap detection and outlier filtering logic (v1.4.1) by preventing stale cached bars from being rendered in the first place
- **Only One Candle Visible After Reload**: Fixed issue where only the current bar would be rendered after recompiling/reloading the indicator, even though LocalStorage contained stored ticks for multiple recent bars. This occurred because `IsBarVisible()` was checking `Chart.FirstVisibleBarIndex/LastVisibleBarIndex`, preventing historical bars from being processed if they weren't visible on screen when the indicator loaded.
  - Removed chart viewport visibility check from `IsBarVisible()` so all bars within `MaxBarsToDisplay` are processed regardless of scroll position
  - Historical bars with stored ticks are now properly rebuilt from LocalStorage data on indicator reload
  - Example: After running for 15+ minutes on M5 timeframe, reloading now shows 3+ bars instead of just 1

### Changed
- **Bid/Ask/Spread Text Size**: Increased font size by 2 points (`FontSize + 2`) for better readability of the Bid/Ask/Spread text label
- **Bid/Ask/Spread Text Format**: Combined into single multi-line text object using newline character: `"Bid=xxx - Ask=yyy\nSpread=xx.x pips"` instead of two separate text objects

### Technical Details
- `Footprints.cs` `Initialize()` now performs cache clearing BEFORE calling `InitializeComponents()`:
  - `_renderer.ClearAll()` removes all chart objects from the previous run (if renderer exists)
  - `_footprintCache.Clear()` removes all cached footprint bars
  - `_processedBars.Clear()` removes all processed bar timestamps
  - State variables reset: `_lastRenderTime`, `_currentBarTime`, `_lastZoomTime` = `DateTime.MinValue`
- Diagnostic logging added: `[Init] Cleared all chart objects from previous run` and `[Init] Cleared footprint cache and state from previous run`
- `Footprints.cs` `IsBarVisible()` now only checks `MaxBarsToDisplay` distance from current bar, removed `Chart.FirstVisibleBarIndex/LastVisibleBarIndex` check
- `FootprintRenderer.DrawBidAskLevels()` now creates single `ChartText` with multi-line string instead of two separate text objects
- `FootprintRenderer.DrawBidAskLevels()` text font size changed from `_config.FontSize` to `_config.FontSize + 2`

## [1.5.1] - 2026-01-31

### Added
- **Bid/Ask Text Labels**: Added text labels displaying current Bid/Ask prices and spread in pips, positioned to the right of the Bid/Ask level lines
  - "Bid=xxxxx, Ask=yyyyy" text label positioned above the Bid line (white color, right-aligned)
  - "Spread=xx.x pips" text label positioned below the Bid line (white color, right-aligned)
  - Both labels positioned at the end of the level lines (`lineEndBar` position) with 3-tick vertical offset
  - Spread calculated as `(Symbol.Ask - Symbol.Bid) / Symbol.PipSize` and formatted to 1 decimal place
  - Bid/Ask prices formatted to the symbol's native digit precision (e.g., F5 for EURUSD)
  - Text labels update live with the Bid/Ask level lines on every current-bar tick

### Technical Details
- `FootprintRenderer.DrawBidAskLevels()` now creates two additional `ChartText` objects tracked under `BID_ASK_KEY`:
  - `"FP_BidAskText"` for the Bid/Ask price values label
  - `"FP_SpreadText"` for the spread pips label
- Text positioned using `Chart.DrawText(name, text, lineEndBar, price +/- offset, Color.White)` with `HorizontalAlignment.Right`
- Vertical offset is `_symbol.TickSize * 3` above and below the Bid price for positioning the two text labels
- Bid/Ask prices formatted using `F{_symbol.Digits}` for correct decimal precision per instrument
- Spread formatted using `F1` for single decimal place (e.g., "2.5 pips")

## [1.5.0] - 2026-01-31

### Added
- **Bid/Ask Price Levels**: Draws Bid and Ask price levels as dashed horizontal lines to the right of the last footprint bar, providing a clear visual reference for current market prices.
  - Bid line: dashed red line (configurable color) at the current Bid price
  - Ask line: dashed green line (configurable color) at the current Ask price
  - Lines span a configurable width (default 4 bars) with a configurable gap from the last bar (default 1 bar)
  - Lines update live with every current-bar tick, tracking real-time Bid/Ask prices
  - Line thickness configurable from 1 to 5 pixels (default 2)
  - Line style: `LineStyle.DotsRare` for dashed appearance
  - Toggle on/off via `Show Bid/Ask Levels` parameter
- **New Parameters (Bid/Ask Levels group)**:
  - `Show Bid/Ask Levels` (bool, default true) - toggle Bid/Ask level display
  - `Bid Level Color` (Color, default Red) - color for the Bid price line
  - `Ask Level Color` (Color, default Green) - color for the Ask price line
  - `Line Thickness` (int, 1-5, default 2) - thickness of the level lines
  - `Line Width (bars)` (int, 2-10, default 4) - horizontal span of the level lines in bars
  - `Gap from Last Bar` (int, 0-5, default 1) - gap between last footprint bar and level lines

### Technical Details
- New `FootprintConfig` properties: `ShowBidAskLevels`, `BidLevelColor`, `AskLevelColor`, `BidAskLevelThickness`, `BidAskLevelWidth`, `BidAskLevelGap`
- New `FootprintConfig` constants: `DEFAULT_BID_ASK_LEVEL_THICKNESS` (2), `DEFAULT_BID_ASK_LEVEL_WIDTH` (4), `DEFAULT_BID_ASK_LEVEL_GAP` (1)
- New `FootprintRenderer.DrawBidAskLevels()` private method draws the two dashed trend lines using bar-index coordinates
- New `FootprintRenderer.RedrawBidAskLevels()` public method called from `Calculate()` on current bar updates
- New `FootprintRenderer.BID_ASK_KEY` static readonly DateTime key (`DateTime.MaxValue`) for tracking Bid/Ask chart objects separately from bar and legend objects
- `Footprints.cs` calls `_renderer.RedrawBidAskLevels()` in `Calculate()` after processing the current bar
- `Footprints.Parameters.cs` six new parameters in the "Bid/Ask Levels" group
- `Footprints.cs` wires all six Bid/Ask parameters to `_config` in both `InitializeComponents()` and `UpdateConfigFromParameters()`
- Uses `_symbol.Bid` and `_symbol.Ask` (already available in the renderer constructor) for live price access

## [1.4.1] - 2026-01-31

### Fixed
- **Value Area Rectangle Too Opaque**: Changed Value Area rendering from filled semi-transparent rectangle to **border only** (not filled). Uses a thin dotted border (`LineStyle.DotsRare`) with low opacity (alpha 100) so it no longer obscures candlesticks, bin rectangles, or volume text. Much less invasive on the chart.
  - New `Value Area Border Thickness` parameter in the Display group (1-5, default 2)
  - `IsFilled = false` replaces the previous `IsFilled = true` with semi-transparent fill
- **Old Ticks Creating Distant Bars**: Tightened gap detection to prevent stale stored ticks from creating bars at old, distant price levels (e.g., a bar far away in the top-left of the chart).
  - Reduced global `MAX_TICK_GAP` from 24 hours to **2 hours** - stored data is discarded if the indicator was offline longer than 2 hours
  - New **bar-level gap detection** via `AreStoredTicksValid()`: for each bar, if the stored ticks have a time gap greater than 1 hour from the bar's open time, those ticks are discarded for that specific bar
  - New **price-level outlier detection** in `CalculateOptimalYRange()`: bars whose mid-price is more than 10% away from the median price of recent bars are excluded from auto-zoom calculation, preventing distant bars from distorting the visible range

### Technical Details
- `FootprintRenderer.DrawBinValueAreaBox()` now sets `IsFilled = false`, `Thickness = config.ValueAreaBorderThickness`, `LineStyle = LineStyle.DotsRare`
- New `FootprintConfig.ValueAreaBorderThickness` property (int, default 2) with `DEFAULT_VALUE_AREA_BORDER_THICKNESS` constant
- `Footprints.Parameters.cs` new parameter `Value Area Border Thickness` in Display group (MinValue=1, MaxValue=5, DefaultValue=2)
- `Footprints.cs` wires `ValueAreaBorderThickness` to `_config.ValueAreaBorderThickness` in both `InitializeComponents()` and `UpdateConfigFromParameters()`
- `Footprints.Storage.cs` `MAX_TICK_GAP` changed from `TimeSpan.FromHours(24)` to `TimeSpan.FromHours(2)`
- New `Footprints.Storage.cs` constant `MAX_BAR_TICK_GAP = TimeSpan.FromHours(1)` for per-bar tick validation
- New `Footprints.Storage.cs` method `AreStoredTicksValid(DateTime barOpenTime, IEnumerable<FootprintTickData> storedTicks)` checks time gap between bar and its stored ticks
- `Footprints.cs` `GetOrBuildFootprint()` now calls `AreStoredTicksValid()` before passing stored ticks to the builder; discards ticks that fail validation
- `FootprintRenderer.CalculateOptimalYRange()` new outlier detection block: calculates median price of recent bars, filters out bars with mid-price more than 10% away from median before computing zoom range

## [1.4.0] - 2026-01-31

### Changed
- **Major Layout Redesign: Separated 3-Column Layout**: Replaced the overlapping bin-on-candlestick design with a clear 3-column layout inspired by TradingView's footprint chart. Each bar's time space is divided into three sections:
  - **LEFT column (35%)**: RED gradient rectangles with SELL volume numbers
  - **CENTER column (30%)**: Traditional thin candlestick (body + shadows)
  - **RIGHT column (35%)**: GREEN gradient rectangles with BUY volume numbers
- **Candlestick Positioning**: Candlestick body now draws in the CENTER section only (37%-63% of bar width), with shadow lines at the exact center (50%). Previously the candlestick spanned the full bar width and was overlapped by bin rectangles.
- **Sell/Buy Bin Separation**: Replaced unified `DrawBinRectangle()` and `DrawBinText()` with dedicated `DrawSellBinColumn()` and `DrawBuyBinColumn()` methods. Each column has its own gradient color scheme (red for sell, green for buy) and positioned text.
- **Gradient Color System**: New `GetSellGradientColor()` and `GetBuyGradientColor()` methods produce red/green gradients based on volume dominance ratio (1x-5x range mapped to 30%-100% intensity). Replaces the previous unified gradient that used the same color for both sides.
- **POC Line**: Now uses DateTime coordinates spanning from SELL_SECTION_START to BUY_SECTION_END for precise positioning across all three columns.
- **Value Area Box**: Now uses DateTime coordinates spanning the full bar width for consistent alignment with the 3-column layout.
- **Imbalance Markers**: Repositioned to the outer edge of the dominant side's column (right edge for buy imbalance, left edge for sell imbalance) instead of a fixed right-aligned position.
- **Legend**: Updated text to reflect the new layout: "Left=Sell | Right=Buy" instead of "Green=Buy | Red=Sell".
- **Removed**: `GetBarTimeRangeWithGap()` helper (replaced by more flexible `GetBarDuration()` and `GetBarDateTime()` helpers).
- **Removed**: `HorizontalGapPercentage` config property is no longer used for bin rendering (the 3-column layout has built-in 2% gaps between sections).

### Technical Details
- New private constants for 3-column layout positioning: `SELL_SECTION_START` (0.02), `SELL_SECTION_END` (0.35), `CANDLE_SECTION_START` (0.37), `CANDLE_SECTION_END` (0.63), `CANDLE_SECTION_CENTER` (0.50), `BUY_SECTION_START` (0.65), `BUY_SECTION_END` (0.98)
- New `FootprintRenderer.GetBarDuration(int barIndex)` private method calculates bar duration from consecutive bar times
- New `FootprintRenderer.GetBarDateTime(int barIndex, double fraction)` private method converts fractional bar position to DateTime
- New `FootprintRenderer.GetSellGradientColor(long sellVolume, long buyVolume)` private method for red gradient
- New `FootprintRenderer.GetBuyGradientColor(long buyVolume, long sellVolume)` private method for green gradient
- New `FootprintRenderer.DrawSellBinColumn()` private method draws sell rectangle + text in LEFT section
- New `FootprintRenderer.DrawBuyBinColumn()` private method draws buy rectangle + text in RIGHT section
- Removed `FootprintRenderer.DrawBinRectangle()` (replaced by separated column methods)
- Removed `FootprintRenderer.DrawBinText()` (replaced by separated column methods)
- Removed `FootprintRenderer.GetBarTimeRangeWithGap()` (replaced by GetBarDateTime)
- `DrawCandlestick()` now uses DateTime coordinates via `GetBarDateTime()` for CENTER section positioning
- `DrawBinPOCLine()` now uses DateTime coordinates via `GetBarDateTime()`
- `DrawBinValueAreaBox()` now uses DateTime coordinates via `GetBarDateTime()`
- `DrawBinImbalanceMarker()` now uses DateTime coordinates with side-specific alignment
- `RenderFootprint()` rendering order updated for 3-column layout: candlestick first, then sell bins, then buy bins, then overlays

## [1.3.1] - 2026-01-31

### Fixed
- **Auto-Zoom Distant Bars Issue**: When the chart contained bars at vastly different price levels (e.g., one at ~$80,000 and another at ~$31,000), the auto-zoom tried to include both, creating a huge vertical range that made numbers illegible. The zoom now focuses on the **most recent N bars** (default 20, configurable) instead of all visible bars, ensuring the zoom is relevant to current price action.
  - New `Zoom Focus Bars` parameter in the Zoom group (5-100, default 20)
  - `CalculateOptimalYRange()` now sorts visible bars by time descending and only uses the most recent N bars for range calculation
  - Distant old bars are excluded from the zoom calculation entirely

### Changed
- **Default Font Size**: Increased from 11 to 12 for improved readability of footprint numbers

### Technical Details
- New `FootprintConfig.AutoZoomFocusBars` property (int, default 20) with `DEFAULT_AUTO_ZOOM_FOCUS_BARS` constant
- `FootprintRenderer.CalculateOptimalYRange()` filters `visibleFootprintBars` using `OrderByDescending(b => b.BarTime).Take(AutoZoomFocusBars)` before computing global high/low
- `Footprints.Parameters.cs` new parameter `Zoom Focus Bars` in Zoom group (MinValue=5, MaxValue=100, DefaultValue=20)
- `Footprints.cs` wires `AutoZoomFocusBars` to `_config.AutoZoomFocusBars` in both `InitializeComponents()` and `UpdateConfigFromParameters()`
- `FootprintConfig.DEFAULT_FONT_SIZE` changed from 11 to 12
- `Footprints.Parameters.cs` Font Size `DefaultValue` changed from 11 to 12

## [1.3.0] - 2026-01-31

### Added
- **LocalStorage Tick Persistence**: Tick data (classified as uptick/downtick with timestamps) is now persisted to cTrader's LocalStorage using Device scope, so footprint data survives chart refreshes, indicator reloads, and timeframe changes.
  - New domain model `FootprintTickData` with `TickClassification` enum (Uptick, Downtick, ZeroTick, Unknown) for storage-specific tick representation
  - New `FootprintTickStorage` manager class handling serialization/deserialization, tick management, and automatic cleanup
  - New `Footprints.Storage.cs` partial class integrating LocalStorage with the indicator lifecycle
  - Storage key format: `"Footprint_EURUSD"` (per symbol, alphanumeric only)
  - Serialization format: compact pipe-delimited text with version header (`FP1`)
  - Uses `InvariantCulture` for price serialization to avoid locale-dependent decimal separators
- **Tick Reaggregation on Reload**: When the indicator initializes, stored ticks are loaded and passed to `FootprintBarBuilder` for each bar's time range, enriching footprint data beyond the live tick buffer
- **Automatic Tick Cleanup**: Ticks older than 7 days or exceeding 100,000 count are automatically pruned
- **Gap Detection**: If more than 24 hours have elapsed since the last stored tick, stored data is discarded to avoid stale reaggregation
- **Periodic Save**: Storage is saved to disk on every rendered current-bar update, plus every 500 newly classified ticks via the `OnTickClassified` callback
- **Immediate Flush**: `LocalStorage.Flush(LocalStorageScope.Device)` is called after each save to ensure data is written immediately (bypasses the 1-minute auto-save delay)

### Changed
- **FootprintBarBuilder**: Now accepts an optional `IEnumerable<FootprintTickData> storedTicks` parameter in both `BuildFromTicks()` and `BuildFromBar()` methods. Stored ticks are processed first (Phase 1) before live ticks (Phase 2), and the classifier state is maintained across both phases for correct classification continuity.
- **FootprintBarBuilder**: New `OnTickClassified` callback (`Action<DateTime, double, TickType>`) fires for each newly classified live tick, enabling the indicator to store ticks without tight coupling.
- **Footprints.cs Initialize()**: Now calls `InitializeStorage()` after `InitializeComponents()` to load persisted ticks before processing begins.
- **Footprints.cs GetOrBuildFootprint()**: Retrieves stored ticks for each bar's time range and passes them to the builder for reaggregation.
- **Footprints.cs Calculate()**: Saves storage data after each rendered current-bar update for data safety.

### Technical Details
- New file: `Domain/FootprintTickData.cs` - tick data entity with `TickClassification` enum
- New file: `Storage/FootprintTickStorage.cs` - storage manager with serialize/deserialize, cleanup, and key generation
- New file: `Footprints.Storage.cs` - partial class for LocalStorage integration (InitializeStorage, SaveStorageData, StoreProcessedTick, ConvertTickTypeToClassification)
- `FootprintBarBuilder.BuildFromTicks()` new parameter: `IEnumerable<FootprintTickData> storedTicks`
- `FootprintBarBuilder.BuildFromBar()` new parameter: `IEnumerable<FootprintTickData> storedTicks`
- `FootprintBarBuilder.OnTickClassified` new public callback property
- `Footprints.cs` new helper: `EstimateBarDurationMinutes()` for bar close time calculation
- Constants: `SAVE_INTERVAL_TICKS = 500`, `MAX_TICK_GAP = 24h`, `MAX_TICKS_TO_STORE = 100000`, `MAX_TICK_AGE = 7 days`, `HEADER_VERSION = "FP1"`, `PRICE_DECIMAL_PLACES = 8`
- LocalStorage scope: `LocalStorageScope.Device` for cross-reload persistence
- `AccessRights` remains `None` (LocalStorage works regardless of access rights)

## [1.2.6] - 2026-01-31

### Added
- **Horizontal Gap Between Bars**: Bin rectangles and candlestick bodies now use DateTime-based coordinates with a configurable gap percentage, creating visible space between adjacent bars for cleaner visual separation.
  - New `Bar Gap (%)` parameter in the Display group (0-30%, default 10%)
  - 10% gap on each side = 80% bar width, 20% total gap
  - Gap applies to bin rectangles and custom candlestick bodies
  - Uses `Chart.DrawRectangle(name, DateTime, double, DateTime, double, Color)` overload instead of integer bar indices

### Technical Details
- New `FootprintConfig.HorizontalGapPercentage` property (0.0-0.30, default 0.10) with `DEFAULT_HORIZONTAL_GAP_PERCENTAGE` constant
- New `FootprintRenderer.GetBarTimeRangeWithGap()` private helper calculates DateTime start/end with gap applied from bar duration
- `FootprintRenderer.DrawBinRectangle()` uses DateTime coordinates via `GetBarTimeRangeWithGap()` instead of `barIndex` / `barIndex + 1`
- `FootprintRenderer.DrawCandlestick()` body rectangle uses DateTime coordinates via `GetBarTimeRangeWithGap()` with integer fallback
- `Footprints.Parameters.cs` new parameter `Bar Gap (%)` in Display group
- `Footprints.cs` wires `BarGapPercentage / 100.0` to `_config.HorizontalGapPercentage` in both `InitializeComponents()` and `UpdateConfigFromParameters()`

## [1.2.5] - 2026-01-31

### Fixed
- **Text Positioning Gap**: Volume text numbers no longer positioned at `barIndex - 1` (previous bar), which caused huge visual gaps when bars are far apart (e.g., weekend gaps, night sessions). Text is now drawn at the SAME bar (`barIndex`) centered on each bin rectangle.
- **Text Contrast**: Text uses white color for maximum contrast against colored bin backgrounds. Bins with very light backgrounds (high R+G+B > 450) use black text instead. Imbalance bins continue to use their imbalance color for emphasis.
- **Legend Positioning**: Legend no longer drifts to the center of the chart. Replaced `Chart.DrawText()` (coordinate-based) with `Chart.DrawStaticText()` using `VerticalAlignment.Top` and `HorizontalAlignment.Right` for fixed top-right corner positioning independent of scroll/zoom.
- **Legend Font Size**: Legend font increased to `FontSize + 2` for better readability.
- **Legend Format**: Compact 4-line format: `"FOOTPRINT\nGreen=Buy | Red=Sell\nGray=Neutral\nYellow=POC | Blue=VA"` with slightly transparent white color.
- **Legend Redraw Optimization**: `RedrawLegend()` no longer redraws on every scroll/zoom event since `DrawStaticText` is position-independent. Only draws once if not already present.
- **Duplicate Changelog**: Removed the full changelog from the `Footprints.cs` XML doc comment. Changelog is now maintained exclusively in `CHANGELOG.md`.

### Technical Details
- `FootprintRenderer.DrawBinText()` positions text at `barIndex` (was `barIndex - 1`) with `HorizontalAlignment.Center`
- `FootprintRenderer.DrawBinText()` calculates background luminance via `(R + G + B) > 450` to choose white vs black text
- `FootprintRenderer.DrawBinText()` no longer requires `barIndex >= 1` guard (text is on the same bar)
- `FootprintRenderer.DrawLegend()` uses `Chart.DrawStaticText()` instead of multiple `Chart.DrawText()` objects
- `FootprintRenderer.DrawLegend()` creates a single `ChartStaticText` object instead of 7 separate `ChartText` objects
- `FootprintRenderer.RedrawLegend()` only draws legend if not already present (no-op when legend exists)
- `Footprints.cs` XML doc comment reduced to description and version only; full changelog in `CHANGELOG.md`

## [1.2.4] - 2026-01-31

### Fixed
- **Text Positioning**: Volume numbers now rendered clearly to the LEFT of each bar instead of overlapping inside/on top of bin rectangles
  - Text anchored at `barIndex - 1` (previous bar) with `HorizontalAlignment.Right`, which places it entirely left of the current bar
  - Single combined `"SELL | BUY"` text object replaces three separate trailing-space-based objects (cleaner rendering, no misalignment)
  - Neutral bins displayed in readable light gray (RGB 200,200,200); imbalance bins use the dominant side's imbalance color
  - Guard added: `barIndex < 1` check prevents invalid index access for the first bar on the chart
- **Legend Not Visible**: Legend in the top-right corner is now reliably rendered
  - Uses `_chartArea.TopY` / `_chartArea.BottomY` with percentage-based line height (3% of visible price range) instead of `_symbol.TickSize * 15` which was instrument-dependent and often too small or too large
  - Legend positioned at `lastBar` (not `lastBar - 2`) to ensure it stays within the visible area
  - All legend text objects use `VerticalAlignment.Top` for consistent downward stacking from the top of the chart
  - Legend text shortened for compactness: "Buy" instead of "Green = Buy Dominant", "POC" instead of "Yellow = POC (Point of Control)"
  - Value Area legend color uses full opacity (alpha 255) for readability instead of semi-transparent alpha 200
  - Fallback logging when `_chartArea` is null to aid debugging

### Technical Details
- `FootprintRenderer.DrawBinText()` now creates a single `ChartText` per bin (was three: sell, separator, buy) anchored at `barIndex - 1`
- Chart object naming changed from `FP_BinSell_`, `FP_BinSep_`, `FP_BinBuy_` to single `FP_BinText_` prefix
- `FootprintRenderer.DrawLegend()` now calculates `lineHeight` as `priceRange * 0.03` (3% of visible range)
- Legend bar position changed from `lastBar - 2` to `lastBar`
- All legend `ChartText` objects now set `VerticalAlignment = VerticalAlignment.Top`

## [1.2.3] - 2026-01-31

### Fixed
- **Text Overlap with Candlestick**: Moved SELL and BUY volume numbers to the LEFT of the bar to prevent overlap with bin rectangles and candlestick body
  - All three text elements (SELL, separator, BUY) anchored at `barIndex` with `HorizontalAlignment.Right` so they render entirely to the left of the bar
  - Trailing spaces used to offset each element: SELL furthest left, separator in the middle, BUY closest to bar edge
  - Layout: `"  1234  |  "` (SELL with separator padding) ... `"|       "` (separator) ... `"  5678 "` (BUY)
  - Clean vertical number alignment maintained via right-aligned format specifiers

### Technical Details
- `FootprintRenderer.DrawBinText()` all three text objects now use `barIndex` (int) with `HorizontalAlignment.Right` instead of mixed positions with `HorizontalAlignment.Center`
- SELL text includes trailing separator and spaces: `$"{bin.SellVolume,6}  \u2502  "` to push it furthest left
- Separator text includes trailing spaces: `"\u2502       "` to position between SELL and BUY
- BUY text includes minimal trailing space: `$"{bin.BuyVolume,6} "` to sit just left of bar edge
- Approach avoids fractional bar index values (cTrader `DrawText` requires `int` for bar index parameter)

## [1.2.2] - 2026-01-31

### Improved
- **Cleaner Bar Separation**: Increased vertical gap between bin rectangles from 2% to 5% of bin height and added subtle white border (1px, 40 alpha) around each bin rectangle for cleaner visual separation between adjacent bars and bins
- **Legend in Top-Right Corner**: New persistent legend explaining color meanings (green = buy dominant, red = sell dominant, gray = neutral) and line meanings (yellow = POC, blue = Value Area)
  - Legend auto-repositions on chart scroll and zoom events
  - Togglable via new `Show Legend` parameter (default: true)
- **Increased Default Font Size**: Default font size increased from 10 to 11 for improved readability
  - New `DEFAULT_FONT_SIZE` constant in `FootprintConfig`
- **Value Area Visibility**: Significantly improved Value Area visibility
  - Default `ValueAreaOpacity` increased from 30 to 60
  - Value Area box now has a dotted SkyBlue border (`LineStyle.Dots`) for clear delineation
  - Default `ValueAreaColor` alpha changed from 30 to 60

### Technical Details
- `FootprintRenderer.DrawBinRectangle()` vertical gap increased from `binHeight * 0.02` to `binHeight * 0.05`, added `Thickness = 1` with subtle white border
- `FootprintRenderer.DrawBinValueAreaBox()` adds `LineStyle.Dots` with `Color.SkyBlue` border for clear Value Area delineation
- New `FootprintRenderer.DrawLegend()` creates 7 text objects (title + 3 color lines + 2 line explanation lines) tracked under `DateTime.MinValue` key
- New `FootprintRenderer.RedrawLegend()` public method called from scroll/zoom event handlers
- `FootprintRenderer.RenderFootprint()` draws legend on first render if enabled
- `FootprintConfig.ShowLegend` new boolean property (default true)
- `FootprintConfig.DEFAULT_FONT_SIZE` new constant = 11
- `Footprints.Parameters.cs` new parameter `Show Legend` in Display group
- `Footprints.cs` wires `ShowLegend` to config and calls `RedrawLegend()` on scroll/zoom

## [1.2.1] - 2026-01-31

### Fixed
- **Bin Text Readability**: Replaced single gray "SELL | BUY" text with two separate color-coded text objects
  - Sell volume drawn in SellColor (red) on the left side of the bar
  - Buy volume drawn in BuyColor (green) on the right side of the bar
  - Subtle gray separator (\u2502) drawn between the two numbers
  - Imbalance colors applied per-side only (e.g., sell imbalance highlights sell text only, not buy text)
- **Bin Rectangle Overlap**: Added 2% vertical gap between adjacent bin rectangles for visual separation
- **Rendering Order**: Fixed Z-order so elements render back-to-front: bin rectangles -> Value Area -> POC line -> text -> imbalance markers
  - Previously Value Area and POC were drawn before bin rectangles, causing them to be hidden
- **Value Area Opacity**: Reduced default opacity from 50 to 30 so Value Area overlay does not obscure bin rectangles and text
  - Value Area box now properly uses the configurable `ValueAreaOpacity` property instead of hardcoded alpha

### Technical Details
- `FootprintRenderer.DrawBinText()` now creates three chart objects per bin: sell text (left), separator (center), buy text (right)
- New chart object naming: `FP_BinSell_`, `FP_BinSep_`, `FP_BinBuy_` replace single `FP_BinText_`
- `FootprintRenderer.DrawBinRectangle()` applies `binHeight * 0.02` gap at top and bottom
- `FootprintRenderer.DrawBinValueAreaBox()` now applies `ValueAreaOpacity` from config instead of using raw `ValueAreaColor` alpha
- `FootprintRenderer.RenderFootprint()` bin-based rendering section reordered for correct Z-order
- `FootprintConfig` default `ValueAreaOpacity` changed from 50 to 30
- `FootprintConfig` default `ValueAreaColor` alpha changed from 50 to 30

## [1.2.0] - 2026-01-31

### Added
- **Bin Aggregation**: Divide each bar's High-Low range into N equal-sized bins (default 5) and aggregate buy/sell volume per bin, dramatically improving readability (5 lines instead of 50+)
  - New domain model `FootprintBin` with buy/sell volume, delta, imbalance, POC, and Value Area properties
  - `FootprintBarBuilder.AggregatePriceLevelsIntoBins()` handles bin creation, volume aggregation, imbalance detection, POC selection, and Value Area calculation from bins
  - Bin-based POC line drawn at highest-volume bin midpoint
  - Bin-based Value Area box spanning from lowest VA bin bottom to highest VA bin top
  - Colored background rectangles (heatmap style) for each bin, with color based on buy/sell ratio
  - Imbalance markers for bins using the same threshold logic as price levels
  - Doji bar handling: single bin for bars with range smaller than tick size
- **New Parameters (Binning group)**:
  - `Number of Bins` (int, 3-20, default 5) - number of equal-sized price bins per bar
  - `Show Bin Rectangles` (bool, default true) - toggle colored bin background rectangles
  - `Bin Rectangle Opacity` (int, 20-200, default 80) - transparency of bin rectangles
- **Backward Compatibility**: Setting `NumberOfBins` to 0 disables binning and reverts to legacy per-tick price level rendering

### Changed
- **FootprintBar**: Added `Bins` list property, `High` and `Low` OHLC properties for bin range calculation
- **FootprintBarBuilder.BuildFromBar()**: Now accepts optional `numberOfBins` parameter and passes OHLC High/Low to `BuildFromTicks()`
- **FootprintBarBuilder.BuildFromTicks()**: New parameters `numberOfBins`, `barHigh`, `barLow` for bin aggregation support
- **FootprintRenderer.RenderFootprint()**: Automatically detects bins and switches between bin-based and legacy rendering
- **FootprintRenderer.CalculateOptimalYRange()**: Uses bin count instead of price level count when bins are present, improving auto-zoom accuracy
- **FootprintRenderer.DrawDelta()**: Uses bin price range when bins are available for delta text positioning

### Technical Details
- New file: `Domain/FootprintBin.cs` - aggregated price bin entity
- `FootprintBarBuilder` new private methods: `AggregatePriceLevelsIntoBins()`, `DetectBinImbalance()`, `CalculateBinValueArea()`
- `FootprintRenderer` new private methods: `DrawBinRectangle()`, `DrawBinText()`, `DrawBinPOCLine()`, `DrawBinValueAreaBox()`, `DrawBinImbalanceMarker()`
- `FootprintConfig` new properties: `NumberOfBins`, `ShowBinRectangles`, `BinRectangleOpacity`
- `FootprintConfig` new constants: `DEFAULT_NUMBER_OF_BINS`, `DEFAULT_BIN_RECTANGLE_OPACITY`
- All new chart objects (bin rectangles, bin text, bin imbalance markers) properly tracked and cleaned up via DateTime-based tracking

## [1.1.1] - 2026-01-31

### Fixed
- **Auto-Zoom Not Using Vertical Space**: The `CalculateOptimalYRange()` was computing the Y range from global OHLC high/low across all visible bars, resulting in a huge range where footprint data occupied only a small fraction of the chart
- **Pixel-Based Zoom Calculation**: Replaced the old range-based algorithm with a pixel-based approach that uses `ChartArea.Height` to determine how tight the Y-axis range should be
  - Each price level now gets approximately `fontSize * 2.2` pixels of vertical space
  - Range is tightly centered on the footprint data midpoint with configurable padding
  - Font size 10 yields ~22 pixels per level, making text clearly readable
- **ChartArea Type**: Changed from `IndicatorArea` to `ChartArea` for the zoom control reference, providing access to the `Height` property needed for pixel calculations
- **Diagnostic Logging**: Added Print() statements to log ChartArea.Height, pixels per level, max levels visible, actual level count, and calculated Y range boundaries

### Technical Details
- `FootprintRenderer.SetChartArea()` now accepts `ChartArea` instead of `IndicatorArea`
- `FootprintRenderer.CalculateOptimalYRange()` now accepts `ChartArea` as first parameter
- `FootprintRenderer` constructor now accepts `Action<string>` delegate for Print() output
- New constant `PIXELS_PER_LEVEL_MULTIPLIER = 2.2` controls vertical spacing per level
- `Footprints.cs` now passes `Chart.ChartArea` instead of `IndicatorArea` to the renderer

## [1.1.0] - 2026-01-31

### Added
- **Custom Candlestick Rendering**: Draw candle bodies (rectangles) and shadows (trend lines) directly from the indicator
  - Bullish/bearish body colors with configurable opacity (default 80/255)
  - Shadow (wick) lines with configurable color and thickness
  - Doji candles rendered with minimum body height for visibility
  - Candles drawn as background layer so footprint text remains readable
- **Dynamic Y-Axis Zoom Control**: Automatic zoom adjustment via `ChartArea.SetYRange()`
  - Calculates optimal Y range based on all visible footprint bars and OHLC data
  - Configurable padding percentage (default 10%) above and below the range
  - Extra bottom padding when delta display is enabled
  - Minimum level count threshold before auto-zoom activates (3 levels)
  - Zoom throttling (300ms) to prevent performance degradation
  - Re-applies zoom on chart scroll and chart zoom events
- **New Parameters**:
  - `Show Custom Candlesticks` (bool, default true) - toggle custom candle rendering
  - `Bullish Candle Color` (Color, default Green) - bullish candle body color
  - `Bearish Candle Color` (Color, default Red) - bearish candle body color
  - `Shadow Color` (Color, default Gray) - candlestick shadow color
  - `Shadow Thickness` (int, 1-3, default 1) - shadow line thickness
  - `Candle Body Opacity` (int, 20-200, default 80) - candle body transparency
  - `Enable Auto Zoom` (bool, default true) - toggle dynamic zoom
  - `Zoom Padding (%)` (double, 0-50, default 10) - extra space above/below range

### Changed
- **Default Font Size**: Increased from 8 to 10 for better readability
- **Default Font Family**: Changed from Arial to Consolas (monospace) for aligned volume columns
- **Font Size Range**: Extended maximum from 14 to 20
- **Text Separator**: Changed from ` | ` (pipe) to ` \u2502 ` (Unicode box-drawing light vertical) for cleaner visual
- **Renderer Architecture**: Added `SetBarsReference()` and `SetChartArea()` methods for dependency injection
- **Rendering Order**: Candlestick drawn first (background), then value area, POC, text, imbalances, delta (foreground)

### Technical Details
- `FootprintRenderer` now accepts `Bars` reference for OHLC data access
- `FootprintRenderer` now accepts `IndicatorArea` reference for Y-axis control
- New `FootprintConfig` properties: `BullishCandleColor`, `BearishCandleColor`, `ShadowColor`, `ShowCustomCandlesticks`, `EnableAutoZoom`, `ShadowThickness`, `CandleBodyOpacity`, `ZoomPaddingPercent`, `MinLevelsForAutoZoom`
- New `FootprintConfig.ApplyCandleOpacity()` utility method
- Constants added: `MIN_FONT_SIZE`, `MAX_FONT_SIZE`, `DEFAULT_CANDLE_OPACITY`, `DEFAULT_SHADOW_THICKNESS`, `DEFAULT_ZOOM_PADDING`
- Chart event handlers: `ScrollChanged`, `ZoomChanged` for zoom re-application
- All new chart objects (candle body, shadows) properly tracked and cleaned up

## [1.0.0] - 2026-01-31

### Added
- Initial implementation of Footprint indicator for cTrader
- **Tick Classification**: Uptick/downtick algorithm for categorizing buy/sell volume
  - Uptick (price increase) = Buy volume
  - Downtick (price decrease) = Sell volume
  - Zero tick (no change) = Inherits previous classification
- **Point of Control (POC)**: Identifies price level with highest total volume
- **Value Area Calculation**: Shows price range containing 70% of total volume (configurable 50-90%)
- **Imbalance Detection**: Highlights levels where buy or sell volume dominates (default 300% threshold)
- **Bilateral Visualization**: "SELL | BUY" format for each price level
- **Color Gradient Rendering**: Dynamic colors based on buy/sell volume ratio
- **Analysis Parameters**:
  - Imbalance Threshold (150-1000%, default 300%)
  - Value Area Percentage (50-90%, default 70%)
  - Tick Size Override (optional)
- **Display Parameters**:
  - Show/Hide POC, Value Area, Imbalances, Delta
  - Color gradient toggle
  - Font size configuration (6-14)
  - Max bars to display (10-500, default 100)
- **Color Customization**:
  - Buy/Sell/Neutral colors
  - Imbalance Buy/Sell colors
  - POC and Value Area colors
- **Performance Optimizations**:
  - In-memory caching (max 500 bars)
  - Render throttling for real-time bar (configurable, default 500ms)
  - Lazy loading (only visible bars processed)
  - Automatic cache pruning
- **Proper Resource Management**:
  - Chart object tracking for all drawn elements
  - Cleanup on redraw to prevent memory leaks
  - DateTime-based bar identification (stable across reloads)

### Technical Details
- **Architecture**: Modular design with separate namespaces
  - `Footprints.Domain`: Business entities (FootprintBar, PriceLevel)
  - `Footprints.Processing`: Tick classification and bar building logic
  - `Footprints.Graphics`: Rendering and visualization
- **SOLID Principles**: Follows Single Responsibility, Open/Closed, and Dependency Inversion
- **AccessRights**: None (no file system or network access required)
- **Overlay Mode**: Displays directly on price chart

### Known Limitations
- Tick history buffer limited to ~10k ticks in cTrader
- First tick of each bar cannot be classified (insufficient reference data)
- Performance may degrade with >200 bars simultaneously displayed
- Tick volume data not directly available from cTrader (uses tick count as proxy)

### Future Enhancements
- LocalStorage integration for historical data persistence
- Advanced imbalance detection algorithms
- Customizable text positioning and layout
- Export functionality for footprint data
- Additional metrics (buying/selling pressure, absorption levels)
