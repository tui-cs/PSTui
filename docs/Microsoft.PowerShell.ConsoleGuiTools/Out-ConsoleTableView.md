---
external help file: ConsoleGuiToolsModule.dll-Help.xml
keywords: powershell,cmdlet
locale: en-us
Module Name: Microsoft.PowerShell.ConsoleGuiTools
ms.date: 05/01/2026
schema: 2.0.0
title: Out-ConsoleTableView
---

# Out-ConsoleTableView

## SYNOPSIS

Sends output to an interactive table view with column headers, horizontal scrolling, and native multi-selection.

## SYNTAX

```PowerShell
 Out-ConsoleTableView [-InputObject <psobject>] [-Title <string>] [-OutputMode {None | Single |
    Multiple}] [-Filter <string>] [-Search <string>] [-Focus {Table | Filter}] [-MinUI]
    [-FullScreen] [-ForceDriver <string>] [<CommonParameters>]
```

## DESCRIPTION

The **Out-ConsoleTableView** cmdlet sends the output from a command to a table view window where the output is displayed in an interactive table with column headers, column sizing, and horizontal scrolling.

Use the Filter box at the top of the window to search the text in the table using regular expressions. Unlike the Filter, the `-Search` parameter positions the cursor on the first matching row without hiding non-matching rows.

Objects are streamed into the table as they arrive from the pipeline — the UI appears immediately and rows are added incrementally.

To send items from the interactive window down the pipeline, select rows (use arrow keys and `SPACE` or click with the mouse) and press `ENTER`. Press `ESC` to cancel without output. Use `Ctrl+A` to select all rows.

## EXAMPLES

### Example 1: Output processes to a table view

```PowerShell
Get-Process | Out-ConsoleTableView
```

This command gets the processes running on the local computer and sends them to a table view window with column headers for each property.

### Example 2: Select a single process using the table view

```PowerShell
Get-Process | octv -OutputMode Single | Stop-Process
```

This command displays processes in a table view restricted to single selection. The selected process is piped to `Stop-Process`.

### Example 3: Filter processes by name on the command line

```PowerShell
Get-Process | octv -Filter "chrome"
```

This command pre-populates the filter box with "chrome", showing only processes whose properties match that regex pattern.

### Example 4: Search for a row without filtering

```PowerShell
Get-Service | octv -Search "wuauserv"
```

This command displays all services but positions the cursor on the first row matching "wuauserv". Unlike `-Filter`, all rows remain visible.

### Example 5: Start with focus on the filter field

```PowerShell
Get-ChildItem | octv -Focus Filter
```

This command opens the table view with the cursor in the filter text field, ready to type a filter immediately. Pressing `ENTER` while in the filter field accepts the currently selected item(s).

### Example 6: Full screen mode with a custom title

```PowerShell
Get-Process | octv -FullScreen -Title "Process Monitor"
```

This command runs the table view in full-screen mode using the alternate screen buffer, with a custom window title.

### Example 7: Minimal UI for scripting

```PowerShell
Get-Process | octv -MinUI -OutputMode Single
```

This command shows the table view with no window frame, filter box, or status bar — just the table. Useful for quick selection in scripts.

### Example 8: Combine Filter and Search

```PowerShell
Get-Process | octv -Filter "svc" -Search "host"
```

This filters to rows matching "svc" and then positions the cursor on the first of those rows matching "host".

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
Specifies that the cmdlet accepts input for **Out-ConsoleTableView**.

When you use the **InputObject** parameter to send a collection of objects to **Out-ConsoleTableView**, **Out-ConsoleTableView** treats the collection as one collection object, and it displays one row that represents the collection.

To display each object in the collection, use a pipeline operator (|) to send objects to **Out-ConsoleTableView**.

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
Specifies the text that appears in the title bar of the **Out-ConsoleTableView** window.

By default, the title bar displays "Out-ConsoleTableView".

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: Out-ConsoleTableView
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

### -ForceDriver
Forces the Terminal.Gui driver to use. Valid values are `ansi`, `windows`, or `unix`.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.Management.Automation.PSObject

You can send any object to this cmdlet. Objects are streamed — the UI appears as soon as the first object arrives.

## OUTPUTS

### System.Object

By default **Out-ConsoleTableView** returns objects representing the selected rows to the pipeline. Use `-OutputMode` to change this behavior.

## NOTES

* **Out-ConsoleTableView** uses Terminal.Gui's `TableView` control which provides column headers, column sizing, horizontal scrolling, and native multi-row selection.

* The alias for **Out-ConsoleTableView** is `octv`.

* Objects are streamed into the table as they arrive from the pipeline. The UI appears immediately on the first object and rows are added incrementally. A spinner in the status bar indicates loading is in progress.

* The command output that you send to **Out-ConsoleTableView** should not be formatted, such as by using the Format-Table or Format-Wide cmdlets. To select properties, use the Select-Object cmdlet.

* Keyboard shortcuts:
  - `ENTER` — Accept selection and close
  - `ESC` — Cancel and close
  - `Ctrl+A` — Select all rows (when OutputMode is Multiple)
  - `Home`/`End` — Jump to first/last row
  - Arrow keys — Navigate rows and columns
  - `Tab` — Move focus between filter and table

## RELATED LINKS

[Out-ConsoleGridView](Out-ConsoleGridView.md)
