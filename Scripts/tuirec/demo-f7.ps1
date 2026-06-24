# F7 history demo for tuirec. The pwsh REPL itself does not render under a
# recording PTY, so this reproduces exactly what pressing F7 shows: recent
# command history in Out-ConsoleGridView (same shape Show-PSTuiHistory builds),
# with the typed prefix used as the live filter.
Import-Module PSTui -ErrorAction Stop

$history = @(
    'Get-Process | Out-ConsoleGridView'
    'Get-ChildItem -Recurse -Filter *.cs'
    'Get-Service | Where-Object Status -eq Running'
    'git log --oneline -20'
    'dotnet build PSTui.slnx -c Release'
    'Get-Content README.md | Select-String PSTui'
    'Install-Module PSTui'
    'Get-Date -Format o'
    'Get-History | Format-Table -AutoSize'
) | ForEach-Object { [pscustomobject]@{ CommandLine = $_ } }

$history |
    Out-ConsoleGridView -OutputMode Single -Title 'Command History  (F7)' -Focus Filter |
    Out-Null
