
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

task FindDotNet -Before Clean, Build {
    Assert (Get-Command dotnet -ErrorAction SilentlyContinue) "The dotnet CLI was not found, please install it: https://aka.ms/dotnet-cli"
    $DotnetVersion = dotnet --version
    Assert ($?) "The required .NET SDK was not found, please install it: https://aka.ms/dotnet-cli"
    Write-Host "Using dotnet $DotnetVersion at path $((Get-Command dotnet).Source)" -ForegroundColor Green
}

task Clean {
    Remove-BuildItem ./module, ./out
    Push-Location src/PSTui
    Invoke-BuildExec { & dotnet clean }
    Pop-Location
}

task Build {
    New-Item -ItemType Directory -Force ./module | Out-Null
    # Clear stale assemblies so iterative builds don't accumulate dropped deps.
    Remove-Item -Force ./module/*.dll -ErrorAction SilentlyContinue

    Push-Location src/PSTui
    Invoke-BuildExec { & dotnet publish --configuration $Configuration --output publish }

    # Ship only PSTui's own assemblies plus Terminal.Gui's runtime closure.
    # Everything else in publish/ comes from the Microsoft.PowerShell.SDK
    # package graph and is already provided by the PowerShell host in-process,
    # so shipping it would bloat the package (~67 DLLs/29MB -> ~16/9MB) and
    # risk assembly conflicts. The keep-list is derived from PSTui.deps.json so
    # it stays correct if Terminal.Gui's dependencies change.
    $deps   = Get-Content -Raw "./publish/PSTui.deps.json" | ConvertFrom-Json
    $target = ($deps.targets.PSObject.Properties | Select-Object -First 1).Value
    $libs   = @{}
    foreach ($p in $target.PSObject.Properties) { $libs[($p.Name -split '/')[0]] = $p.Value }

    # Transitive closure of Terminal.Gui (deliberately not seeded from PSTui/*,
    # which reference System.Management.Automation and would drag in the SDK).
    $closure = [System.Collections.Generic.HashSet[string]]::new()
    $queue   = [System.Collections.Generic.Queue[string]]::new()
    $queue.Enqueue('Terminal.Gui')
    while ($queue.Count) {
        $name = $queue.Dequeue()
        if (-not $closure.Add($name)) { continue }
        if ($libs[$name].dependencies) {
            foreach ($d in $libs[$name].dependencies.PSObject.Properties.Name) { $queue.Enqueue($d) }
        }
    }

    $shipNames = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($name in $closure) {
        if ($libs[$name].runtime) {
            foreach ($rt in $libs[$name].runtime.PSObject.Properties.Name) {
                [void]$shipNames.Add([System.IO.Path]::GetFileName($rt))
            }
        }
    }
    # PSTui's own assemblies (their System.Management.Automation dependency is host-provided).
    [void]$shipNames.Add('PSTui.dll')
    [void]$shipNames.Add('PSTui.Models.dll')

    $shipped = Get-ChildItem "./publish/*.dll" | Where-Object { $shipNames.Contains($_.Name) }
    $shipped | ForEach-Object { Copy-Item -Force -Path $_.FullName -Destination ../../module }
    Write-Build Green "Packaged $($shipped.Count) assemblies (PSTui + Terminal.Gui closure)."

    # Copy the module manifest
    Copy-Item -Force -Path "./publish/PSTui.psd1" -Destination ../../module

    # Copy the nested script module (F7/Shift+F7 command-history key handlers)
    Copy-Item -Force -Path "./publish/PSTui.History.psm1" -Destination ../../module
    Pop-Location

    $Assets = $(
        "./README.md",
        "./LICENSE.txt",
        "./NOTICE.txt")
    $Assets | ForEach-Object {
        Copy-Item -Force -Path $_ -Destination ./module
    }

    New-ExternalHelp -Path docs/PSTui -OutputPath module/en-US -Force
}

task Test {
    Invoke-BuildExec { & dotnet test PSTui.slnx --configuration $Configuration }

    # PowerShell-level tests (module load, aliases, F7/Shift+F7 key handlers)
    # that the C# xUnit suite cannot cover. These Import-Module the built
    # ./module, whose manifest requires PowerShell 7.6+, so they can only run on
    # a 7.6+ host. CI installs PS 7.6; on an older local host, skip them with a
    # clear message rather than failing on a cryptic manifest-version error.
    $minPwsh = [version]'7.6'
    if ($PSVersionTable.PSVersion -lt $minPwsh) {
        Write-Warning "Skipping Pester tests: they import the PSTui module, which requires PowerShell $minPwsh+ (current: $($PSVersionTable.PSVersion)). The .NET (xUnit) tests above still ran."
        return
    }

    Import-Module Pester -MinimumVersion 5.0 -Force
    $pester = New-PesterConfiguration
    $pester.Run.Path = './test/PSTui.Tests.ps1'
    $pester.Run.PassThru = $true
    $pester.Output.Verbosity = 'Detailed'
    $result = Invoke-Pester -Configuration $pester
    Assert ($result.FailedCount -eq 0) "$($result.FailedCount) Pester test(s) failed."
}

task Package {
    New-Item -ItemType Directory -Force ./out | Out-Null
    if (-Not (Get-PSResourceRepository -Name PSTui -ErrorAction SilentlyContinue)) {
        Register-PSResourceRepository -Name PSTui -Uri ./out
    }
    Publish-PSResource -Path ./module -Repository PSTui -Verbose
}

task . Clean, Build, Test
