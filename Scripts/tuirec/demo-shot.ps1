# SHOT demo for tuirec: a pwsh session running `Get-Process | shot`.
# Prompt printed before Import-Module so the GIF opens on the command line.
$prompt = 'PS ~/PStui> '

Write-Host ''
Write-Host $prompt -NoNewline; Write-Host 'Get-Process | shot'
Import-Module PSTui -ErrorAction Stop
Start-Sleep -Milliseconds 1200
Get-Process |
    Sort-Object -Property CPU -Descending |
    Select-Object -First 15 |
    Show-ObjectTree -Title 'Get-Process | shot' -FullScreen

Write-Host $prompt
Start-Sleep -Milliseconds 800
