---
external help file: PSTui.dll-Help.xml
keywords: powershell,cmdlet
locale: en-us
Module Name: PSTui
ms.date: 06/27/2026
schema: 2.0.0
title: Disable-PSTuiHistoryKeyHandler
---

# Disable-PSTuiHistoryKeyHandler

## SYNOPSIS

Removes PSTui's `F7` / `Shift+F7` command-history key bindings.

## SYNTAX

```PowerShell
Disable-PSTuiHistoryKeyHandler [<CommonParameters>]
```

## DESCRIPTION

Removes the `F7` and `Shift+F7` PSReadLine key handlers that PSTui registers on import. Use [Enable-PSTuiHistoryKeyHandler](Enable-PSTuiHistoryKeyHandler.md) to re-add them. If PSReadLine is not available, the command is a no-op.

## EXAMPLES

### Example 1: Remove the bindings for this session

```PowerShell
Disable-PSTuiHistoryKeyHandler
```

Unbinds `F7` and `Shift+F7` for the current session.

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### None

## NOTES

* To opt out permanently, set `$PSTuiDisableHistoryKeyHandler = $true` or `$env:PSTUI_DISABLE_HISTORY_KEYS = 1` **before** importing PSTui, rather than calling this at runtime.

## RELATED LINKS

[Enable-PSTuiHistoryKeyHandler](Enable-PSTuiHistoryKeyHandler.md)

[Show-PSTuiHistory](Show-PSTuiHistory.md)
