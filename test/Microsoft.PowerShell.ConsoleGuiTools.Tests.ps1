# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

Describe 'Microsoft.PowerShell.ConsoleGuiTools Module' {

    BeforeAll {
        $modulePath = Join-Path $PSScriptRoot '..' 'module' 'Microsoft.PowerShell.ConsoleGuiTools.psd1'
        if (-not (Test-Path $modulePath)) {
            $modulePath = Join-Path $PSScriptRoot '..' 'src' 'Microsoft.PowerShell.ConsoleGuiTools' 'publish' 'Microsoft.PowerShell.ConsoleGuiTools.psd1'
        }
        if (Test-Path $modulePath) {
            Import-Module $modulePath -Force -ErrorAction Stop
        } else {
            throw "Module not found. Build the project first with 'Invoke-Build Build'."
        }
    }

    Context 'Module loads correctly' {
        It 'Should import without errors' {
            $module = Get-Module -Name Microsoft.PowerShell.ConsoleGuiTools
            $module | Should -Not -BeNullOrEmpty
        }

        It 'Should export Out-ConsoleGridView command' {
            $cmd = Get-Command -Name Out-ConsoleGridView -Module Microsoft.PowerShell.ConsoleGuiTools -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
        }

        It 'Should export Show-ObjectTree command' {
            $cmd = Get-Command -Name Show-ObjectTree -Module Microsoft.PowerShell.ConsoleGuiTools -ErrorAction SilentlyContinue
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
        Remove-Module -Name Microsoft.PowerShell.ConsoleGuiTools -Force -ErrorAction SilentlyContinue
    }
}
