using System;
using System.Collections.Generic;
using System.Linq;

namespace Footprints.Domain;

/// <summary>
/// Represents footprint data for a single candlestick bar, containing volume distribution across price levels.
/// </summary>
public class FootprintBar
{
    /// <summary>
    /// The bar's opening time (used as unique identifier).
    /// </summary>
    public DateTime BarTime { get; }

    /// <summary>
    /// Dictionary mapping price to price level data.
    /// Key: price (rounded to tick size), Value: PriceLevel object.
    /// </summary>
    public Dictionary<double, PriceLevel> PriceLevels { get; }

    /// <summary>
    /// Total buy volume across all price levels in this bar.
    /// </summary>
    public long TotalBuyVolume { get; private set; }

    /// <summary>
    /// Total sell volume across all price levels in this bar.
    /// </summary>
    public long TotalSellVolume { get; private set; }

    /// <summary>
    /// Total volume for the entire bar (buy + sell).
    /// </summary>
    public long TotalVolume => TotalBuyVolume + TotalSellVolume;

    /// <summary>
    /// Delta (buy volume - sell volume) for the entire bar.
    /// </summary>
    public long Delta => TotalBuyVolume - TotalSellVolume;

    /// <summary>
    /// The Point of Control (price level with highest total volume).
    /// </summary>
    public PriceLevel PointOfControl { get; private set; }

    /// <summary>
    /// Highest price in the Value Area (covering 70% of volume).
    /// </summary>
    public double ValueAreaHigh { get; private set; }

    /// <summary>
    /// Lowest price in the Value Area (covering 70% of volume).
    /// </summary>
    public double ValueAreaLow { get; private set; }

    /// <summary>
    /// List of price levels with significant imbalances.
    /// </summary>
    public List<PriceLevel> ImbalanceLevels { get; }

    /// <summary>
    /// Aggregated price bins for improved readability.
    /// When populated, rendering should prefer Bins over individual PriceLevels.
    /// </summary>
    public List<FootprintBin> Bins { get; set; }

    /// <summary>
    /// Highest price (OHLC High) for this bar. Set during bin aggregation.
    /// </summary>
    public double High { get; set; }

    /// <summary>
    /// Lowest price (OHLC Low) for this bar. Set during bin aggregation.
    /// </summary>
    public double Low { get; set; }

    /// <summary>
    /// Creates a new footprint bar.
    /// </summary>
    /// <param name="barTime">The bar's opening time</param>
    public FootprintBar(DateTime barTime)
    {
        BarTime = barTime;
        PriceLevels = new Dictionary<double, PriceLevel>();
        ImbalanceLevels = new List<PriceLevel>();
        Bins = new List<FootprintBin>();
        TotalBuyVolume = 0;
        TotalSellVolume = 0;
    }

    /// <summary>
    /// Adds tick volume to the appropriate price level.
    /// Creates a new price level if it doesn't exist.
    /// </summary>
    /// <param name="price">The price (should be rounded to tick size)</param>
    /// <param name="volume">Volume to add</param>
    /// <param name="isBuy">True if buy volume (uptick), false if sell volume (downtick)</param>
    public void AddTickVolume(double price, long volume, bool isBuy)
    {
        if (!PriceLevels.ContainsKey(price))
        {
            PriceLevels[price] = new PriceLevel(price);
        }

        PriceLevels[price].AddVolume(volume, isBuy);

        // Update totals
        if (isBuy)
            TotalBuyVolume += volume;
        else
            TotalSellVolume += volume;
    }

    /// <summary>
    /// Calculates the Point of Control (price level with maximum total volume).
    /// </summary>
    public void CalculatePOC()
    {
        if (PriceLevels.Count == 0)
        {
            PointOfControl = null;
            return;
        }

        // Reset previous POC flag
        if (PointOfControl != null)
            PointOfControl.IsPointOfControl = false;

        // Find level with maximum volume
        PointOfControl = PriceLevels.Values
            .OrderByDescending(pl => pl.TotalVolume)
            .FirstOrDefault();

        if (PointOfControl != null)
            PointOfControl.IsPointOfControl = true;
    }

