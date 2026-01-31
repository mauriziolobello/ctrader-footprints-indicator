using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Footprints.Domain;

namespace Footprints.Storage;

/// <summary>
/// Manages tick storage for footprint data with LocalStorage persistence.
/// Stores individual classified ticks that can be reaggregated to any timeframe.
/// Based on the CVD TickStorageData pattern for cTrader LocalStorage.
///
/// Serialization format:
///   Header line: "FP1|symbol|lastTickTicks|tickCount"
///   Data lines:  "timestamp_ticks|price_F8|classification_int"
///   Lines separated by newline character.
///
/// Storage limits:
///   - Maximum 100,000 ticks stored (approximately 4MB serialized)
///   - Ticks older than 7 days are automatically purged
/// </summary>
public class FootprintTickStorage
{
    /// <summary>
    /// Header version identifier for format compatibility checking.
    /// </summary>
    private const string HEADER_VERSION = "FP1";

    /// <summary>
    /// Record separator character between header and tick data lines.
    /// </summary>
    private const char RECORD_SEPARATOR = '\n';

    /// <summary>
    /// Maximum number of ticks to retain in storage.
    /// Prevents unbounded memory and storage growth.
    /// </summary>
    private const int MAX_TICKS_TO_STORE = 100000;

    /// <summary>
    /// Maximum age of ticks before automatic cleanup.
    /// </summary>
    private static readonly TimeSpan MAX_TICK_AGE = TimeSpan.FromDays(7);

    /// <summary>
    /// Number of decimal places used when serializing tick prices.
    /// 8 decimals covers all instrument types (forex, crypto, indices).
    /// </summary>
    private const int PRICE_DECIMAL_PLACES = 8;

    /// <summary>
    /// Symbol name associated with this storage instance.
    /// </summary>
    public string Symbol { get; private set; }

    /// <summary>
    /// Internal list of stored ticks, ordered chronologically.
    /// </summary>
    private List<FootprintTickData> _ticks;

    /// <summary>
    /// Timestamp of the most recently added tick.
    /// Used for gap detection on reload.
    /// </summary>
    public DateTime LastTickTime { get; set; }

    /// <summary>
    /// Number of ticks currently stored.
    /// </summary>
    public int Count => _ticks.Count;

    /// <summary>
    /// Creates a new empty tick storage instance.
    /// </summary>
    public FootprintTickStorage()
    {
        _ticks = new List<FootprintTickData>();
        LastTickTime = DateTime.MinValue;
    }

    /// <summary>
    /// Creates a new tick storage instance for the specified symbol.
    /// </summary>
    /// <param name="symbol">Trading symbol name (e.g., "EURUSD")</param>
    public FootprintTickStorage(string symbol) : this()
    {
        Symbol = symbol;
    }

    /// <summary>
    /// Adds a new classified tick to storage.
    /// Unknown ticks are silently ignored.
    /// Triggers cleanup if storage limits are exceeded.
    /// </summary>
    /// <param name="timestamp">Tick timestamp (UTC)</param>
    /// <param name="price">Tick price (mid price)</param>
    /// <param name="classification">Tick classification (Uptick/Downtick/ZeroTick)</param>
    public void AddTick(DateTime timestamp, double price, TickClassification classification)
    {
        if (classification == TickClassification.Unknown)
            return;

        FootprintTickData tick = new FootprintTickData(timestamp, price, classification);
        _ticks.Add(tick);
        LastTickTime = timestamp;

        // Periodic cleanup (every 1000 ticks to avoid per-tick overhead)
        if (_ticks.Count % 1000 == 0)
        {
            CleanupOldTicks();
        }
    }

    /// <summary>
    /// Removes old ticks to keep storage within limits.
    /// First removes ticks older than MAX_TICK_AGE, then trims
    /// by count if still exceeding MAX_TICKS_TO_STORE.
    /// </summary>
    private void CleanupOldTicks()
    {
        // Remove by age
        DateTime cutoffTime = DateTime.UtcNow - MAX_TICK_AGE;
        _ticks.RemoveAll(t => t.Timestamp < cutoffTime);

        // Remove by count (keep most recent)
        if (_ticks.Count > MAX_TICKS_TO_STORE)
        {
            int toRemove = _ticks.Count - MAX_TICKS_TO_STORE;
            _ticks.RemoveRange(0, toRemove);
        }
    }

    /// <summary>
    /// Gets all ticks that fall within a specific bar's time range.
    /// Used for reaggregating stored ticks into footprint bars
    /// when the chart is refreshed or timeframe is changed.
    /// </summary>
    /// <param name="barOpenTime">Bar open time (inclusive)</param>
    /// <param name="barCloseTime">Bar close time (exclusive)</param>
    /// <returns>Enumerable of ticks within the time range</returns>
    public IEnumerable<FootprintTickData> GetTicksForBar(DateTime barOpenTime, DateTime barCloseTime)
    {
        return _ticks.Where(t => t.Timestamp >= barOpenTime && t.Timestamp < barCloseTime);
    }

