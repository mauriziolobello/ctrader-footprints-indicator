namespace Footprints.Processing;

/// <summary>
/// Classifies individual ticks as buy or sell volume using the uptick/downtick rule.
/// This is a stateful classifier that tracks the previous tick price and classification.
/// </summary>
public class TickClassifier
{
    private double _previousPrice;
    private TickType _previousType;
    private bool _isFirstTick;

    /// <summary>
    /// Creates a new tick classifier.
    /// </summary>
    public TickClassifier()
    {
        Reset();
    }

    /// <summary>
    /// Resets the classifier state (call when starting a new bar).
    /// </summary>
    public void Reset()
    {
        _previousPrice = 0;
        _previousType = TickType.Unknown;
        _isFirstTick = true;
    }

    /// <summary>
    /// Classifies a tick as buy or sell based on price movement.
    ///
    /// Algorithm:
    /// - Uptick (price > previous): Buy
    /// - Downtick (price &lt; previous): Sell
    /// - Zero tick (price == previous): Inherits previous classification
    /// - First tick: Unknown (cannot classify without reference)
    /// </summary>
    /// <param name="currentPrice">Current tick price</param>
    /// <returns>Tick classification (Buy, Sell, or Unknown)</returns>
    public TickType ClassifyTick(double currentPrice)
    {
        // First tick cannot be classified
        if (_isFirstTick)
        {
            _previousPrice = currentPrice;
            _previousType = TickType.Unknown;
            _isFirstTick = false;
            return TickType.Unknown;
        }

        TickType currentType;

        if (currentPrice > _previousPrice)
        {
            // Uptick: Buy
            currentType = TickType.Buy;
        }
        else if (currentPrice < _previousPrice)
        {
            // Downtick: Sell
            currentType = TickType.Sell;
        }
        else
        {
            // Zero tick: inherit previous classification
            currentType = _previousType;
        }

        _previousPrice = currentPrice;
        _previousType = currentType;

        return currentType;
    }

    /// <summary>
    /// Gets the last classified tick type (for debugging/testing).
    /// </summary>
    public TickType GetLastType()
    {
        return _previousType;
    }

    /// <summary>
    /// Gets the last processed price (for debugging/testing).
    /// </summary>
    public double GetLastPrice()
    {
        return _previousPrice;
    }
}

/// <summary>
/// Type of tick classification based on price movement.
/// </summary>
public enum TickType
{
    /// <summary>
    /// Cannot be classified (first tick or insufficient data).
    /// </summary>
    Unknown,

    /// <summary>
    /// Buy tick (uptick: price increased).
    /// </summary>
    Buy,

    /// <summary>
    /// Sell tick (downtick: price decreased).
    /// </summary>
    Sell
}
