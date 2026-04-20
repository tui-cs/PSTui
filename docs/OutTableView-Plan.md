# OutTableView Implementation Plan

## Overview

Create a new `Out-ConsoleTableView` (OCTV) cmdlet that mirrors `Out-ConsoleGridView` (OCGV) functionality but uses Terminal.Gui's `TableView` instead of `ListView`. This addresses **Issue #209** — streaming pipeline input so rows appear as they arrive (in `ProcessRecord`) rather than waiting until `EndProcessing`.

## Why TableView?

The current OCGV uses `ListView` with a custom `GridViewDataSource` and manual column formatting (`GridViewHelpers.GetPaddedString`, a custom `Header` view, etc.). Terminal.Gui's `TableView` provides all of this natively:

- Built-in column headers with sticky scrolling (`TableStyle.ShowHeaders`, `AlwaysShowHeaders`)
- Column sizing, alignment, and formatting via `ColumnStyle`
- Only renders visible rows — efficient for large/streaming datasets
- `ITableSource` interface enables dynamic data (rows can grow while displayed)
- Multi-cell and full-row selection (`FullRowSelect`, `MultiSelect`)
- Checkbox support via `CheckBoxTableSourceWrapperByIndex`

## Architecture

### New Files

| File | Class | Purpose |
|------|-------|---------|
| `OutConsoleTableViewCmdletCommand.cs` | `OutConsoleTableViewCmdletCommand` | PSCmdlet — streams objects in `ProcessRecord` |
| `OutTableViewWindow.cs` | `OutTableViewWindow : Runnable<HashSet<int>>` | Main UI window using `TableView` |
| `OutTableViewDataSource.cs` | `OutTableViewDataSource : ITableSource` | Thread-safe, dynamically-growing table source |

### Reused Files (no changes needed)

| File | Why |
|------|-----|
| `TypeGetter.cs` | PSObject-to-DataTable conversion (columns, property resolution) |
| `ApplicationData.cs` | Config container (add streaming flag if needed) |
| `DataTable.cs`, `DataTableRow.cs`, `DataTableColumn.cs` | Data model |
| `OutputModeOptions.cs` | `None` / `Single` / `Multiple` enum |
| `GridViewHelpers.cs` | `FilterData` regex logic (adapt for TableView rows) |
| `OutConsoleGridView.cs` | Orchestrator pattern (create a parallel `OutConsoleTableView.cs`) |

### Files NOT needed (replaced by TableView)

| File | Replaced by |
|------|-------------|
| `GridViewDataSource.cs` (IListDataSource) | `OutTableViewDataSource` (ITableSource) |
| `Header.cs` (custom header view) | `TableView`'s built-in headers |
| `GridViewRow.cs` (DisplayString formatting) | `ColumnStyle.RepresentationGetter` / `Format` |

---

## Detailed Design

### Phase 1: Cmdlet with Streaming Pipeline Support (Issue #209)

#### 1.1 — `OutConsoleTableViewCmdletCommand`

```
[Cmdlet(VerbsData.Out, "ConsoleTableView")]
[Alias("octv")]
```

**Parameters** — identical to OCGV:
- `InputObject` (ValueFromPipeline)
- `Title`, `OutputMode`, `Filter`, `MinUI`, `ForceDriver`, `AllProperties`

**Key difference from OCGV:** Move data processing from `EndProcessing` into `ProcessRecord`:

```
BeginProcessing():
    - Validate environment (same as OCGV)
    - Initialize Terminal.Gui application on a background thread
    - Create OutTableViewWindow (initially empty)
    - Start the TG run loop on the background thread

ProcessRecord():
    - Convert each PSObject via TypeGetter (determine columns on first object)
    - Thread-safely add each DataTableRow to OutTableViewDataSource
    - Signal the UI to refresh (Application.Invoke to marshal to UI thread)

EndProcessing():
    - Signal "no more input" to the data source
    - Wait for the UI to close (user presses Enter/Esc)
    - Collect selected indexes and write selected PSObjects to pipeline
```

**Thread safety:** Terminal.Gui runs on its own thread. `ProcessRecord` runs on the pipeline thread. All UI mutations must go through `Application.Invoke()`. The `OutTableViewDataSource` needs a lock around its row list.

