using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using Footprints.Domain;

namespace Footprints.Processing;

/// <summary>
/// Builds FootprintBar objects from tick data using tick classification.
/// Supports optional bin aggregation to group price levels into equal-sized bins
/// for improved readability on the chart.
/// Supports integration with FootprintTickStorage for tick persistence:
/// - Accepts pre-classified stored ticks for reaggregation on chart reload
/// - Fires a callback for each newly classified tick so it can be stored
/// </summary>
public class FootprintBarBuilder
{
    private readonly MarketData _marketData;
    private readonly Symbol _symbol;
    private readonly TickClassifier _tickClassifier;

    /// <summary>
    /// Optional callback invoked for each newly classified tick from live market data.
    /// Parameters: (DateTime timestamp, double price, TickType classification).
    /// Used by the indicator to store ticks in FootprintTickStorage.
    /// </summary>
    public Action<DateTime, double, TickType> OnTickClassified { get; set; }

    /// <summary>
    /// Creates a new footprint bar builder.
    /// </summary>
    /// <param name="marketData">cTrader MarketData API</param>
    /// <param name="symbol">Trading symbol</param>
    public FootprintBarBuilder(MarketData marketData, Symbol symbol)
    {
        _marketData = marketData;
        _symbol = symbol;
        _tickClassifier = new TickClassifier();
    }

    /// <summary>
    /// Builds a FootprintBar from tick data for the specified time range.
    /// First processes any stored ticks (from LocalStorage persistence), then
    /// processes live ticks from MarketData. This ensures reaggregation works
    /// correctly on chart refresh or timeframe change.
    /// </summary>
    /// <param name="barOpenTime">Bar opening time</param>
    /// <param name="barCloseTime">Bar closing time</param>
    /// <param name="imbalanceThreshold">Imbalance detection threshold percentage (default 300%)</param>
    /// <param name="valueAreaPercentage">Value area percentage (default 70%)</param>
    /// <param name="numberOfBins">Number of bins for aggregation (0 = disabled)</param>
    /// <param name="barHigh">OHLC High price for the bar (needed for bin calculation)</param>
    /// <param name="barLow">OHLC Low price for the bar (needed for bin calculation)</param>
    /// <param name="storedTicks">Pre-classified ticks from LocalStorage to process before live ticks (optional)</param>
    /// <returns>Constructed FootprintBar with all metrics calculated</returns>
    public FootprintBar BuildFromTicks(DateTime barOpenTime, DateTime barCloseTime, double imbalanceThreshold = 300.0, double valueAreaPercentage = 70.0, int numberOfBins = 0, double barHigh = 0, double barLow = 0, IEnumerable<FootprintTickData> storedTicks = null)
    {
        var footprintBar = new FootprintBar(barOpenTime);

        // Reset classifier for new bar
        _tickClassifier.Reset();

        // === Phase 1: Process stored ticks first (from LocalStorage) ===
        // These are pre-classified ticks that were persisted from previous sessions.
        // They do NOT go through the classifier again and do NOT fire OnTickClassified.
        if (storedTicks != null)
        {
            foreach (FootprintTickData storedTick in storedTicks)
            {
                // Convert TickClassification to buy/sell
                bool isBuy;
                if (storedTick.Classification == TickClassification.Uptick)
                    isBuy = true;
                else if (storedTick.Classification == TickClassification.Downtick)
                    isBuy = false;
                else
                    continue; // Skip Unknown and ZeroTick with no direction

                // Round price to tick size for consistency
                double roundedPrice = RoundToTickSize(storedTick.Price);
                long volume = 1;

                footprintBar.AddTickVolume(roundedPrice, volume, isBuy);

                // Feed the stored tick price to the classifier to maintain state
                // so that the first live tick can be correctly classified relative
                // to the last stored tick price.
                _tickClassifier.ClassifyTick(storedTick.Price);
            }
        }

        // === Phase 2: Process live ticks from MarketData ===
        var allTicks = _marketData.GetTicks();

        if (allTicks != null && allTicks.Count > 0)
        {
            foreach (var tick in allTicks)
            {
                // Skip ticks outside the bar's time range
                if (tick.Time < barOpenTime || tick.Time >= barCloseTime)
                    continue;

                // Use mid price (average of bid and ask) for classification
                double tickPrice = (tick.Bid + tick.Ask) / 2.0;

                // Classify tick
                TickType tickType = _tickClassifier.ClassifyTick(tickPrice);

                // Skip unknown ticks (first tick)
                if (tickType == TickType.Unknown)
                    continue;

                // Fire callback so the tick can be stored for persistence
                OnTickClassified?.Invoke(tick.Time, tickPrice, tickType);

                // Round price to tick size
                double roundedPrice = RoundToTickSize(tickPrice);

                // Get volume (use 1 if not available, as cTrader doesn't provide tick volume directly)
                long volume = 1;

                // Add to footprint bar
                bool isBuy = tickType == TickType.Buy;
                footprintBar.AddTickVolume(roundedPrice, volume, isBuy);
            }
        }

        // Calculate all metrics on price levels
        footprintBar.CalculatePOC();
        footprintBar.CalculateValueArea(valueAreaPercentage);
        footprintBar.DetectImbalances(imbalanceThreshold);

        // Store OHLC High/Low on the bar
        footprintBar.High = barHigh;
        footprintBar.Low = barLow;

        // Aggregate into bins if enabled
        if (numberOfBins > 0)
        {
            AggregatePriceLevelsIntoBins(footprintBar, numberOfBins, imbalanceThreshold, valueAreaPercentage);
        }

        return footprintBar;
    }

