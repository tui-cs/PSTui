---
external help file: PSTui.dll-Help.xml
keywords: powershell,cmdlet
locale: en-us
Module Name: PSTui
ms.date: 05/04/2026
schema: 2.0.0
title: Out-ConsoleGridView
---

# Out-ConsoleGridView

## SYNOPSIS

Sends output to an interactive table view with column headers, horizontal scrolling, sorting, and streaming support.

## SYNTAX

```PowerShell
 Out-ConsoleGridView [-InputObject <psobject>] [-Title <string>] [-OutputMode {None | Single |
    Multiple}] [-Filter <string>] [-Search <string>] [-Focus {Table | Filter}] [-MinUI]
    [-FullScreen] [-Driver <string>] [<CommonParameters>]
```

## DESCRIPTION

The **Out-ConsoleGridView** cmdlet sends the output from a command to a grid view window where the output is displayed in an interactive table with column headers, column sizing, horizontal scrolling, and column sorting.

Use the Filter box at the top of the window to search the text in the table using regular expressions. Unlike the Filter, the `-Search` parameter positions the cursor on the first matching row without hiding non-matching rows.

Objects are streamed into the table as they arrive from the pipeline — the UI appears immediately and rows are added incrementally. A spinner in the status bar indicates loading is in progress.

To send items from the interactive window down the pipeline, select rows (use arrow keys and `SPACE` or click with the mouse) and then press `ENTER`. Press `ESC` to cancel without output. Use `Ctrl+A` to select all rows, or `Ctrl+D` to deselect all. Click column headers to sort.

## EXAMPLES

### Example 1: Output processes to a grid view

```PowerShell
Get-Process | Out-ConsoleGridView
```

This command gets the processes running on the local computer and sends them to a grid view window with column headers for each property. The table appears as soon as the first object arrives — rows stream in as the pipeline executes.

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

The command uses `ocgv`, which is the built-in alias for the **Out-ConsoleGridView** cmdlet, it uses the *Title* parameter to specify the window title.

### Example 6: Define a function to kill processes using a graphical chooser

```PowerShell
function killp { Get-Process | Out-ConsoleGridView -OutputMode Single -Filter $args[0] | Stop-Process -Id {$_.Id} }
killp note
```
This example shows defining a function named `killp` that shows a grid view of all running processes and allows the user to select one to kill it.

The example uses the `-Filter` parameter to filter for all processes with a name that includes `note` (thus highlighting `Notepad` if it were running). Selecting an item in the grid view and pressing `ENTER` will kill that process.

### Example 7: Pass multiple items through Out-ConsoleGridView

```PowerShell
Get-Process | Out-ConsoleGridView -OutputMode Multiple | Export-Csv -Path .\ProcessLog.csv
```

This command lets you select multiple processes from the **Out-ConsoleGridView** window.
The processes that you select are passed to the **Export-Csv** command and written to the ProcessLog.csv file.

By default, `-OutputMode` is `Multiple`, which lets you send multiple items down the pipeline.

### Example 8: Browse command history with F7 / Shift+F7

