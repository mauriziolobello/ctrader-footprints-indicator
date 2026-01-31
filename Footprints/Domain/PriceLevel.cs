namespace Footprints.Domain;

/// <summary>
/// Represents a single price level within a footprint bar, containing buy and sell volume data.
/// </summary>
public class PriceLevel
{
    /// <summary>
    /// The price of this level (rounded to tick size).
    /// </summary>
    public double Price { get; }

    /// <summary>
    /// Total buy volume at this price level (uptick volume).
    /// </summary>
    public long BuyVolume { get; set; }

    /// <summary>
    /// Total sell volume at this price level (downtick volume).
    /// </summary>
    public long SellVolume { get; set; }

    /// <summary>
    /// Total volume at this price level (BuyVolume + SellVolume).
    /// </summary>
    public long TotalVolume => BuyVolume + SellVolume;

    /// <summary>
    /// Delta between buy and sell volume (BuyVolume - SellVolume).
    /// </summary>
    public long Delta => BuyVolume - SellVolume;

    /// <summary>
    /// Indicates if this is the Point of Control (highest volume level in the bar).
    /// </summary>
    public bool IsPointOfControl { get; set; }

    /// <summary>
    /// Indicates if this price level is within the Value Area (70% of total volume).
    /// </summary>
    public bool IsInValueArea { get; set; }

    /// <summary>
    /// Indicates if this level has a significant imbalance between buy and sell volume.
    /// </summary>
    public bool HasImbalance { get; set; }

    /// <summary>
    /// Type of imbalance detected at this level.
    /// </summary>
    public ImbalanceType ImbalanceType { get; set; }

    /// <summary>
    /// Creates a new price level.
    /// </summary>
    /// <param name="price">The price for this level (should be rounded to tick size)</param>
    public PriceLevel(double price)
    {
        Price = price;
        BuyVolume = 0;
        SellVolume = 0;
        IsPointOfControl = false;
        IsInValueArea = false;
        HasImbalance = false;
        ImbalanceType = ImbalanceType.None;
    }

    /// <summary>
    /// Adds volume to this price level based on tick classification.
    /// </summary>
    /// <param name="volume">Volume to add</param>
    /// <param name="isBuy">True if this is buy volume (uptick), false if sell volume (downtick)</param>
    public void AddVolume(long volume, bool isBuy)
    {
        if (isBuy)
            BuyVolume += volume;
        else
            SellVolume += volume;
    }

    /// <summary>
    /// Calculates the buy/sell ratio for this level.
    /// Returns 0 if no volume exists.
    /// </summary>
    public double GetBuySellRatio()
    {
        if (SellVolume == 0 && BuyVolume == 0)
            return 0;

        if (SellVolume == 0)
            return double.MaxValue;

        return (double)BuyVolume / SellVolume;
    }

    /// <summary>
    /// Gets the dominant side (buy or sell) based on volume.
    /// </summary>
    public string GetDominantSide()
    {
        if (BuyVolume > SellVolume)
            return "Buy";
        if (SellVolume > BuyVolume)
            return "Sell";
        return "Neutral";
    }
}

/// <summary>
/// Type of volume imbalance detected at a price level.
/// </summary>
public enum ImbalanceType
{
    /// <summary>
    /// No significant imbalance detected.
    /// </summary>
    None,

    /// <summary>
    /// Buy volume significantly exceeds sell volume.
    /// </summary>
    Buy,

    /// <summary>
    /// Sell volume significantly exceeds buy volume.
    /// </summary>
    Sell
}
