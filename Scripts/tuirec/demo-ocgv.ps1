# Hero demo for tuirec: a pwsh session running `ls | ocgv` then `killp`.
# The prompt+command is printed *before* Import-Module so the GIF opens on the
# command line (no blank import lead-in), then the picker opens full-screen.
# We Esc out of both pickers, so nothing is actually killed.
$prompt = 'PS ~/PStui> '

Write-Host ''
Write-Host $prompt -NoNewline; Write-Host 'ls | ocgv'
Import-Module PSTui -ErrorAction Stop
Start-Sleep -Milliseconds 1100
Get-ChildItem |
    Out-ConsoleGridView -Title 'ls | ocgv' -Focus Filter -FullScreen |
    Out-Null

Write-Host $prompt -NoNewline; Write-Host 'killp'
Start-Sleep -Milliseconds 1400
Get-Process |
    Select-Object ProcessName, Id,
        @{ Name = 'CPU(s)'; Expression = { [math]::Round($_.CPU, 2) } },
        @{ Name = 'WS(MB)'; Expression = { [math]::Round($_.WorkingSet64 / 1MB, 2) } } |
    Out-ConsoleGridView -OutputMode Single -Title 'killp - pick a process' -Focus Filter -FullScreen |
    Out-Null

Write-Host $prompt
Start-Sleep -Milliseconds 900
