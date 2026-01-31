## 🏗️ Principi Architetturali

### Organizzazione Classi

1. **Classi Separate per Entità**
   - Ogni entità ha la propria classe dedicata
   - File separato per ogni classe
   - Favorisce testabilità, manutenibilità e separazione delle responsabilità

2. **Partial Class solo per la Classe Principale**
   - La classe dell'indicatore può essere suddivisa in partial class

3. **Non usare Partial Class per Entità aggiuntive**

Questo approccio:
- Elimina duplicazione di codice
- Facilita l'aggiunta di nuove formazioni (Ledge)
- Mantiene consistenza visiva tra formazioni simili

### Namespace

1. **Classe Principale in `cAlgo`**
   - Se crei classi (oltre quella dell'indicatore), definisci un namespace separato da `cAlgo` (richiesto da cTrader)
   - Tutte le classi aggiuntive useranno il nuovo il namespace
   - Questo separa la logica di dominio dal framework cTrader
   - Importare con `using <namespace>;` nei file che ne hanno bisogno

### SOLID Principles

Il progetto segue rigorosamente i principi SOLID:

1. **Single Responsibility Principle (SRP)**
2. **Open/Closed Principle**
3. **Liskov Substitution Principle**
4. **Interface Segregation Principle**
5. **Dependency Inversion Principle**

---

## 🎯 Regole di Codifica

### Naming Conventions

- **Campi privati:** `_camelCase` (es. `_congestionZones`)
- **Proprietà pubbliche:** `PascalCase` (es. `CongestionColor`)
- **Metodi:** `PascalCase` (es. `ProcessBar()`)
- **Parametri:** `PascalCase` nei `[Parameter]`, `camelCase` nei metodi
- **Costanti:** `UPPER_CASE` (es. `MIN_BARS_IN_CONGESTION`)

### Documentazione

- **Tutti i metodi** devono avere `/// <summary>` XML documentation
- **Parametri complessi** devono avere descrizioni dettagliate
- **Logica non ovvia** deve essere commentata inline

### Gestione Oggetti Grafici

**IMPORTANTE:** Tutti gli oggetti grafici (`Chart.Draw*`) devono essere tracciati:

```csharp
// ✅ CORRETTO
zone.Rectangle = Chart.DrawRectangle(...);

// ❌ SBAGLIATO (memory leak)
Chart.DrawRectangle(...);
```

**Cleanup obbligatorio:**
```csharp
if (zone.Rectangle != null)
{
    Chart.RemoveObject(zone.Rectangle.Name);
    zone.Rectangle = null;
}
```

---

## 🔢 Versioning

### Formato Semantico: `major.minor.fix`

- **major:** Cambiamenti architetturali importanti
- **minor:** Nuove funzionalità
- **fix:** Bug fix e piccoli miglioramenti

### Regole di Aggiornamento

**SEMPRE** aggiornare versione quando:
- ✅ Aggiungi nuove funzionalità
- ✅ Correggi bug
- ✅ Modifichi comportamento esistente
- ✅ Aggiungi parametri configurabili

**Aggiorna il changelog in un file separato dal nome CHANGELOG.md:**
```csharp
/// Changelog:
/// 1.1.0 - Refactoring: classe base RangeFormation, rendering unificato
/// 1.0.0 - Implementazione iniziale: Congestion e Trading Range
```

---

## 📝 Unicode e Logging

### Caratteri Speciali

**SEMPRE** usare escape sequences `\uxxxx`:

```csharp
// ✅ CORRETTO
Print("\u2554\u2550\u2557"); // ╔═╗

// ❌ SBAGLIATO (problemi rendering)
Print("╔═╗");
```

### Box Drawing Characters

- Top-left: `\u2554` (╔)
- Top-right: `\u2557` (╗)
- Bottom-left: `\u255A` (╚)
- Bottom-right: `\u255D` (╝)
- Horizontal: `\u2550` (═)
- Vertical: `\u2551` (║)

---

## 🔧 cTrader Specifics

### Vincoli Piattaforma

- **AccessRights:** `None` (no file system, no network)
- **LocalStorage:** Disponibile per persistenza dati
- **No reflection:** Limitazioni su runtime type inspection

### Best Practices

1. **Evita `Thread.Sleep()`** → Blocca UI
2. **Non rimuovere oggetti grafici** se non necessario
3. **Traccia tutti gli oggetti** creati dinamicamente

### ChartArea.SetYRange() - Limitazione Trade Environment

**CRITICO:** `ChartArea.SetYRange()` ha comportamenti diversi tra Algo e Trade:

**Ambiente Algo (Automate):**
- ✅ `ChartArea.SetYRange()` funziona correttamente
- ✅ Auto-zoom dinamico funziona come previsto

**Ambiente Trade:**
- ❌ `ChartArea.SetYRange()` viene **ignorato silenziosamente**
- ❌ Tutte le barre vengono disegnate sovrapposte nello stesso spazio verticale
- ❌ Risultato: grafico illeggibile con elementi impilati

```csharp
// Questo codice funziona SOLO in Algo environment
ChartArea.SetYRange(bottomY, topY);  // Ignorato in Trade!
```

**Soluzione:**
```csharp
// Disabilitare auto-zoom per Trade environment
[Parameter("Enable Auto Zoom", DefaultValue = false, Group = "Zoom")]
public bool EnableAutoZoom { get; set; }

// Aggiungere warning nel log
if (EnableAutoZoom)
{
    Print("WARNING: Auto Zoom may not work in Trade environment");
}
```

**Best practice:**
- Default `EnableAutoZoom = false` per compatibilità Trade
- Documentare la limitazione nel README
- Aggiungere warning nel log se auto-zoom è abilitato
- Utente può abilitare manualmente auto-zoom se usa solo Algo environment

### LocalStorage Key Validation

**CRITICO:** Le chiavi di LocalStorage hanno regole di validazione molto rigide:

**Caratteri permessi:**
- ✅ Lettere latine (a-z, A-Z)
- ✅ Numeri (0-9)
- ✅ Spazi (ma NON all'inizio o alla fine)

**Caratteri NON permessi:**
- ❌ Underscore `_`
- ❌ Trattini `-`
- ❌ Slash `/`
- ❌ Qualsiasi altro carattere speciale

```csharp
// ❌ SBAGLIATO - Genera errore di validazione
string key = "Footprint_BTCUSD";  // underscore non permesso
string key = "Footprint-BTCUSD";  // trattino non permesso

// ✅ CORRETTO - Usa spazi come separatori
string key = "Footprint BTCUSD";  // spazio permesso
string key = "FootprintBTCUSD";   // nessun separatore

// ✅ CORRETTO - Sanificazione del nome simbolo
public static string GenerateStorageKey(string symbol)
{
    StringBuilder sanitized = new StringBuilder();
    foreach (char c in symbol)
    {
        if (char.IsLetterOrDigit(c))
            sanitized.Append(c);
    }
    return $"Footprint {sanitized}";  // Usa spazio come separatore
}
```

**Errore tipico se si viola la regola:**
```
[Storage] Error saving: Le chiavi possono contenere solamente caratteri latini,
numeri e spazi. Non sono consentiti spazi all'inizio e alla fine della chiave. (Parameter 'key')
```

**Best practice:**
- Sanifica sempre i nomi dei simboli rimuovendo caratteri speciali
- Usa spazi o nessun separatore nelle chiavi
- Testa la generazione della chiave con simboli che contengono `/`, `-`, o altri caratteri speciali

### Gestione Indici Candele

**IMPORTANTE:** cTrader reindicizza periodicamente le candele durante refresh/reload del grafico. Gli indici delle barre **NON sono stabili** nel tempo.

**Regola fondamentale:**
- ❌ **NON memorizzare** indici delle candele (`int index`)
- ✅ **Memorizzare** DateTime delle candele (`DateTime barTime`)

```csharp
// ❌ SBAGLIATO - L'indice può cambiare dopo refresh
public int MeasuringBarIndex { get; }

// ✅ CORRETTO - Il DateTime è stabile
public DateTime MeasuringBarTime { get; }
```

**Quando serve l'indice**, ricavarlo al momento:
```csharp
int index = Bars.OpenTimes.GetIndexByTime(barTime);
```

Questo garantisce che l'indicatore funzioni correttamente anche dopo lunghi periodi di esecuzione.

---

## ⚠️ Common Pitfalls

### Memory Leaks

```csharp
// ❌ MEMORY LEAK
for (int i = 0; i < 1000; i++)
{
    Chart.DrawLine(...); // Non tracciato!
}

// ✅ CORRETTO
private List<ChartObject> _lines = new List<ChartObject>();
_lines.Add(Chart.DrawLine(...));
```

### Index Out of Bounds

```csharp
// ❌ PERICOLOSO
int idx = zone.MeasuringBarIndex + 2;
var time = Bars.OpenTimes[idx]; // Può crashare!

// ✅ SICURO
if (zone.MeasuringBarIndex + 2 < Bars.Count)
{
    var time = Bars.OpenTimes[zone.MeasuringBarIndex + 2];
}
```

### Performance

```csharp
// ❌ LENTO (ogni tick)
foreach (var zone in AllZones)
{
    Chart.RemoveObject(...);
    Chart.DrawRectangle(...);
}

// ✅ VELOCE (solo se necessario)
if (zone.NeedsUpdate)
{
    UpdateRectangle(zone);
}
```

---

**Nota:** Questo documento deve essere aggiornato ad ogni cambiamento significativo nell'architettura o nelle convenzioni del progetto.