#### 1.2 — `OutTableViewDataSource : ITableSource`

A custom `ITableSource` implementation that supports dynamic row addition:

```csharp
class OutTableViewDataSource : ITableSource
{
    // ITableSource
    string[] ColumnNames { get; }
    int Columns => ColumnNames.Length;
    int Rows => _rows.Count;  // grows over time
    object this[int row, int col] => _rows[row].Values[col];

    // Dynamic data
    private readonly List<DataTableRow> _rows;  // guarded by lock
    private readonly ReaderWriterLockSlim _lock;

    void AddRow(DataTableRow row);      // called from pipeline thread
    void SetColumns(DataTableColumn[] columns);  // called once on first object
}
```

**Column discovery:** On the first PSObject, call `TypeGetter` to determine columns. If a later object has *new* properties (heterogeneous pipeline), either:
- (Simple) Ignore new columns — match OCGV behavior
- (Future) Add columns dynamically and backfill nulls

#### 1.3 — `OutTableViewWindow : Runnable<HashSet<int>>`

UI layout:

```
┌─ Out-ConsoleTableView ─────────────────────────────┐
│ Filter: [__________________] [x] All Properties     │  ← TextField (regex)
│                                                      │
│ Name       Status     Id     StartTime              │  ← TableView built-in header
│ ──────────────────────────────────────────────────── │
│ □ svchost   Running   1234   2024-01-01 12:00       │  ← CheckBox column (if OutputMode != None)
│ □ explorer  Running   5678   2024-01-01 12:01       │
│   ...                                               │
│                                                      │
│ [Streaming: 1,234 rows received]                     │  ← Status indicator
├──────────────────────────────────────────────────────┤
│ Space:Mark  ^A:All  ^D:None  Enter:Accept  Esc:Close │  ← StatusBar
└──────────────────────────────────────────────────────┘
```

**Key TableView configuration:**
```csharp
var tableView = new TableView(dataSource)
{
    FullRowSelect = true,
    MultiSelect = (outputMode == OutputModeOption.Multiple),
    Style = new TableStyle
    {
        ShowHeaders = true,
        AlwaysShowHeaders = true,
        ExpandLastColumn = true,
        ShowHorizontalHeaderUnderline = true,
    }
};
```

**Selection/marking via CheckBox wrapper:**
- For `OutputMode.Single` or `Multiple`: wrap `OutTableViewDataSource` in `CheckBoxTableSourceWrapperByIndex`
- This adds a checkbox column automatically
- Use `CellToggled` event to track selections
- `Ctrl+A` / `Ctrl+D` mapped to select-all / select-none

**Filtering:**
- On filter text change, create a new filtered `OutTableViewDataSource` containing only matching rows
- Set `tableView.Table = filteredSource`
- Preserve check state across filter changes (track by `OriginalObjectIndex`)

**Streaming status:**
- While pipeline is still sending data, show row count in StatusBar
- On `EndProcessing` signal, update status to final count

### Phase 2: Polish & Parity

#### 2.1 — MinUI Mode
- Remove frame, filter TextField, and StatusBar (same as OCGV)
- TableView only

#### 2.2 — AllProperties Toggle
- Checkbox in filter bar (Alt+A)
- On toggle: re-run `TypeGetter.CastObjectsToTableView` with `allProperties: true`
- Rebuild columns and data source

#### 2.3 — Keyboard Bindings
Match OCGV behavior:
| Key | Action |
|-----|--------|
| Space | Toggle mark on current row |
| Enter | Accept selection, close |
| Esc | Cancel, close (return nothing) |
| Ctrl+A | Mark all (Multiple mode) |
| Ctrl+D | Unmark all |
| Alt+A | Toggle AllProperties |
| Up/Down/PgUp/PgDn/Home/End | Navigation (TableView built-in) |

#### 2.4 — Column Width Calculation
- Use `ColumnStyle.MinWidth` / `MaxWidth` per column
- Calculate natural widths from first N rows (not all, for streaming perf)
- Recalculate periodically as new rows arrive (or on user request)

---

## Implementation Order

