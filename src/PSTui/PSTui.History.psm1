#
# Copyright (c) Tig Kindel and tui-cs contributors.
# Licensed under the MIT license.
#
# Graphical command-history key handlers for PSTui, folded in from the
# standalone F7History module (https://github.com/tui-cs/F7History).
#
#   F7        -> this session's command history
#   Shift+F7  -> global history across all PowerShell sessions (PSReadLine)
#
# The typed prefix is used as the initial filter, and the selected command is
# inserted at the prompt. Enabled by default. To opt out:
#   * set $env:PSTUI_DISABLE_HISTORY_KEYS to 1/true/yes, or
#   * set $PSTuiDisableHistoryKeyHandler = $true *before* Import-Module PSTui, or
#   * run Disable-PSTuiHistoryKeyHandler at any time.
#

$script:F7Chord      = 'F7'
$script:ShiftF7Chord = 'Shift+F7'

<#
.SYNOPSIS
    Shows PowerShell command history in Out-ConsoleGridView and inserts the
    selected command at the prompt.
.DESCRIPTION
    Backs the F7 / Shift+F7 key handlers, and can be called or bound to other
    keys directly. The current prompt text is used as the initial filter; the
    selected command is inserted at the prompt.

    This command is exported (rather than module-private) so the PSReadLine key
    handlers, which run in the global session state, can resolve it — see #15.
.PARAMETER Global
    Show history from all PowerShell sessions (PSReadLine) instead of only this one.
#>
function Show-PSTuiHistory {
    [CmdletBinding()]
    param(
        [switch] $Global
    )

    $line = $null
    $cursor = $null
    [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$line, [ref]$cursor)

    if ($Global) {
        $title = 'Command History (all PowerShell sessions)'
        $items = [Microsoft.PowerShell.PSConsoleReadLine]::GetHistoryItems()
        if (-not $items) { return }
        [array]::Reverse($items)
        $seen = @{}
        $history = foreach ($item in $items) {
            $cmd = $item.CommandLine
            if ($cmd -and -not $seen.ContainsKey($cmd)) {
                $seen[$cmd] = $true
                [PSCustomObject]@{ CommandLine = $cmd }
            }
        }
    }
    else {
        $title = 'Command History'
        $history = Get-History |
            Sort-Object -Descending -Property Id |
            Select-Object -ExpandProperty CommandLine -Unique |
            ForEach-Object { [PSCustomObject]@{ CommandLine = $_ } }
    }

    if (-not $history) { return }

    $selection = $history | Out-ConsoleGridView -OutputMode Single -Title $title -Filter $line

    # Re-anchor PSReadLine's prompt at the *current* cursor row before touching
    # the buffer. Under Terminal.Gui v2, Out-ConsoleGridView renders inline by
    # default, so the screen has scrolled and PSReadLine's saved prompt row
    # (_initialY) is now stale; a plain DeleteLine/Render would repaint the
    # prompt in the wrong place (the F7History bug fixed in tui-cs/F7History#25).
    # Passing CursorTop as the arg makes InvokePrompt re-anchor on this row.
    [Microsoft.PowerShell.PSConsoleReadLine]::InvokePrompt($null, [Console]::CursorTop)

    # Replace the typed prefix (used above as the filter) with the selection.
    [Microsoft.PowerShell.PSConsoleReadLine]::DeleteLine()
    if ($selection) {
        [Microsoft.PowerShell.PSConsoleReadLine]::Insert($selection.CommandLine)
    }
}

<#
.SYNOPSIS
    Binds F7 / Shift+F7 to PSTui's graphical command history.
.DESCRIPTION
    F7 shows this session's history; Shift+F7 shows global PSReadLine history.
    Called automatically when PSTui is imported (unless opted out). Safe to call
    again to re-bind after Disable-PSTuiHistoryKeyHandler.
#>
function Enable-PSTuiHistoryKeyHandler {
    [CmdletBinding()]
    param()

    if (-not (Get-Command -Name Set-PSReadLineKeyHandler -ErrorAction SilentlyContinue)) {
        Write-Verbose 'PSReadLine is not available; PSTui history key handlers were not set.'
        return
    }

    Set-PSReadLineKeyHandler -Chord $script:F7Chord `
        -BriefDescription 'PSTui: Command History' `
        -Description "Show this session's command history in Out-ConsoleGridView (PSTui)" `
        -ScriptBlock { Show-PSTuiHistory }

    Set-PSReadLineKeyHandler -Chord $script:ShiftF7Chord `
        -BriefDescription 'PSTui: Command History (all sessions)' `
        -Description 'Show global command history in Out-ConsoleGridView (PSTui)' `
        -ScriptBlock { Show-PSTuiHistory -Global }
}

<#
.SYNOPSIS
    Removes PSTui's F7 / Shift+F7 command-history key bindings.
#>
function Disable-PSTuiHistoryKeyHandler {
    [CmdletBinding()]
    param()

    if (-not (Get-Command -Name Remove-PSReadLineKeyHandler -ErrorAction SilentlyContinue)) {
        return
    }

    foreach ($chord in $script:F7Chord, $script:ShiftF7Chord) {
        try { Remove-PSReadLineKeyHandler -Chord $chord -ErrorAction Stop } catch { }
    }
}

# --- Auto-enable on import, unless the user opted out ------------------------
$optOut = ($env:PSTUI_DISABLE_HISTORY_KEYS -and
           $env:PSTUI_DISABLE_HISTORY_KEYS -in @('1', 'true', 'yes', 'on')) -or
          ($global:PSTuiDisableHistoryKeyHandler -eq $true)

if (-not $optOut) {
    # Never let key-handler setup break module import (e.g. no console host in CI).
    try { Enable-PSTuiHistoryKeyHandler } catch {
        Write-Verbose "PSTui: could not set history key handlers: $($_.Exception.Message)"
    }
}

Export-ModuleMember -Function Show-PSTuiHistory, Enable-PSTuiHistoryKeyHandler, Disable-PSTuiHistoryKeyHandler
