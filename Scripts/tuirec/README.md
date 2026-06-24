# Recording PSTui GIFs with `tuirec`

Use this guide to (re)generate the animated GIFs in the README and docs. The
recorder is [`tui-cs/tuirec`](https://github.com/tui-cs/tuirec) — a Go CLI that
spawns a process in a PTY, injects keystrokes, records an asciinema cast, and
renders a GIF via `agg`. This is the PSTui-specific companion to
[Terminal.Gui's `Scripts/tuirec/README.md`](https://github.com/tui-cs/Terminal.Gui/blob/main/Scripts/tuirec/README.md);
read that for the full keystroke-token reference.

## Install

```bash
# Requires Go 1.22+. agg is auto-downloaded on first use.
go install github.com/tui-cs/tuirec/cmd/tuirec@latest
export PATH="$(go env GOPATH)/bin:$PATH"   # tuirec installs to GOPATH/bin
tuirec --version
```

## How PSTui differs from recording a Terminal.Gui app

PSTui cmdlets are driven from a **PowerShell pipeline**, not a `ScenarioRunner`
DLL. Two consequences shape every recipe here:

1. **Record a script file, not `-Command`.** `tuirec --args` is **comma-split**,
   and PowerShell pipelines are full of commas (`Select-Object a, b, c`). So the
   demos live in small `*.ps1` files in this folder and are run with `-File`:

   ```bash
   --binary pwsh --args "-NoLogo,-NoProfile,-File,./Scripts/tuirec/demo-ocgv.ps1"
   ```

2. **The pwsh REPL does not render under a recording PTY** — only the
   Terminal.Gui app it launches does. So you cannot record "type a command, press
   Enter, watch ocgv open." Instead each demo script *is* the scenario: it imports
   PSTui and invokes the cmdlet(s) directly, and `tuirec` drives the resulting
   TUI. (This is why the F7 demo records the history picker directly — see below.)

The demo scripts:

| Script | Records |
|--------|---------|
| `demo-ocgv.ps1` | `ls \| ocgv` then the `killp` process picker → `hero.gif` |
| `demo-shot.ps1` | `Get-Process \| shot` tree exploration → `shot.gif` |
| `demo-f7.ps1`   | the `F7` command-history picker → `f7history.gif` |

## Composing keystrokes

Same token syntax as Terminal.Gui (`wait:<ms>`, `CursorDown`, `Enter`, `Esc`,
`Tab`, backtick-quoted `` `literal text` ``). PSTui notes:

- **Type into the filter with `-Focus Filter`.** `ocgv` (and the F7 picker) take
  a `-Focus Filter` parameter so keystrokes land in the filter box and rows
  narrow *live* — the best thing to show. Then `Esc` (or `Enter` to select).
- **`shot` has no `-Focus`; use `-FullScreen`.** The tree's inline render is
  finicky to position; `-FullScreen` gives a clean top-anchored capture. The
  first root is focused on open — `CursorRight` expands, `CursorDown` navigates.
- **Quote the keystrokes in single quotes** (bash *and* PowerShell) so backtick
  literals survive: `ks='wait:1500,`src`,wait:2000,Escape'`.
- **`killp` is safe in recordings** — `demo-ocgv.ps1` `Esc`s out of the picker,
  so no process is ever killed.

## Recipes

Run from the repo root. PSTui must be importable (`Install-Module PSTui`, or it
auto-resolves from the built `./module`).

### `hero.gif` — `ls | ocgv` + `killp`

```bash
export PATH="$(go env GOPATH)/bin:$PATH"
ks='wait:1600,`src`,wait:2200,Escape,wait:2200,`Finder`,wait:2200,Escape,wait:1500'
tuirec record --binary pwsh \
  --args "-NoLogo,-NoProfile,-File,./Scripts/tuirec/demo-ocgv.ps1" \
  --name hero --title "PSTui — ocgv" \
  --keystrokes "$ks" \
  --startup-delay 3200 --drain 1500 --cols 100 --rows 28 --keystroke-delay 140
cp artifacts/hero.gif docs/PSTui/hero.gif
```

### `shot.gif` — `Get-Process | shot`

```bash
ks='wait:1800,CursorDown,CursorDown,CursorDown,CursorDown,CursorDown,wait:1500,CursorUp,CursorUp,CursorUp,CursorUp,CursorUp,wait:600,CursorRight,wait:1800,CursorDown,CursorDown,CursorDown,CursorDown,wait:1500,Escape,wait:1000'
tuirec record --binary pwsh \
  --args "-NoLogo,-NoProfile,-File,./Scripts/tuirec/demo-shot.ps1" \
  --name shot --title "PSTui — Show-ObjectTree (shot)" \
  --keystrokes "$ks" \
  --startup-delay 3200 --drain 1200 --cols 100 --rows 28
cp artifacts/shot.gif docs/PSTui/shot.gif
```

### `f7history.gif` — the F7 command-history picker

```bash
ks='wait:1700,`Get`,wait:2200,`-S`,wait:2000,Escape,wait:1200'
tuirec record --binary pwsh \
  --args "-NoLogo,-NoProfile,-File,./Scripts/tuirec/demo-f7.ps1" \
  --name f7history --title "PSTui — F7 command history" \
  --keystrokes "$ks" \
  --startup-delay 3200 --drain 1500 --cols 100 --rows 22 --keystroke-delay 130
cp artifacts/f7history.gif docs/PSTui/f7history.gif
```

## Validate

`tuirec` writes to `artifacts/` (git-ignored); commit the copies under
`docs/PSTui/`.

```bash
# No errors leaked into the cast:
grep -iE "error|not recognized|exception" artifacts/<name>.cast

# Not blank — eyeball a middle frame (needs python3 + Pillow):
python3 -c "from PIL import Image; im=Image.open('artifacts/<name>.gif'); print(im.n_frames,'frames'); im.seek(im.n_frames//2); im.convert('RGB').save('/tmp/mid.png')"
```

A good recording is **> ~20 KB** with several frames; a near-blank capture is a
few KB / 2–3 frames (usually means keystrokes went to the wrong focus, or the
target produced no output).

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| "recording has no output events" / blank | Recording the bare pwsh REPL (it doesn't render in the PTY) | Record a demo `.ps1` that invokes the cmdlet directly |
| Keystrokes seem ignored | Focus is on the tree/table, not the filter | `ocgv`: add `-Focus Filter`. `shot`: `Tab` to the tree, or it starts focused |
| `shot` renders mid-screen / empty | Inline tree positioning | Use `-FullScreen` |
| `--args` parsed into too many args | Commas in the PowerShell got split | Move the pipeline into a `-File` script (see above) |
| Backtick literal text dropped | Shell ate the backticks | Single-quote the whole `ks='...'` |