### Step 1: Skeleton cmdlet + static TableView (no streaming)
1. Create `OutConsoleTableViewCmdletCommand.cs` — copy OCGV cmdlet, change names
2. Create `OutTableViewDataSource.cs` — implement `ITableSource` wrapping `DataTable`
3. Create `OutTableViewWindow.cs` — TableView with filter, status bar, selection
4. Create `OutConsoleTableView.cs` — orchestrator (copy from `OutConsoleGridView.cs`)
5. Register cmdlet in module manifest
6. **Goal:** `Get-Process | Out-ConsoleTableView` works identically to OCGV

### Step 2: Selection & marking
1. Integrate `CheckBoxTableSourceWrapperByIndex` for mark support
2. Implement `Accept()` / `Close()` returning selected indexes
3. Wire up Ctrl+A, Ctrl+D, Space, Enter, Esc
4. **Goal:** `Get-Process | Out-ConsoleTableView -OutputMode Multiple` returns selected objects

### Step 3: Filtering
1. Add regex filter TextField
2. On filter change, rebuild a filtered ITableSource
3. Preserve marks across filter changes
4. **Goal:** Filtering works as in OCGV

### Step 4: Streaming pipeline (Issue #209)
1. Refactor cmdlet to start UI in `BeginProcessing` on background thread
2. Move object processing to `ProcessRecord`
3. Add thread-safe `AddRow` to `OutTableViewDataSource`
4. Marshal UI updates via `Application.Invoke`
5. Add streaming row count indicator
6. **Goal:** `1..100 | % { Start-Sleep -Milliseconds 100; $_ } | Out-ConsoleTableView` shows rows as they arrive

### Step 5: Polish
1. MinUI mode
2. AllProperties toggle
3. Column width auto-sizing with periodic recalculation
4. ForceDriver support
5. Error handling parity with OCGV

### Step 6: Tests
1. Port existing OCGV tests to OCTV equivalents
2. Add streaming-specific tests (rows appearing incrementally)
3. Add thread-safety tests for concurrent AddRow + filter

---

## Key Design Decisions

### 1. New cmdlet vs. replacing OCGV
**Decision:** New cmdlet (`Out-ConsoleTableView` / `octv`). Rationale:
- No breaking changes to existing OCGV users
- Can ship alongside OCGV for comparison
- Eventually OCGV could become an alias for OCTV once stable

### 2. Streaming architecture
**Decision:** Background thread for Terminal.Gui, pipeline thread calls `AddRow`.
- Terminal.Gui must own a thread (it blocks on `Run()`)
- PowerShell pipeline must not block in `ProcessRecord`
- `Application.Invoke()` is the official TG mechanism for cross-thread UI updates

### 3. Column discovery timing
**Decision:** Determine columns from the first PSObject, lock schema after that.
- Same as OCGV — uses `TypeGetter` which examines format definitions
- Heterogeneous pipelines: later objects with missing properties show empty cells
- Later objects with *extra* properties: those properties are ignored (OCGV parity)

### 4. Checkbox vs. highlight selection
**Decision:** Use `CheckBoxTableSourceWrapperByIndex` for explicit mark indicators.
- Clearer UX than highlight-only selection
- Users can see marks persist across scrolling and filtering
- Matches OCGV's `IsMarked` visual pattern

---

## Risk & Open Questions

1. **Thread safety of Terminal.Gui's `Table` property setter** — Need to verify that changing `tableView.Table` from `Application.Invoke` is safe while the view is rendering. Likely fine since `Invoke` runs on the UI thread.

2. **Column width instability during streaming** — As new rows arrive with wider values, columns may need to resize. Options: (a) fix widths after first batch, (b) allow periodic resize, (c) user-triggered resize. Start with (a).

3. **Memory for very large streams** — All rows are kept in memory (same as OCGV). For truly infinite streams, may need a circular buffer or virtualized source in the future. Out of scope for v1.

4. **`TypeGetter` thread safety** — `TypeGetter.CastObjectsToTableView` uses PowerShell runspace internals. Need to ensure it's called from the pipeline thread only, not from the UI thread.

5. **Module manifest** — Need to add the new cmdlet to the exported commands list.
