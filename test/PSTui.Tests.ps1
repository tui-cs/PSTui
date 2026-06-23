# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

Describe 'PSTui Module' {

    BeforeAll {
        $modulePath = Join-Path $PSScriptRoot '..' 'module' 'PSTui.psd1'
        if (-not (Test-Path $modulePath)) {
            $modulePath = Join-Path $PSScriptRoot '..' 'src' 'PSTui' 'publish' 'PSTui.psd1'
        }
        if (Test-Path $modulePath) {
            Import-Module $modulePath -Force -ErrorAction Stop
        } else {
            throw "Module not found. Build the project first with 'Invoke-Build Build'."
        }
    }

    Context 'Module loads correctly' {
        It 'Should import without errors' {
            $module = Get-Module -Name PSTui
            $module | Should -Not -BeNullOrEmpty
        }

        It 'Should export Out-ConsoleGridView command' {
            $cmd = Get-Command -Name Out-ConsoleGridView -Module PSTui -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
        }

        It 'Should export Show-ObjectTree command' {
            $cmd = Get-Command -Name Show-ObjectTree -Module PSTui -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Aliases' {
        It 'Should register ocgv alias for Out-ConsoleGridView' {
            $alias = Get-Alias -Name ocgv -ErrorAction SilentlyContinue
            $alias | Should -Not -BeNullOrEmpty
            $alias.Definition | Should -Be 'Out-ConsoleGridView'
        }

        It 'Should register shot alias for Show-ObjectTree' {
            $alias = Get-Alias -Name shot -ErrorAction SilentlyContinue
            $alias | Should -Not -BeNullOrEmpty
            $alias.Definition | Should -Be 'Show-ObjectTree'
        }
    }

    Context 'Command-history key handlers (folded-in F7History)' {
        It 'Should export Enable-PSTuiHistoryKeyHandler' {
            Get-Command -Name Enable-PSTuiHistoryKeyHandler -Module PSTui -ErrorAction SilentlyContinue |
                Should -Not -BeNullOrEmpty
        }

        It 'Should export Disable-PSTuiHistoryKeyHandler' {
            Get-Command -Name Disable-PSTuiHistoryKeyHandler -Module PSTui -ErrorAction SilentlyContinue |
                Should -Not -BeNullOrEmpty
        }

        It 'Should bind F7 and Shift+F7 to PSTui by default when PSReadLine is available' -Skip:(-not (Get-Command Get-PSReadLineKeyHandler -ErrorAction SilentlyContinue)) {
            $bound = Get-PSReadLineKeyHandler | Where-Object { $_.Function -like 'PSTui*' }
            @($bound).Key | Should -Contain 'F7'
            @($bound).Key | Should -Contain 'Shift+F7'
        }

        It 'Disable-PSTuiHistoryKeyHandler removes the bindings; Enable restores them' -Skip:(-not (Get-Command Get-PSReadLineKeyHandler -ErrorAction SilentlyContinue)) {
            Disable-PSTuiHistoryKeyHandler
            @(Get-PSReadLineKeyHandler | Where-Object { $_.Function -like 'PSTui*' }).Count | Should -Be 0
            Enable-PSTuiHistoryKeyHandler
            @(Get-PSReadLineKeyHandler | Where-Object { $_.Function -like 'PSTui*' }).Count | Should -Be 2
        }
    }

    Context 'Out-ConsoleGridView parameters' {
        It 'Should have InputObject parameter' {
            $cmd = Get-Command Out-ConsoleGridView
            $cmd.Parameters.Keys | Should -Contain 'InputObject'
        }

        It 'Should have Title parameter' {
            $cmd = Get-Command Out-ConsoleGridView
            $cmd.Parameters.Keys | Should -Contain 'Title'
        }

        It 'Should have OutputMode parameter' {
            $cmd = Get-Command Out-ConsoleGridView
            $cmd.Parameters.Keys | Should -Contain 'OutputMode'
        }

        It 'Should have Filter parameter' {
            $cmd = Get-Command Out-ConsoleGridView
            $cmd.Parameters.Keys | Should -Contain 'Filter'
        }

        It 'Should have MinUI parameter' {
            $cmd = Get-Command Out-ConsoleGridView
            $cmd.Parameters.Keys | Should -Contain 'MinUI'
        }
    }

    Context 'Show-ObjectTree parameters' {
        It 'Should have InputObject parameter' {
            $cmd = Get-Command Show-ObjectTree
            $cmd.Parameters.Keys | Should -Contain 'InputObject'
        }

        It 'Should have Title parameter' {
            $cmd = Get-Command Show-ObjectTree
            $cmd.Parameters.Keys | Should -Contain 'Title'
        }

        It 'Should have Filter parameter' {
            $cmd = Get-Command Show-ObjectTree
            $cmd.Parameters.Keys | Should -Contain 'Filter'
        }

        It 'Should have MinUI parameter' {
            $cmd = Get-Command Show-ObjectTree
            $cmd.Parameters.Keys | Should -Contain 'MinUI'
        }
    }

    AfterAll {
        Remove-Module -Name PSTui -Force -ErrorAction SilentlyContinue
    }
}
