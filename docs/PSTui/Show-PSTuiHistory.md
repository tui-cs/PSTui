---
external help file: PSTui.dll-Help.xml
keywords: powershell,cmdlet
locale: en-us
Module Name: PSTui
ms.date: 06/27/2026
schema: 2.0.0
title: Show-PSTuiHistory
---

# Show-PSTuiHistory

## SYNOPSIS

Shows PowerShell command history in `Out-ConsoleGridView` and inserts the selected command at the prompt.

## SYNTAX

```PowerShell
Show-PSTuiHistory [-Global] [<CommonParameters>]
```

## DESCRIPTION

Backs the `F7` / `Shift+F7` command-history key handlers, and can be called or bound to other keys directly. The current prompt text is used as the initial filter; the selected command is inserted at the prompt.

This command is exported (rather than module-private) so the PSReadLine key handlers, which run in the global session state, can resolve it.

## EXAMPLES

### Example 1: Show this session's history

```PowerShell
Show-PSTuiHistory
```

Opens the current session's command history in `Out-ConsoleGridView`. Selecting an entry and pressing `ENTER` inserts it at the prompt.

### Example 2: Bind it to a different key

```PowerShell
Set-PSReadLineKeyHandler -Chord 'Ctrl+r' -ScriptBlock { Show-PSTuiHistory -Global }
```

Binds <kbd>Ctrl</kbd>+<kbd>R</kbd> to the all-sessions history picker.

## PARAMETERS

### -Global

Show history from all PowerShell sessions (PSReadLine), de-duplicated, instead of only the current session.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### None

This command updates the PSReadLine prompt buffer directly; it does not write to the pipeline.

## NOTES

* The bindings register on `Import-Module PSTui`. See [Enable-PSTuiHistoryKeyHandler](Enable-PSTuiHistoryKeyHandler.md) / [Disable-PSTuiHistoryKeyHandler](Disable-PSTuiHistoryKeyHandler.md) to toggle them at runtime.

## RELATED LINKS

[Enable-PSTuiHistoryKeyHandler](Enable-PSTuiHistoryKeyHandler.md)

[Disable-PSTuiHistoryKeyHandler](Disable-PSTuiHistoryKeyHandler.md)

[Out-ConsoleGridView](Out-ConsoleGridView.md)
