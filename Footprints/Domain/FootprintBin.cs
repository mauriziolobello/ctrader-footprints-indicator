namespace Footprints.Domain;

/// <summary>
/// Represents an aggregated price bin within a footprint bar.
/// Instead of showing volume at each individual tick/price level,
/// the bar's High-Low range is divided into N equal-sized bins,
/// and all ticks falling within each bin are aggregated.
/// This dramatically improves readability (e.g., 5 lines instead of 50+).
/// </summary>
public class FootprintBin
{
    /// <summary>
    /// Bottom price of the bin (inclusive).
    /// </summary>
    public double PriceBottom { get; set; }

    /// <summary>
    /// Top price of the bin (exclusive, except for the topmost bin).
    /// </summary>
    public double PriceTop { get; set; }

    /// <summary>
    /// Midpoint price of the bin (used for text positioning on chart).
    /// </summary>
    public double PriceMid => (PriceTop + PriceBottom) / 2.0;

    /// <summary>
    /// Aggregated buy (uptick) volume for all ticks in this bin.
    /// </summary>
    public long BuyVolume { get; set; }

    /// <summary>
    /// Aggregated sell (downtick) volume for all ticks in this bin.
    /// </summary>
    public long SellVolume { get; set; }

    /// <summary>
    /// Total volume in this bin (buy + sell).
    /// </summary>
    public long TotalVolume => BuyVolume + SellVolume;

    /// <summary>
    /// Delta between buy and sell volume (BuyVolume - SellVolume).
    /// </summary>
    public long Delta => BuyVolume - SellVolume;

    /// <summary>
    /// Whether this bin has a significant imbalance between buy and sell volume.
    /// </summary>
    public bool HasImbalance { get; set; }

    /// <summary>
    /// Type of imbalance detected in this bin (None, Buy, or Sell).
    /// </summary>
    public ImbalanceType ImbalanceType { get; set; }

    /// <summary>
    /// Whether this bin is the Point of Control (highest total volume bin).
    /// </summary>
    public bool IsPointOfControl { get; set; }

    /// <summary>
    /// Whether this bin is within the Value Area.
    /// </summary>
    public bool IsInValueArea { get; set; }

    /// <summary>
    /// Buy-to-sell ratio for this bin.
    /// Returns double.MaxValue if sell volume is zero with non-zero buy volume.
    /// Returns 1.0 if both are zero.
    /// </summary>
    public double Ratio
    {
        get
        {
            if (SellVolume == 0)
                return BuyVolume > 0 ? double.MaxValue : 1.0;
            return (double)BuyVolume / SellVolume;
        }
    }

    /// <summary>
    /// Creates a new empty footprint bin.
    /// </summary>
    public FootprintBin()
    {
        BuyVolume = 0;
        SellVolume = 0;
        HasImbalance = false;
        ImbalanceType = ImbalanceType.None;
        IsPointOfControl = false;
        IsInValueArea = false;
    }
}
