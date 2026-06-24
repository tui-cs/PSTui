# SHOT demo for tuirec: explore objects as an interactive tree (full screen).
Import-Module PSTui -ErrorAction Stop
Get-Process |
    Sort-Object -Property CPU -Descending |
    Select-Object -First 15 |
    Show-ObjectTree -Title 'Get-Process | shot' -FullScreen
