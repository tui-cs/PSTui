---
external help file: PSTui.dll-Help.xml
keywords: powershell,cmdlet
locale: en-us
Module Name: PSTui
ms.date: 06/27/2026
schema: 2.0.0
title: Enable-PSTuiHistoryKeyHandler
---

# Enable-PSTuiHistoryKeyHandler

## SYNOPSIS

Binds `F7` / `Shift+F7` to PSTui's graphical command history.

## SYNTAX

```PowerShell
Enable-PSTuiHistoryKeyHandler [<CommonParameters>]
```

## DESCRIPTION

`F7` shows the current session's history; `Shift+F7` shows global PSReadLine history. This command is called automatically when PSTui is imported (unless opted out), and is safe to call again to re-bind after [Disable-PSTuiHistoryKeyHandler](Disable-PSTuiHistoryKeyHandler.md).

If PSReadLine is not available (for example, a non-interactive host), the command is a no-op and writes a verbose message.

To opt out of the automatic binding on import, set either `$PSTuiDisableHistoryKeyHandler = $true` (a PowerShell variable) or `$env:PSTUI_DISABLE_HISTORY_KEYS = 1` (an environment variable) **before** importing PSTui.

## EXAMPLES

### Example 1: Re-enable the bindings

```PowerShell
Enable-PSTuiHistoryKeyHandler
```

Re-binds `F7` and `Shift+F7` after they were removed with [Disable-PSTuiHistoryKeyHandler](Disable-PSTuiHistoryKeyHandler.md).

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### None

## NOTES

* The bindings only register on `Import-Module PSTui` — `Install-Module` alone does not bind the keys. Add `Import-Module PSTui` to your `$PROFILE` to get them in every session.

## RELATED LINKS

[Disable-PSTuiHistoryKeyHandler](Disable-PSTuiHistoryKeyHandler.md)

[Show-PSTuiHistory](Show-PSTuiHistory.md)