    /// <summary>
    /// Builds a FootprintBar from existing bar data, optionally merging with stored ticks.
    /// When stored ticks are provided, they are processed first to populate the footprint
    /// from persisted data, then live ticks from MarketData are added on top.
    /// </summary>
    /// <param name="bars">Bars data series</param>
    /// <param name="index">Bar index</param>
    /// <param name="imbalanceThreshold">Imbalance detection threshold percentage</param>
    /// <param name="valueAreaPercentage">Value area percentage</param>
    /// <param name="numberOfBins">Number of bins for aggregation (0 = disabled)</param>
    /// <param name="storedTicks">Pre-classified ticks from LocalStorage for this bar's time range (optional)</param>
    /// <returns>Constructed FootprintBar with all metrics calculated</returns>
    public FootprintBar BuildFromBar(Bars bars, int index,
        double imbalanceThreshold = 300.0, double valueAreaPercentage = 70.0,
        int numberOfBins = 0, IEnumerable<FootprintTickData> storedTicks = null)
    {
        DateTime barTime = bars.OpenTimes[index];
        DateTime barCloseTime = index + 1 < bars.Count
            ? bars.OpenTimes[index + 1]
            : barTime.AddMinutes(GetBarDurationMinutes(bars));

        // Get OHLC High/Low for bin aggregation
        double barHigh = bars.HighPrices[index];
        double barLow = bars.LowPrices[index];

        return BuildFromTicks(barTime, barCloseTime, imbalanceThreshold, valueAreaPercentage,
            numberOfBins, barHigh, barLow, storedTicks);
    }

    /// <summary>
    /// Aggregates price levels into equal-sized bins for improved readability.
    /// Divides the bar's High-Low range into N equal bins, sums volume in each,
    /// recalculates POC and Value Area from bins, and detects bin imbalances.
    /// </summary>
    /// <param name="footprintBar">Footprint bar with price levels already calculated</param>
    /// <param name="numberOfBins">Number of bins to create (3-20)</param>
    /// <param name="imbalanceThreshold">Imbalance detection threshold percentage</param>
    /// <param name="valueAreaPercentage">Value area percentage for bin-based calculation</param>
    private void AggregatePriceLevelsIntoBins(FootprintBar footprintBar, int numberOfBins,
        double imbalanceThreshold, double valueAreaPercentage)
    {
        if (footprintBar.PriceLevels.Count == 0 || numberOfBins <= 0)
            return;

        double high = footprintBar.High;
        double low = footprintBar.Low;
        double range = high - low;
        double tickSize = _symbol.TickSize;

        // Clear any existing bins
        footprintBar.Bins.Clear();

        // Handle doji bars (range < tick size): single bin for entire bar
        if (range < tickSize)
        {
            FootprintBin singleBin = new FootprintBin
            {
                PriceBottom = low,
                PriceTop = high + tickSize,
                BuyVolume = footprintBar.TotalBuyVolume,
                SellVolume = footprintBar.TotalSellVolume
            };
            footprintBar.Bins.Add(singleBin);
            DetectBinImbalance(singleBin, imbalanceThreshold);
            singleBin.IsPointOfControl = true;
            singleBin.IsInValueArea = true;
            return;
        }

        // Calculate bin size
        double binSize = range / numberOfBins;

        // Create empty bins
        for (int i = 0; i < numberOfBins; i++)
        {
            footprintBar.Bins.Add(new FootprintBin
            {
                PriceBottom = low + (i * binSize),
                PriceTop = low + ((i + 1) * binSize)
            });
        }

        // Aggregate price levels into bins
        foreach (PriceLevel level in footprintBar.PriceLevels.Values)
        {
            // Find which bin this level belongs to
            int binIndex = (int)((level.Price - low) / binSize);

            // Clamp to valid range (handle edge cases where price == high)
            binIndex = Math.Max(0, Math.Min(numberOfBins - 1, binIndex));

            // Add volume to bin
            footprintBar.Bins[binIndex].BuyVolume += level.BuyVolume;
            footprintBar.Bins[binIndex].SellVolume += level.SellVolume;
        }

        // Detect imbalances for each bin
        foreach (FootprintBin bin in footprintBar.Bins)
        {
            DetectBinImbalance(bin, imbalanceThreshold);
        }

        // Find POC bin (highest total volume)
        FootprintBin pocBin = footprintBar.Bins
            .OrderByDescending(b => b.TotalVolume)
            .FirstOrDefault();

        if (pocBin != null)
        {
            pocBin.IsPointOfControl = true;
        }

        // Calculate Value Area from bins (expand from POC bin)
        CalculateBinValueArea(footprintBar.Bins, valueAreaPercentage);
    }

