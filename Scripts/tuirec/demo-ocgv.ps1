# Hero demo for tuirec: `ls | ocgv` followed by the `killp` process picker.
# Run head-less by tuirec (see README.md). We Esc out of both pickers, so no
# process is actually killed.

Import-Module PSTui -ErrorAction Stop

# 1) ls | ocgv — browse the current directory as an interactive table.
Get-ChildItem |
    Out-ConsoleGridView -Title 'ls | ocgv' -Focus Filter |
    Out-Null

# 2) killp — pick a process from a graphical chooser.
Get-Process |
    Select-Object ProcessName, Id,
        @{ Name = 'CPU(s)'; Expression = { [math]::Round($_.CPU, 2) } },
        @{ Name = 'WS(MB)'; Expression = { [math]::Round($_.WorkingSet64 / 1MB, 2) } } |
    Out-ConsoleGridView -OutputMode Single -Title 'killp  -  pick a process' -Focus Filter |
    Out-Null
