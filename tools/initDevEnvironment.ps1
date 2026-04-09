# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

<#
.SYNOPSIS
    Initializes the development environment for GraphicalTools.
.DESCRIPTION
    Creates IDE support files (.sln, launchSettings.json, .vscode/settings.json)
    that are .gitignored per the PowerShell team convention. Run this after cloning
    the repository to enable Visual Studio and VS Code development/debugging.

    The template files are stored in tools/ide/ and copied to their expected locations.
#>

param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot/..

try {
    $templateDir = "tools/ide"

    # --- Solution file ---
    $slnPath = "GraphicalTools.sln"
    if ($Force -or -not (Test-Path $slnPath)) {
        Write-Host "Creating $slnPath..." -ForegroundColor Cyan
        Remove-Item -Force GraphicalTools.slnx -ErrorAction SilentlyContinue
        & dotnet new sln --name GraphicalTools --force --format sln | Out-Null
        & dotnet sln add src/Microsoft.PowerShell.ConsoleGuiTools/Microsoft.PowerShell.ConsoleGuiTools.csproj --solution-folder src
        & dotnet sln add src/Microsoft.PowerShell.OutGridView.Models/Microsoft.PowerShell.OutGridView.Models.csproj --solution-folder src
        Write-Host "  Created $slnPath" -ForegroundColor Green
    } else {
        Write-Host "Skipping $slnPath (already exists, use -Force to overwrite)" -ForegroundColor Yellow
    }

    # --- Visual Studio launch profiles ---
    $launchDest = "src/Microsoft.PowerShell.ConsoleGuiTools/Properties/launchSettings.json"
    if ($Force -or -not (Test-Path $launchDest)) {
        Write-Host "Creating $launchDest..." -ForegroundColor Cyan
        New-Item -ItemType Directory -Force (Split-Path $launchDest) | Out-Null

        # Read template and replace pwsh.exe placeholder with actual path
        $pwshPreview = "C:\Program Files\PowerShell\7-preview\pwsh.exe"
        $pwshStable = "C:\Program Files\PowerShell\7\pwsh.exe"
        if (Test-Path $pwshPreview) {
            $pwsh = $pwshPreview
        } elseif (Test-Path $pwshStable) {
            $pwsh = $pwshStable
        } else {
            $pwsh = (Get-Command pwsh -ErrorAction SilentlyContinue).Source
            if (-not $pwsh) {
                Write-Warning "Could not find pwsh.exe. Launch profiles will need manual path correction."
                $pwsh = "pwsh.exe"
            }
        }

        $json = Get-Content "$templateDir/launchSettings.json" -Raw
        $json = $json.Replace('"pwsh.exe"', "`"$($pwsh.Replace('\', '\\'))`"")
        Set-Content -Path $launchDest -Value $json

        Write-Host "  Created $launchDest (using $pwsh)" -ForegroundColor Green
    } else {
        Write-Host "Skipping $launchDest (already exists, use -Force to overwrite)" -ForegroundColor Yellow
    }

    # --- VS Code settings ---
    $vscodeDest = ".vscode/settings.json"
    if ($Force -or -not (Test-Path $vscodeDest)) {
        Write-Host "Creating $vscodeDest..." -ForegroundColor Cyan
        New-Item -ItemType Directory -Force .vscode | Out-Null
        Copy-Item "$templateDir/.vscode/settings.json" $vscodeDest
        Write-Host "  Created $vscodeDest" -ForegroundColor Green
    } else {
        Write-Host "Skipping $vscodeDest (already exists, use -Force to overwrite)" -ForegroundColor Yellow
    }

    Write-Host "`nDev environment initialized! Open GraphicalTools.sln in Visual Studio or the root folder in VS Code." -ForegroundColor Green
} finally {
    Pop-Location
}