    /// <summary>
    /// Calculates the Value Area (price range containing specified percentage of total volume).
    /// Expands from POC until reaching the target volume percentage.
    /// </summary>
    /// <param name="percentage">Target volume percentage (default 70%)</param>
    public void CalculateValueArea(double percentage = 70.0)
    {
        if (PointOfControl == null || PriceLevels.Count == 0)
        {
            ValueAreaHigh = 0;
            ValueAreaLow = 0;
            return;
        }

        // Reset previous value area flags
        foreach (var level in PriceLevels.Values)
            level.IsInValueArea = false;

        long targetVolume = (long)(TotalVolume * (percentage / 100.0));
        long accumulatedVolume = 0;

        // Start from POC
        var orderedLevels = PriceLevels.Values.OrderBy(pl => pl.Price).ToList();
        int pocIndex = orderedLevels.IndexOf(PointOfControl);

        // Mark POC as in value area
        PointOfControl.IsInValueArea = true;
        accumulatedVolume += PointOfControl.TotalVolume;

        // Expand up and down from POC
        int upperIndex = pocIndex + 1;
        int lowerIndex = pocIndex - 1;

        while (accumulatedVolume < targetVolume && (upperIndex < orderedLevels.Count || lowerIndex >= 0))
        {
            PriceLevel upperLevel = upperIndex < orderedLevels.Count ? orderedLevels[upperIndex] : null;
            PriceLevel lowerLevel = lowerIndex >= 0 ? orderedLevels[lowerIndex] : null;

            // Choose direction with more volume
            if (upperLevel != null && lowerLevel != null)
            {
                if (upperLevel.TotalVolume >= lowerLevel.TotalVolume)
                {
                    upperLevel.IsInValueArea = true;
                    accumulatedVolume += upperLevel.TotalVolume;
                    upperIndex++;
                }
                else
                {
                    lowerLevel.IsInValueArea = true;
                    accumulatedVolume += lowerLevel.TotalVolume;
                    lowerIndex--;
                }
            }
            else if (upperLevel != null)
            {
                upperLevel.IsInValueArea = true;
                accumulatedVolume += upperLevel.TotalVolume;
                upperIndex++;
            }
            else if (lowerLevel != null)
            {
                lowerLevel.IsInValueArea = true;
                accumulatedVolume += lowerLevel.TotalVolume;
                lowerIndex--;
            }
            else
            {
                break;
            }
        }

        // Set value area bounds
        var valueAreaLevels = PriceLevels.Values.Where(pl => pl.IsInValueArea).ToList();
        if (valueAreaLevels.Any())
        {
            ValueAreaHigh = valueAreaLevels.Max(pl => pl.Price);
            ValueAreaLow = valueAreaLevels.Min(pl => pl.Price);
        }
        else
        {
            ValueAreaHigh = PointOfControl.Price;
            ValueAreaLow = PointOfControl.Price;
        }
    }

    /// <summary>
    /// Detects price levels with significant imbalances between buy and sell volume.
    /// </summary>
    /// <param name="thresholdPercentage">Minimum imbalance ratio (default 300% = 3x)</param>
    public void DetectImbalances(double thresholdPercentage = 300.0)
    {
        ImbalanceLevels.Clear();

        foreach (var level in PriceLevels.Values)
        {
            // Reset previous imbalance flags
            level.HasImbalance = false;
            level.ImbalanceType = ImbalanceType.None;

            // Skip levels with no volume
            if (level.TotalVolume == 0)
                continue;

            long maxVol = Math.Max(level.BuyVolume, level.SellVolume);
            long minVol = Math.Min(level.BuyVolume, level.SellVolume);

            // Avoid division by zero
            if (minVol == 0)
            {
                // One side has volume, the other doesn't - clear imbalance
                level.HasImbalance = true;
                level.ImbalanceType = level.BuyVolume > 0 ? ImbalanceType.Buy : ImbalanceType.Sell;
                ImbalanceLevels.Add(level);
                continue;
            }

            // Calculate ratio: max / min (expressed as percentage)
            double ratio = ((double)maxVol / minVol) * 100.0;

            if (ratio >= thresholdPercentage)
            {
                level.HasImbalance = true;
                level.ImbalanceType = level.BuyVolume > level.SellVolume ? ImbalanceType.Buy : ImbalanceType.Sell;
                ImbalanceLevels.Add(level);
            }
        }
    }

    /// <summary>
    /// Gets all price levels sorted by price (ascending).
    /// </summary>
    public List<PriceLevel> GetSortedLevels()
    {
        return PriceLevels.Values.OrderBy(pl => pl.Price).ToList();
    }

    /// <summary>
    /// Gets the highest price across all levels.
    /// </summary>
    public double GetHighestPrice()
    {
        return PriceLevels.Count > 0 ? PriceLevels.Keys.Max() : 0;
    }

    /// <summary>
    /// Gets the lowest price across all levels.
    /// </summary>
    public double GetLowestPrice()
    {
        return PriceLevels.Count > 0 ? PriceLevels.Keys.Min() : 0;
    }
}
