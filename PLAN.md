# PSTui — Rebranding & Re-release Plan

Re-releasing the PowerShell Console GUI tools — **`Out-ConsoleGridView` (`ocgv`)**
and **`Show-ObjectTree` (`shot`)** — under the **gui-cs** organization as
**PSTui** (*PowerShell TUI tools*), following Microsoft's decision to archive
`Microsoft.PowerShell.ConsoleGuiTools`.

## 1. Background

- **[ConsoleGuiTools#275](https://github.com/PowerShell/ConsoleGuiTools/issues/275)** —
  Microsoft declared `Microsoft.PowerShell.ConsoleGuiTools` feature-complete at
  **0.7.7** (its final release) and will **archive the repo**. The PowerShell
  team committed to **pointing users at a community fork** if @tig stands one up.
- **[ConsoleGuiTools#267](https://github.com/PowerShell/ConsoleGuiTools/pull/267)**
  ("Updates OCGV and SHOT to Terminal.Gui v2") — the modernization was *already
  done and approved by @andyleejordan*, but never merged because MS sunset the
  project. **This work is now landed on this branch** (originally
  `tig/GraphicalTools:terminal_gui_v2`, mirrored as `gui-cs/pstui:tig-terminal_gui_v2`).
  It includes:
  - Terminal.Gui **v1 (1.17.1) → v2 (2.1.0)** rewrite of both cmdlets.
  - **.NET 8 → .NET 10**; centralized package management (`Directory.Packages.props`).
  - `ocgv` rebuilt on v2 `TableView` (streaming pipeline, regex filter, native
    sortable headers); new params `-Driver`, `-FullScreen`, `-Search`, `-Focus`,
    `-AllProperties`; removed `-UseNetDriver`. Default render is now **inline**
    (use `-FullScreen` for the old alt-buffer behavior).
  - `shot` rebuilt on v2 `TreeView` with `RegexTreeViewTextFilter`.
  - The old `Out-ConsoleTableView` (octv) was **folded into `ocgv`**.
  - **xUnit test suite**; removed dead/dangerous `Serializers.cs`
    (`TypeNameHandling.All`); bumped to **1.0.0**.

**Implication:** This is *not* a "rebrand on v1 now, migrate to v2 later" effort.
The v2 work exists and is review-clean. The job is to **rebrand on top of it.**

## 2. Ecosystem alignment (gui-cs)

- **Terminal.Gui** is at **v2** — instance-based `IRunnable` app model, no more
  `Nstack`/`ustring` (uses `Rune`/`string`).
- **clet** — *"One binary. Every prompt. JSON out. Go home."* Verb-noun
  commands, consistent JSON envelopes (`{"schemaVersion":1,"status":"ok",...}`),
  Unix exit codes (0/1/2/130), NativeAOT, **for humans *and* AI agents**.
- **cli** — the gui-cs library for scriptable commands with agent
  discoverability (`--opencli` manifest).

PSTui should feel like a first-class member of this family.

## 3. Decisions

| Topic | Decision |
|-------|----------|
| Project / module / PSGallery id | **`PSTui`** (Microsoft retains the `Microsoft.PowerShell.ConsoleGuiTools` gallery id; we need a fresh one regardless) |
| Brand / tagline | *"PowerShell TUI tools, built on Terminal.Gui."* Retire **"Console GUI"** terminology from prose. |
| Code baseline | **PR #267** (Terminal.Gui v2, tests, 1.0.0) — already landed on this branch |
| Cmdlet names | **No renames.** Keep `Out-ConsoleGridView`/`ocgv` and `Show-ObjectTree`/`shot` exactly as in #267 — protects existing users' muscle memory and scripts |
| Licensing | MIT retained; rewrite `NOTICE.txt`, copyright → gui-cs / Tig Kindel |

> The cmdlet *names* (`ocgv`, `shot`) are the stable contract for existing users
> and stay put. "Removing old-school terminology" applies to **branding and
> prose** ("Console GUI" → "TUI"), the **module name**, and **namespaces** —
> not to the cmdlets.

## 4. Workstreams

### A. v2 foundation — DONE
- PR #267 landed on `claude/powershell-tui-rebranding-543gas`
  (Terminal.Gui 2.1.0, new window classes, test suite, module 1.0.0).
- [ ] Confirm the test suite builds & passes in CI (needs .NET 10 SDK).

### B. De-Microsoft rebrand
- Rename module `Microsoft.PowerShell.ConsoleGuiTools` → **`PSTui`**:
  `.psd1` (new **GUID**, `Author`/`CompanyName`/`Copyright`, `ProjectUri` →
  `gui-cs/pstui`, `LicenseUri`; tags drop `gui.cs`, add `TUI`/`Terminal.Gui`).
  `CmdletsToExport`/`AliasesToExport` stay `Out-ConsoleGridView`/`ocgv` and
  `Show-ObjectTree`/`shot`.
- Rename namespaces/assemblies/projects:
  `Microsoft.PowerShell.ConsoleGuiTools` → `PSTui`,
  `Microsoft.PowerShell.OutGridView.Models` → `PSTui.Models`
  (sheds the deprecated WinForms `Out-GridView` association).
- `*.props`: `Company`, `Copyright`, `RepositoryUrl`, `PackageLicenseUrl`.
- Rewrite **README** around "PowerShell TUI tools"; add a prominent
  **"Migrating from Microsoft.PowerShell.ConsoleGuiTools"** section.
- Replace residual "Console GUI" terminology in docs with "TUI".

### C. Packaging, CI & release
- New PSGallery package **`PSTui`** (decouple from the Microsoft OneBranch
  pipeline; gui-cs GitHub Actions release flow).
- Decide signing/publishing path (PSGallery API key under gui-cs).
- First release **v1.0.0** (matches #267; signals the modern v2 base).

### D. Transition & comms
- Coordinate with @andyleejordan so the archived ConsoleGuiTools repo's README
  points at `gui-cs/pstui`.
- Add PSTui to the gui-cs org README ecosystem list.
- Migration note: `Install-Module PSTui`; `ocgv` and `shot` are unchanged.

### E. Future (post-1.0, north star)
- Expose grid/tree pickers through the **cli** agent-discoverability layer so
  `ocgv`/`shot` gain clet-style JSON-out + clean exit-code modes usable by AI
  agents — same envelope contract as clet.

## 5. Open items to confirm

- **Target framework:** #267 targets **.NET 10**. A binary module loads
  in-process, so its TFM must match the PowerShell host runtime (PS 7.4 = .NET 8;
  .NET 10 needs PS 7.6+). Decide: keep `net10.0`, multi-target `net8.0;net10.0`,
  or pin `net8.0` (LTS) for the widest install base.
- **JSON stack:** confirm whether the v2 code still references `Newtonsoft.Json`;
  if so, migrate to `System.Text.Json` during workstream B.

## 6. Sequenced checklist

1. [x] Land PR #267 (v2 foundation) on the rebrand branch.
2. [ ] Confirm tests build & pass (.NET 10).
3. [ ] De-Microsoft rebrand → module/namespaces/props/README (`PSTui`);
       keep `ocgv` and `shot` names unchanged.
4. [ ] gui-cs CI/release pipeline; resolve TFM + JSON-stack open items.
5. [ ] Publish **PSTui 1.0.0** to PSGallery.
6. [ ] Coordinate archive pointer with @andyleejordan; update gui-cs org README.
7. [ ] (Post-1.0) clet/cli agent-discoverability alignment.