PSTui binds `F7` and `Shift+F7` to a graphical command-history picker
automatically when the module is imported (the [F7History](https://github.com/tui-cs/F7History)
module, folded in). No setup beyond `Import-Module PSTui` is required — add that
line to your `$PROFILE` to get the bindings in every session.

Press `F7` to see the history for the current PowerShell session, or `Shift+F7`
for history across all sessions (PSReadLine). Whatever you'd already typed at the
prompt is used as the initial filter, and the entry you select is inserted at the
prompt.

The picker is also exposed as the `Show-PSTuiHistory` command (add `-Global` for
all-sessions history), so you can call it directly or bind it to a different key.

### Example 9: Search for a row without filtering

```PowerShell
Get-Service | ocgv -Search "wuauserv"
```

This command displays all services but positions the cursor on the first row matching "wuauserv". Unlike `-Filter`, all rows remain visible.

### Example 10: Start with focus on the filter field

```PowerShell
Get-ChildItem | ocgv -Focus Filter
```

This command opens the grid view with the cursor in the filter text field, ready to type a filter immediately. Pressing `ENTER` while in the filter field accepts the currently selected item(s).

### Example 11: Full screen mode with a custom title

```PowerShell
Get-Process | ocgv -FullScreen -Title "Process Monitor"
```

This command runs the grid view in full-screen mode using the alternate screen buffer, with a custom window title.

### Example 12: Minimal UI for scripting

```PowerShell
Get-Process | ocgv -MinUI -OutputMode Single
```

This command shows the grid view with no window frame, filter box, or status bar — just the table. Useful for quick selection in scripts.

## PARAMETERS

### -Filter
Pre-populates the Filter edit box, hiding rows that do not match the regular expression pattern.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Search
Positions the cursor on the first row matching this regular expression pattern. Unlike `-Filter`, non-matching rows remain visible.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Focus
Specifies which UI element receives initial focus.

- **Table** (default): The table view receives focus. Use arrow keys to navigate immediately.
- **Filter**: The filter text field receives focus. Start typing to filter. Press `ENTER` to accept the selected item(s).

```yaml
Type: FocusTarget
Parameter Sets: (All)
Aliases:
Accepted values: Table, Filter

Required: False
Position: Named
Default value: Table
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
Specifies that the cmdlet accepts input for **Out-ConsoleGridView**.

When you use the **InputObject** parameter to send a collection of objects to **Out-ConsoleGridView**, **Out-ConsoleGridView** treats the collection as one collection object, and it displays one row that represents the collection.

To display each object in the collection, use a pipeline operator (|) to send objects to **Out-ConsoleGridView**.

```yaml
Type: PSObject
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -OutputMode
Specifies the items that the interactive window sends down the pipeline as input to other commands.
By default, this cmdlet generates zero, one, or many items.

To send items from the interactive window down the pipeline, select items and press `ENTER`. `ESC` cancels.

The values of this parameter determine how many items you can send down the pipeline.

- None. No items.
- Single. Zero items or one item. Use this value when the next command can take only one input object.
- Multiple. Zero, one, or many items. Use this value when the next command can take multiple input objects. This is the default value.

```yaml
Type: OutputModeOption
Parameter Sets: OutputMode
Aliases:
Accepted values: None, Single, Multiple

Required: False
Position: Named
Default value: Multiple
Accept pipeline input: False
Accept wildcard characters: False
```

### -Title
Specifies the text that appears in the title bar of the **Out-ConsoleGridView** window.

By default, the title bar displays the command that invokes **Out-ConsoleGridView**.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MinUI
If specified, no window frame, filter box, or status bar will be displayed. The table is shown without chrome.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Driver
Sets the Terminal.Gui driver to use. Valid values are `ansi`, `windows`, or `unix`.

```yaml
Type: String
Parameter Sets: (All)
Aliases: ForceDriver

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FullScreen
If specified, the application runs in full-screen mode using the alternate screen buffer. By default, the application renders inline in the current terminal.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.Management.Automation.PSObject

You can send any object to this cmdlet. Objects are streamed — the UI appears as soon as the first object arrives.

## OUTPUTS

### System.Object

By default **Out-ConsoleGridView** returns objects representing the selected rows to the pipeline. Use `-OutputMode` to change this behavior.

## NOTES

* **Out-ConsoleGridView** uses Terminal.Gui's `TableView` control which provides column headers, column sizing, horizontal scrolling, column sorting, and native multi-row selection.

* The alias for **Out-ConsoleGridView** is `ocgv`.

* Objects are streamed into the table as they arrive from the pipeline. The UI appears immediately on the first object and rows are added incrementally. A spinner in the status bar indicates loading is in progress.

* The command output that you send to **Out-ConsoleGridView** should not be formatted, such as by using the Format-Table or Format-Wide cmdlets. To select properties, use the Select-Object cmdlet.

* Keyboard shortcuts:
  - `ENTER` — Accept selection and close
  - `ESC` — Cancel and close
  - `Ctrl+A` — Select all rows (when OutputMode is Multiple)
  - `Ctrl+D` — Deselect all rows (when OutputMode is Multiple)
  - `Home`/`End` — Jump to first/last row
  - Arrow keys — Navigate rows and columns
  - `Tab` — Move focus between filter and table
  - Click column headers — Sort by that column

## RELATED LINKS

[Show-ObjectTree](Show-ObjectTree.md)