    /// <summary>
    /// Detects imbalance for a single bin using the same logic as PriceLevel imbalance detection.
    /// </summary>
    /// <param name="bin">The bin to check</param>
    /// <param name="thresholdPercentage">Minimum imbalance ratio (e.g., 300% = 3x)</param>
    private void DetectBinImbalance(FootprintBin bin, double thresholdPercentage)
    {
        bin.HasImbalance = false;
        bin.ImbalanceType = ImbalanceType.None;

        if (bin.TotalVolume == 0)
            return;

        long maxVol = Math.Max(bin.BuyVolume, bin.SellVolume);
        long minVol = Math.Min(bin.BuyVolume, bin.SellVolume);

        // One side has volume, the other does not: clear imbalance
        if (minVol == 0)
        {
            bin.HasImbalance = true;
            bin.ImbalanceType = bin.BuyVolume > 0 ? ImbalanceType.Buy : ImbalanceType.Sell;
            return;
        }

        // Calculate ratio: max / min (expressed as percentage)
        double ratio = ((double)maxVol / minVol) * 100.0;

        if (ratio >= thresholdPercentage)
        {
            bin.HasImbalance = true;
            bin.ImbalanceType = bin.BuyVolume > bin.SellVolume ? ImbalanceType.Buy : ImbalanceType.Sell;
        }
    }

    /// <summary>
    /// Calculates Value Area from bins by expanding outward from the POC bin
    /// until the target volume percentage is reached.
    /// </summary>
    /// <param name="bins">List of bins (ordered by price ascending)</param>
    /// <param name="percentage">Target volume percentage (e.g., 70%)</param>
    private void CalculateBinValueArea(List<FootprintBin> bins, double percentage)
    {
        if (bins == null || bins.Count == 0)
            return;

        // Find POC bin index
        int pocIndex = -1;
        for (int i = 0; i < bins.Count; i++)
        {
            if (bins[i].IsPointOfControl)
            {
                pocIndex = i;
                break;
            }
        }

        if (pocIndex < 0)
            return;

        // Calculate total volume across all bins
        long totalVolume = 0;
        foreach (FootprintBin bin in bins)
        {
            totalVolume += bin.TotalVolume;
        }

        if (totalVolume == 0)
            return;

        long targetVolume = (long)(totalVolume * (percentage / 100.0));
        long accumulatedVolume = bins[pocIndex].TotalVolume;
        bins[pocIndex].IsInValueArea = true;

        // Expand up and down from POC bin
        int upperIndex = pocIndex + 1;
        int lowerIndex = pocIndex - 1;

        while (accumulatedVolume < targetVolume && (upperIndex < bins.Count || lowerIndex >= 0))
        {
            FootprintBin upperBin = upperIndex < bins.Count ? bins[upperIndex] : null;
            FootprintBin lowerBin = lowerIndex >= 0 ? bins[lowerIndex] : null;

            if (upperBin != null && lowerBin != null)
            {
                if (upperBin.TotalVolume >= lowerBin.TotalVolume)
                {
                    upperBin.IsInValueArea = true;
                    accumulatedVolume += upperBin.TotalVolume;
                    upperIndex++;
                }
                else
                {
                    lowerBin.IsInValueArea = true;
                    accumulatedVolume += lowerBin.TotalVolume;
                    lowerIndex--;
                }
            }
            else if (upperBin != null)
            {
                upperBin.IsInValueArea = true;
                accumulatedVolume += upperBin.TotalVolume;
                upperIndex++;
            }
            else if (lowerBin != null)
            {
                lowerBin.IsInValueArea = true;
                accumulatedVolume += lowerBin.TotalVolume;
                lowerIndex--;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Rounds a price to the symbol's tick size.
    /// </summary>
    /// <param name="price">Raw price</param>
    /// <returns>Price rounded to nearest tick size</returns>
    private double RoundToTickSize(double price)
    {
        double tickSize = _symbol.TickSize;
        return Math.Round(price / tickSize) * tickSize;
    }

    /// <summary>
    /// Estimates bar duration in minutes from timeframe.
    /// </summary>
    /// <param name="bars">Bars data series</param>
    /// <returns>Estimated duration in minutes</returns>
    private int GetBarDurationMinutes(Bars bars)
    {
        // Try to get from consecutive bars
        if (bars.Count >= 2)
        {
            var duration = bars.OpenTimes[1] - bars.OpenTimes[0];
            return (int)duration.TotalMinutes;
        }

        // Default fallback
        return 1;
    }
}