    /// <summary>
    /// Gets the timestamp of the earliest stored tick.
    /// Returns DateTime.MaxValue if no ticks are stored.
    /// </summary>
    /// <returns>Earliest tick timestamp</returns>
    public DateTime GetEarliestTickTime()
    {
        if (_ticks.Count == 0)
            return DateTime.MaxValue;

        return _ticks[0].Timestamp;
    }

    /// <summary>
    /// Clears all stored tick data and resets LastTickTime.
    /// </summary>
    public void Clear()
    {
        _ticks.Clear();
        LastTickTime = DateTime.MinValue;
    }

    /// <summary>
    /// Serializes the tick storage to a string for LocalStorage persistence.
    /// Format: "FP1|symbol|lastTickTicks|tickCount\ntimestamp|price|classification\n..."
    /// Uses InvariantCulture for price formatting to avoid locale-dependent decimal separators.
    /// </summary>
    /// <returns>Serialized string representation of all stored ticks</returns>
    public string Serialize()
    {
        // Force cleanup before serialization to minimize storage size
        CleanupOldTicks();

        StringBuilder sb = new StringBuilder();

        // Header line
        sb.Append(HEADER_VERSION);
        sb.Append('|');
        sb.Append(Symbol ?? string.Empty);
        sb.Append('|');
        sb.Append(LastTickTime.Ticks.ToString(CultureInfo.InvariantCulture));
        sb.Append('|');
        sb.Append(_ticks.Count.ToString(CultureInfo.InvariantCulture));

        // Data records
        foreach (FootprintTickData tick in _ticks)
        {
            sb.Append(RECORD_SEPARATOR);
            sb.Append(tick.Timestamp.Ticks.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(tick.Price.ToString($"F{PRICE_DECIMAL_PLACES}", CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(((int)tick.Classification).ToString(CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Deserializes a tick storage instance from a LocalStorage string.
    /// Returns null if the data is empty, malformed, or uses an incompatible version.
    /// </summary>
    /// <param name="data">Serialized string from LocalStorage</param>
    /// <returns>Deserialized FootprintTickStorage instance, or null on failure</returns>
    public static FootprintTickStorage Deserialize(string data)
    {
        if (string.IsNullOrEmpty(data))
            return null;

        string[] lines = data.Split(RECORD_SEPARATOR);
        if (lines.Length == 0)
            return null;

        // Parse header: "FP1|symbol|lastTickTicks|tickCount"
        string[] headerParts = lines[0].Split('|');
        if (headerParts.Length < 4 || headerParts[0] != HEADER_VERSION)
            return null;

        if (!long.TryParse(headerParts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long lastTickTicks))
            return null;

        if (!int.TryParse(headerParts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int expectedCount))
            return null;

        FootprintTickStorage result = new FootprintTickStorage
        {
            Symbol = headerParts[1],
            LastTickTime = new DateTime(lastTickTicks, DateTimeKind.Utc)
        };

        // Pre-allocate list capacity for efficiency
        if (expectedCount > 0 && expectedCount <= MAX_TICKS_TO_STORE)
        {
            result._ticks = new List<FootprintTickData>(expectedCount);
        }

        // Parse data records
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] parts = lines[i].Split('|');
            if (parts.Length != 3)
                continue;

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long tickTicks))
                continue;

            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double price))
                continue;

            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int classification))
                continue;

            // Validate classification range
            if (classification < 0 || classification > 3)
                continue;

            DateTime timestamp = new DateTime(tickTicks, DateTimeKind.Utc);
            TickClassification tickClass = (TickClassification)classification;

            FootprintTickData tick = new FootprintTickData(timestamp, price, tickClass);
            result._ticks.Add(tick);
        }

        // Cleanup after loading (remove expired ticks)
        result.CleanupOldTicks();

        return result;
    }

    /// <summary>
    /// Generates a LocalStorage key for the given symbol name.
    /// Strips non-alphanumeric characters to create a safe key.
    /// Uses space separator as required by cTrader LocalStorage (underscores not allowed).
    /// Example: "EUR/USD" becomes "Footprint EURUSD".
    /// </summary>
    /// <param name="symbol">Trading symbol name</param>
    /// <returns>Safe LocalStorage key string</returns>
    public static string GenerateStorageKey(string symbol)
    {
        StringBuilder sanitized = new StringBuilder();
        foreach (char c in symbol)
        {
            if (char.IsLetterOrDigit(c))
                sanitized.Append(c);
        }

        return $"Footprint {sanitized}";
    }
}
