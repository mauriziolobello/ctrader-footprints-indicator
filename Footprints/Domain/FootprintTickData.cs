using System;

namespace Footprints.Domain;

/// <summary>
/// Represents a single tick with timestamp and classification.
/// Used for LocalStorage persistence so tick data survives chart
/// refreshes and timeframe changes.
/// </summary>
public class FootprintTickData
{
    /// <summary>
    /// Exact timestamp of the tick (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Tick price (mid price: average of bid and ask).
    /// </summary>
    public double Price { get; set; }

    /// <summary>
    /// Tick classification: Uptick, Downtick, or ZeroTick.
    /// Determines whether the tick is counted as buy or sell volume.
    /// </summary>
    public TickClassification Classification { get; set; }

    /// <summary>
    /// Creates a new empty tick data instance.
    /// </summary>
    public FootprintTickData()
    {
    }

    /// <summary>
    /// Creates a new tick data instance with all fields populated.
    /// </summary>
    /// <param name="timestamp">Exact timestamp of the tick (UTC)</param>
    /// <param name="price">Tick price (mid price)</param>
    /// <param name="classification">Tick classification (Uptick/Downtick/ZeroTick)</param>
    public FootprintTickData(DateTime timestamp, double price, TickClassification classification)
    {
        Timestamp = timestamp;
        Price = price;
        Classification = classification;
    }
}

/// <summary>
/// Tick classification for uptick/downtick rule.
/// Maps to the existing TickType enum in TickClassifier but is used
/// specifically for storage persistence with explicit integer values.
/// </summary>
public enum TickClassification
{
    /// <summary>
    /// Cannot be classified (first tick or insufficient data).
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Price increased compared to previous tick (Buy volume).
    /// </summary>
    Uptick = 1,

    /// <summary>
    /// Price decreased compared to previous tick (Sell volume).
    /// </summary>
    Downtick = 2,

    /// <summary>
    /// Price unchanged from previous tick (inherits previous classification).
    /// </summary>
    ZeroTick = 3
}
