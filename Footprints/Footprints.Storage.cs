using System;
using System.Collections.Generic;
using cAlgo.API;
using Footprints.Domain;
using Footprints.Processing;
using Footprints.Storage;

namespace cAlgo.Indicators;

/// <summary>
/// Footprints Indicator - LocalStorage Persistence partial class.
/// Manages tick data persistence using cTrader's LocalStorage API so that
/// historical tick classifications survive chart refreshes, reloads, and
/// timeframe changes. Ticks are stored per-symbol and can be reaggregated
/// to any timeframe on reload.
/// </summary>
public partial class Footprints
{
    /// <summary>
    /// Tick storage manager holding classified ticks for persistence.
    /// </summary>
    private FootprintTickStorage _tickStorage;

    /// <summary>
    /// LocalStorage key for the current symbol (e.g., "Footprint_EURUSD").
    /// </summary>
    private string _storageKey;

    /// <summary>
    /// Counter for periodic save operations. Saves every N processed ticks
    /// to balance between data safety and I/O performance.
    /// </summary>
    private int _ticksSinceLastSave;

    /// <summary>
    /// Number of ticks to process between automatic save operations.
    /// </summary>
    private const int SAVE_INTERVAL_TICKS = 500;

    /// <summary>
    /// Maximum gap between the last stored tick and current time before
    /// the stored data is considered stale and discarded.
    /// A 2-hour gap indicates the indicator was offline too long to have
    /// useful continuity, preventing old ticks from creating distant bars.
    /// </summary>
    private static readonly TimeSpan MAX_TICK_GAP = TimeSpan.FromHours(2);

    /// <summary>
    /// Maximum gap between a bar's open time and its stored ticks before
    /// those ticks are considered too old for that specific bar.
    /// Prevents reaggregation of stale ticks into bars at wrong price levels.
    /// </summary>
    private static readonly TimeSpan MAX_BAR_TICK_GAP = TimeSpan.FromHours(1);

    /// <summary>
    /// Initializes LocalStorage and loads existing tick data for the current symbol.
    /// Uses Device scope so data persists across indicator reloads and timeframe changes.
    /// If a gap greater than MAX_TICK_GAP is detected, stored data is discarded.
    /// </summary>
    private void InitializeStorage()
    {
        _storageKey = FootprintTickStorage.GenerateStorageKey(SymbolName);
        _ticksSinceLastSave = 0;
        // Print($"[Storage] Key: {_storageKey}");

        try
        {
            // Use Device scope to retrieve data persisted across reloads
            string data = LocalStorage.GetString(_storageKey, LocalStorageScope.Device);

            if (!string.IsNullOrEmpty(data))
            {
                _tickStorage = FootprintTickStorage.Deserialize(data);

                if (_tickStorage != null && _tickStorage.Count > 0)
                {
                    // Check for gap between last stored tick and now
                    TimeSpan gap = DateTime.UtcNow - _tickStorage.LastTickTime;

                    if (gap > MAX_TICK_GAP)
                    {
                        // Print($"[Storage] Gap of {gap.TotalHours:F1} hours detected - discarding stale data");
                        _tickStorage = new FootprintTickStorage(SymbolName);
                    }
                    else
                    {
                        // Print($"[Storage] Loaded {_tickStorage.Count} ticks from storage (last tick: {_tickStorage.LastTickTime:yyyy-MM-dd HH:mm:ss} UTC)");
                    }
                }
                else
                {
                    // Print("[Storage] Stored data was empty or could not be deserialized");
                    _tickStorage = new FootprintTickStorage(SymbolName);
                }
            }
            else
            {
                // Print("[Storage] No existing data found");
            }
        }
        catch (Exception ex)
        {
            Print($"[Storage] Error loading: {ex.Message}");  // Keep error logs
        }

        // Create new storage if not loaded
        if (_tickStorage == null)
        {
            _tickStorage = new FootprintTickStorage(SymbolName);
            // Print("[Storage] Created new empty storage");
        }
    }

    /// <summary>
    /// Saves tick data to LocalStorage using Device scope.
    /// Device scope ensures data survives indicator reloads and is accessible
    /// from any instance on this machine.
    /// Calls Flush() to ensure immediate persistence (avoids the 1-minute auto-save delay).
    /// </summary>
    private void SaveStorageData()
    {
        if (_tickStorage == null || _tickStorage.Count == 0)
            return;

        try
        {
            string data = _tickStorage.Serialize();
            LocalStorage.SetString(_storageKey, data, LocalStorageScope.Device);
            LocalStorage.Flush(LocalStorageScope.Device);
            // Print($"[Storage] Saved {_tickStorage.Count} ticks ({data.Length} chars)");
        }
        catch (Exception ex)
        {
            Print($"[Storage] Error saving: {ex.Message}");  // Keep error logs
        }
    }

    /// <summary>
    /// Stores a classified tick in the persistence layer.
    /// Called from tick processing logic after classification.
    /// Triggers periodic saves based on SAVE_INTERVAL_TICKS.
    /// </summary>
    /// <param name="timestamp">Tick timestamp (UTC)</param>
    /// <param name="price">Tick price (mid price)</param>
    /// <param name="classification">Tick classification from the uptick/downtick rule</param>
    private void StoreProcessedTick(DateTime timestamp, double price, TickClassification classification)
    {
        if (_tickStorage == null)
            return;

        _tickStorage.AddTick(timestamp, price, classification);

        _ticksSinceLastSave++;
        if (_ticksSinceLastSave >= SAVE_INTERVAL_TICKS)
        {
            SaveStorageData();
            _ticksSinceLastSave = 0;
        }
    }

    /// <summary>
    /// Checks if stored ticks for a bar are temporally valid (no large time gap).
    /// Returns false if the stored ticks should be discarded due to a time gap
    /// between the bar's open time and the most recent stored tick for that bar.
    /// This prevents old ticks from creating distant bars at stale price levels.
    /// </summary>
    /// <param name="barOpenTime">The bar's open time</param>
    /// <param name="storedTicks">Stored ticks retrieved for this bar's time range</param>
    /// <returns>True if ticks are valid and should be used; false if they should be discarded</returns>
    private bool AreStoredTicksValid(DateTime barOpenTime, IEnumerable<FootprintTickData> storedTicks)
    {
        if (storedTicks == null)
            return true;

        // Materialize to check contents without multiple enumeration
        FootprintTickData mostRecentTick = null;
        foreach (FootprintTickData tick in storedTicks)
        {
            if (mostRecentTick == null || tick.Timestamp > mostRecentTick.Timestamp)
                mostRecentTick = tick;
        }

        if (mostRecentTick == null)
            return true; // No ticks, nothing to validate

        // Check gap between bar open time and most recent stored tick
        TimeSpan tickGap = (barOpenTime - mostRecentTick.Timestamp).Duration();

        if (tickGap > MAX_BAR_TICK_GAP)
        {
            // Print($"[Storage] Bar gap detected: {tickGap.TotalMinutes:F1} minutes - discarding ticks for bar {barOpenTime:yyyy-MM-dd HH:mm:ss}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Converts a TickType (from TickClassifier) to a TickClassification (for storage).
    /// Maps Buy to Uptick and Sell to Downtick.
    /// </summary>
    /// <param name="tickType">TickType from the TickClassifier</param>
    /// <returns>Corresponding TickClassification for storage</returns>
    private static TickClassification ConvertTickTypeToClassification(TickType tickType)
    {
        switch (tickType)
        {
            case TickType.Buy:
                return TickClassification.Uptick;
            case TickType.Sell:
                return TickClassification.Downtick;
            default:
                return TickClassification.Unknown;
        }
    }
}
