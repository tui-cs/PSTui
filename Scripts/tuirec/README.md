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
DLL. Four things shape every recipe here:

1. **Record a script file, not `-Command`.** `tuirec --args` is **comma-split**,
   and PowerShell pipelines are full of commas (`Select-Object a, b, c`). So the
   demos live in `*.ps1` files run with `-File`:
   `--args "-NoLogo,-NoProfile,-File,./Scripts/tuirec/demo-ocgv.ps1"`.

2. **The pwsh REPL does not render under a recording PTY** — only the
   Terminal.Gui app it launches does. You cannot record "type a command, press
   Enter, watch ocgv open" against the live prompt. Instead each demo script *is*
   the scenario and invokes the cmdlet directly.

3. **Show the prompt+command by echoing it from the script — before
   `Import-Module`.** To make a GIF read like a real session, the demo `Write-Host`s
   a synthetic prompt line (`PS ~/PStui> ls | ocgv`) and then sleeps briefly so it
   lingers before the picker opens. Printing it *before* `Import-Module` means the
   GIF opens on the command line instead of a ~1 s blank import lead-in.

4. **Use `-FullScreen` + `--trim=false` for multi-step demos.** `-FullScreen`
   gives each picker a clean alt-screen capture (inline rendering leaves residue
   when a filtered grid is short). But `--trim` (on by default) treats everything
   after the *first* alt-screen exit as postroll and drops it — fatal for a demo
   with several steps — so pass `--trim=false`.

The demo scripts:

| Script | Records |
|--------|---------|
| `demo-ocgv.ps1` | `ls \| ocgv` then the `killp` process picker → `hero.gif` |
| `demo-shot.ps1` | `Get-Process \| shot` tree exploration → `shot.gif` |
| `demo-f7.ps1`   | the `F7` command-history picker → `f7history.gif` |
| `../../demo.ps1` (repo root) | the full guided walkthrough → `demo.gif` |

`killp` is **safe** in recordings — the demos `Esc` out of the picker, so no
process is ever killed.

## Recipes

Run from the repo root. PSTui must be importable (`Install-Module PSTui`, or it
auto-loads from the built `./module`). `tuirec` writes to `artifacts/`
(git-ignored); copy the result into `docs/PSTui/`.

```bash
export PATH="$(go env GOPATH)/bin:$PATH"
```

### `hero.gif` — `ls | ocgv` + `killp`

```bash
ks='wait:600,`src`,wait:1900,Escape,wait:1900,`Finder`,wait:1900,Escape,wait:1200'
tuirec record --binary pwsh --args "-NoLogo,-NoProfile,-File,./Scripts/tuirec/demo-ocgv.ps1" \
  --trim=false --name hero --title "PSTui — ocgv" --keystrokes "$ks" \
  --startup-delay 1900 --drain 1000 --cols 100 --rows 26 --keystroke-delay 120
cp artifacts/hero.gif docs/PSTui/hero.gif
```

### `shot.gif` — `Get-Process | shot`

```bash
ks='wait:1500,CursorDown,CursorDown,CursorDown,wait:1200,CursorUp,CursorUp,CursorUp,wait:500,CursorRight,wait:1700,CursorDown,CursorDown,CursorDown,CursorDown,wait:1500,Escape,wait:900'
tuirec record --binary pwsh --args "-NoLogo,-NoProfile,-File,./Scripts/tuirec/demo-shot.ps1" \
  --trim=false --name shot --title "PSTui — Show-ObjectTree (shot)" --keystrokes "$ks" \
  --startup-delay 2100 --drain 1000 --cols 100 --rows 28
cp artifacts/shot.gif docs/PSTui/shot.gif
```

### `f7history.gif` — the F7 command-history picker

```bash
ks='wait:1500,`Get`,wait:1800,`-S`,wait:1800,Escape,wait:900'
tuirec record --binary pwsh --args "-NoLogo,-NoProfile,-File,./Scripts/tuirec/demo-f7.ps1" \
  --trim=false --name f7history --title "PSTui — F7 history" --keystrokes "$ks" \
  --startup-delay 2100 --drain 1200 --cols 100 --rows 22 --keystroke-delay 130
cp artifacts/f7history.gif docs/PSTui/f7history.gif
```

### `demo.gif` — the full `demo.ps1` walkthrough

Records the repo-root [`demo.ps1`](../../demo.ps1) end-to-end; `Esc` advances
through each of its examples (the `killp` steps select nothing, so no process is
killed).

```bash
ks='wait:800,Escape,wait:2300,Escape,wait:2300,Escape,wait:2300,Escape,wait:2300,Escape,wait:2300,CursorRight,wait:1600,Escape,wait:1000'
tuirec record --binary pwsh --args "-NoLogo,-NoProfile,-File,./demo.ps1" \
  --name demo --title "PSTui — demo.ps1" --keystrokes "$ks" \
  --startup-delay 3200 --drain 1200 --cols 100 --rows 28
cp artifacts/demo.gif docs/PSTui/demo.gif
```

## Validate

```bash
# No errors leaked into the cast:
grep -iE "error|not recognized|exception" artifacts/<name>.cast

# Eyeball a frame (needs python3 + Pillow). Frame 1 is usually the prompt:
python3 -c "from PIL import Image; im=Image.open('artifacts/<name>.gif'); print(im.n_frames,'frames'); im.seek(im.n_frames//2); im.convert('RGB').save('/tmp/mid.png')"
```

A good recording is several frames and **> ~20 KB**; a near-blank capture is a
few KB / 2–3 frames (keystrokes went to the wrong focus, or no output rendered).

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| "recording has no output events" / blank | Recording the bare pwsh REPL (doesn't render in the PTY) | Record a demo `.ps1` that invokes the cmdlet directly |
| Multi-step demo cut off after step 1 | `--trim` drops everything after the first alt-screen exit | `--trim=false` |
| Picker leaves residue / overlaps | Inline render of a short (filtered) grid | Use `-FullScreen` |
| Keystrokes seem ignored | Focus is on the tree/table, not the filter | `ocgv`: add `-Focus Filter`. `shot`: starts focused; `CursorRight` expands |
| `shot` renders mid-screen / empty | Inline tree positioning | Use `-FullScreen` |
| `--args` parsed into too many args | Commas in the PowerShell got split | Move the pipeline into a `-File` script |
| Backtick literal text dropped | Shell ate the backticks | Single-quote the whole `ks='...'` |
| ~1 s blank lead-in | `Import-Module` runs before any output | Echo the prompt line *before* `Import-Module` in the demo script |
