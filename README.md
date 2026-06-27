# PSTui — PowerShell TUI tools

[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/PSTui?label=PowerShell%20Gallery&color=blue)](https://www.powershellgallery.com/packages/PSTui)
[![Downloads](https://img.shields.io/powershellgallery/dt/PSTui?color=blue)](https://www.powershellgallery.com/packages/PSTui)
[![CI](https://github.com/tui-cs/PSTui/actions/workflows/ci-test.yml/badge.svg)](https://github.com/tui-cs/PSTui/actions/workflows/ci-test.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)

> **PowerShell TUI tools, built on [Terminal.Gui](https://github.com/tui-cs/Terminal.Gui).**

PSTui adds interactive terminal-UI cmdlets to the PowerShell pipeline:

- **`Out-ConsoleGridView`** (`ocgv`) — pipe objects into an interactive,
  filterable, sortable table and send the rows you select back down the pipeline.
- **`Show-ObjectTree`** (`shot`) — explore objects in an interactive tree view.

```powershell
Install-Module PSTui
Import-Module PSTui
```

PSTui is the community continuation of Microsoft's now-archived
[`Microsoft.PowerShell.ConsoleGuiTools`](https://github.com/PowerShell/ConsoleGuiTools/issues/275),
rebuilt on **Terminal.Gui v2** and **.NET 10**. The cmdlet and alias names are
unchanged, so existing scripts and muscle memory carry forward — see
[Migrating](#migrating-from-microsoftpowershellconsoleguitools) below.

It's part of the [tui-cs](https://github.com/tui-cs) family, alongside
[Terminal.Gui](https://github.com/tui-cs/Terminal.Gui),
[clet](https://github.com/tui-cs/clet), and [cli](https://github.com/tui-cs/cli).

![ls | ocgv and killp](docs/PSTui/hero.gif)

> `ls | ocgv` then a `killp` process picker — interactive, filterable tables
> straight from the pipeline.

## Installation

Requires **PowerShell 7.6+** (the binary module targets .NET 10). Works on
Windows, macOS, and Linux.

```powershell
Install-Module PSTui
Import-Module PSTui
```

`Out-ConsoleGridView`/`ocgv` and `Show-ObjectTree`/`shot` auto-load on first use,
but the [`F7` command-history bindings](#command-history-f7--shiftf7) only
register on import — add `Import-Module PSTui` to your `$PROFILE` to get them in
every session.

## Migrating from `Microsoft.PowerShell.ConsoleGuiTools`

PSTui is a drop-in continuation of Microsoft's (now archived)
`Microsoft.PowerShell.ConsoleGuiTools`. The cmdlets and aliases are unchanged,
so existing scripts keep working — you only change the module you install:

```powershell
# Old (Microsoft — archived, final release 0.7.7)
Install-Module Microsoft.PowerShell.ConsoleGuiTools

# New (tui-cs community continuation)
Install-Module PSTui
```

|              | `Microsoft.PowerShell.ConsoleGuiTools` | `PSTui`                          |
| ------------ | -------------------------------------- | -------------------------------- |
| Cmdlets      | `Out-ConsoleGridView`, `Show-ObjectTree` | **same**                       |
| Aliases      | `ocgv`, `shot`                         | **same**                         |
| Engine       | Terminal.Gui v1                        | Terminal.Gui v2                  |
| PowerShell   | 7.2+                                   | 7.6+                             |
| Maintainer   | Microsoft (archived)                   | [tui-cs](https://github.com/tui-cs) community |

Because both modules export the **same** cmdlet names, avoid installing both at
once — `Import-Module` will report ambiguous commands. If you have the old
module, remove it first:

```powershell
Uninstall-Module Microsoft.PowerShell.ConsoleGuiTools
Install-Module PSTui
```

### Behavior changes (from the Terminal.Gui v2 rewrite, [ConsoleGuiTools#267](https://github.com/PowerShell/ConsoleGuiTools/pull/267))

* `Out-ConsoleGridView` renders **inline** by default; use `-FullScreen` for the
  old alternate-buffer behavior.
* Objects **stream** into the table as they arrive from the pipeline.
* Pressing <kbd>Enter</kbd> with no explicit selection returns the **focused** row.
* New parameters: `-Driver`, `-FullScreen`, `-Search`, `-Focus`.
* Removed: `-UseNetDriver` (replaced by `-Driver`).

## Features

* [`Out-ConsoleGridView`](docs/PSTui/Out-ConsoleGridView.md) - Send objects to an interactive table view with column headers, horizontal scrolling, streaming, sorting, and native multi-selection.
* [`Show-ObjectTree`](docs/PSTui/Show-ObjectTree.md) - Send objects to a tree view window for interactive exploration and filtering.
* [Graphical command history](#command-history-f7--shiftf7) - `F7`/`Shift+F7` browse and re-run command history (the [F7History](https://github.com/tui-cs/F7History) module, built in).

* Cross-platform - Works on any platform that supports PowerShell 7.6+.
* Interactive - Use the mouse and keyboard to interact with the grid or tree view.
* Filtering - Filter the data using the built-in filter box.
* Sorting - Sort the data by clicking on the column headers.
* Multiple Selection - Select multiple items and send them down the pipeline.
* Customizable - Customize the grid view window with the built-in parameters.

**`Show-ObjectTree` (`shot`)** — explore any object graph as an interactive tree:

![Get-Process | shot](docs/PSTui/shot.gif)

## Examples

Run [`demo.ps1`](demo.ps1) for a guided walkthrough of the examples below:

![demo.ps1 walkthrough](docs/PSTui/demo.gif)

### Example 1: Output processes to a grid view

```PowerShell
Get-Process | Out-ConsoleGridView
```

This command gets the processes running on the local computer and sends them to a grid view window.

### Example 2: Use a variable to output processes to a grid view

```PowerShell
$P = Get-Process
$P | Out-ConsoleGridView -OutputMode Single
```

This command also gets the processes running on the local computer and sends them to a grid view window.

The first command uses the Get-Process cmdlet to get the processes on the computer and then saves the process objects in the $P variable.

The second command uses a pipeline operator to send the $P variable to **Out-ConsoleGridView**.

By specifying `-OutputMode Single` the grid view window will be restricted to a single selection, ensuring no more than a single object is returned.

### Example 3: Display a formatted table in a grid view

```PowerShell
Get-Process | Select-Object -Property Name, WorkingSet, PeakWorkingSet | Sort-Object -Property WorkingSet -Descending | Out-ConsoleGridView
```

This command displays a formatted table in a grid view window.

It uses the Get-Process cmdlet to get the processes on the computer.

Then, it uses a pipeline operator (|) to send the process objects to the Select-Object cmdlet.
The command uses the **Property** parameter of **Select-Object** to select the Name, WorkingSet, and PeakWorkingSet properties to be displayed in the table.

Another pipeline operator sends the filtered objects to the Sort-Object cmdlet, which sorts them in descending order by the value of the **WorkingSet** property.

The final part of the command uses a pipeline operator (|) to send the formatted table to **Out-ConsoleGridView**.

You can now use the features of the grid view to search, sort, and filter the data.

### Example 4: Save output to a variable, and then output a grid view

```PowerShell
($A = Get-ChildItem -Path $pshome -Recurse) | Out-ConsoleGridView
```

This command saves its output in a variable and sends it to **Out-ConsoleGridView**.

The command uses the Get-ChildItem cmdlet to get the files in the Windows PowerShell installation directory and its subdirectories.
The path to the installation directory is saved in the $pshome automatic variable.

The command uses the assignment operator (=) to save the output in the $A variable and the pipeline operator (|) to send the output to **Out-ConsoleGridView**.

The parentheses in the command establish the order of operations.
As a result, the output from the Get-ChildItem command is saved in the $A variable before it is sent to **Out-ConsoleGridView**.

### Example 5: Output processes for a specified computer to a grid view

```PowerShell
Get-Process -ComputerName "Server01" | ocgv -Title "Processes - Server01"
```

This command displays the processes that are running on the Server01 computer in a grid view window.

The command uses `ocgv`, which is the built-in alias for the **Out-ConsoleGridView** cmdlet, it uses the _Title_ parameter to specify the window title.

### Example 6: Define a function to kill processes using a graphical chooser

```PowerShell
function killp { Get-Process | Out-ConsoleGridView -OutputMode Single -Filter $args[0] | Stop-Process -Id {$_.Id} }
killp note
```

This example shows defining a function named `killp` that shows a grid view of all running processes and allows the user to select one to kill it.

The example uses the `-Filter` parameter to filter for all processes with a name that includes `note` (thus highlighting `Notepad` if it were running). Selecting an item in the grid view and pressing `ENTER` will kill that process.

### Example 7: Output processes to a tree view

```PowerShell
Get-Process | Show-ObjectTree
```

This command gets the processes running on the local computer and sends them to a tree view window.

Use right arrow when a row has a `+` symbol to expand the tree. Left arrow will collapse the tree.

### Example 8: Stream a long-running pipeline into the grid

```PowerShell
Get-ChildItem -Path / -Recurse -ErrorAction Ignore | Out-ConsoleGridView
```

The table appears as soon as the first object arrives and rows stream in as the
pipeline executes, so you can start filtering and scrolling immediately —
useful for slow or large pipelines like a recursive file enumeration.

### Example 9: Search for a specific row in the grid view

```PowerShell
Get-Service | ocgv -Search "wuauserv" -Focus Filter
```

This command displays all services in a grid view, positions the cursor on the first row matching "wuauserv", and starts with focus in the filter field.

## Command history (`F7` / `Shift+F7`)

PSTui includes a graphical command-history picker — the
[F7History](https://github.com/tui-cs/F7History) module, **folded in and enabled
by default** — no separate package to install.

![F7 command history](docs/PSTui/f7history.gif)

> **The key bindings register when PSTui is imported.** `Install-Module` alone
> does *not* bind `F7`; PowerShell only loads the module (and its key handlers)
> on `Import-Module`. To have `F7`/`Shift+F7` available in **every** session, add
> this to your PowerShell profile (`notepad $PROFILE` / `code $PROFILE`):
>
> ```powershell
> Import-Module PSTui
> ```

| Key | Shows |
| --- | --- |
| <kbd>F7</kbd> | history for the **current** PowerShell session (`Get-History`) |
| <kbd>Shift</kbd>+<kbd>F7</kbd> | history across **all** sessions (PSReadLine), de-duplicated |

The history opens in `Out-ConsoleGridView`:

- Whatever you'd already typed at the prompt is used as the initial **filter**.
- Selecting an entry and pressing <kbd>Enter</kbd> **inserts it** at the prompt
  (press <kbd>Esc</kbd> to cancel and keep what you had).

It activates automatically when [PSReadLine](https://github.com/PowerShell/PSReadLine)
is available (the default interactive console) and is a no-op otherwise — so
importing PSTui in a script or non-interactive host won't fail.

The picker is also exposed as the **`Show-PSTuiHistory`** command (add `-Global`
for all-sessions history), so you can call it directly or bind it to a different
key with `Set-PSReadLineKeyHandler`.

### Opting out

The module exports two functions to toggle the bindings at runtime:

```powershell
Disable-PSTuiHistoryKeyHandler   # remove the F7 / Shift+F7 bindings
Enable-PSTuiHistoryKeyHandler    # re-add them
```

To disable it permanently, do **either** of the following *before* PSTui is
imported (e.g. in your `$PROFILE`):

```powershell
$PSTuiDisableHistoryKeyHandler = $true        # PowerShell variable
$env:PSTUI_DISABLE_HISTORY_KEYS = 1           # ...or an environment variable
```

Run `Get-Help Enable-PSTuiHistoryKeyHandler` for details.

## Development

### 1. Install PowerShell 7.6+

Install PowerShell 7.6+ with [these instructions](https://github.com/PowerShell/PowerShell#get-powershell).

### 2. Clone the GitHub repository

```powershell
git clone https://github.com/tui-cs/PSTui.git
```

### 3. Install [Invoke-Build](https://github.com/nightroman/Invoke-Build)

```powershell
Install-Module InvokeBuild -Scope CurrentUser
```

Now you're ready to build the code.  You can do so in one of two ways:

### 4. Building the code from PowerShell

```powershell
pushd ./PSTui
Invoke-Build Build
popd
```

From there you can import the module that you just built for example (start a fresh `pwsh` instance first so you can unload the module with an `exit`; otherwise building again may fail because the `.dll` will be held open):

```powershell
pwsh
Import-Module ./module/PSTui
```

And then run the cmdlet you want to test, for example:

```powershell
Get-Process | Out-ConsoleGridView
exit
```

> NOTE: If you change the code and rebuild the project, you'll need to launch a
> _new_ PowerShell process since the DLL is already loaded and can't be unloaded.

### 5. Debugging in Visual Studio Code

```powershell
code ./PSTui
```

Build by hitting `Ctrl-Shift-B` in VS Code.

Set a breakpoint and hit `F5` to start the debugger.

Click on the VS Code "TERMINAL" tab and type your command that starts `Out-ConsoleGridView`, e.g.

```powershell
ls | ocgv
```

Your breakpoint should be hit.

## Contributions Welcome

We would love to incorporate community contributions into this project.  If
you would like to contribute code, documentation, tests, or bug reports,
please read the [development section above](https://github.com/tui-cs/PSTui#development)
to learn more.

## PSTui Architecture

`PSTui` consists of 2 .NET Projects:

* PSTui - Cmdlet implementation for Out-ConsoleGridView and Show-ObjectTree
* PSTui.Models - Contains data contracts between the TUI & Cmdlet

## Credits

Originally authored by [Tyler Leonhardt](http://twitter.com/tylerleonhardt).
Carried forward and maintained by [Tig Kindel](https://www.kindel.com)
([@tig](https://github.com/tig)) under the [tui-cs](https://github.com/tui-cs)
organization.

## License

This project is [licensed under the MIT License](LICENSE.txt).

## Code of Conduct

Please see our [Code of Conduct](.github/CODE_OF_CONDUCT.md) before participating in this project.

## Security Policy

For any security issues, please see our [Security Policy](.github/SECURITY.md).
