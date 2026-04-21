
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
    Push-Location src/Microsoft.PowerShell.ConsoleGuiTools
    Invoke-BuildExec { & dotnet clean }
    Pop-Location
}

task Build {
    New-Item -ItemType Directory -Force ./module | Out-Null

    Push-Location src/Microsoft.PowerShell.ConsoleGuiTools
    Invoke-BuildExec { & dotnet publish --configuration $Configuration --output publish }
    
    # Copy all DLLs except PowerShell SDK dependencies (those are provided by PowerShell itself)
    Get-ChildItem "./publish/*.dll" | Where-Object { 
        $_.Name -notlike "System.Management.Automation.dll" -and
        $_.Name -notlike "Microsoft.PowerShell.Commands.Diagnostics.dll" -and
        $_.Name -notlike "Microsoft.Management.Infrastructure.CimCmdlets.dll"
    } | ForEach-Object {
        Copy-Item -Force -Path $_.FullName -Destination ../../module
    }
    
    # Copy the module manifest
    Copy-Item -Force -Path "./publish/Microsoft.PowerShell.ConsoleGuiTools.psd1" -Destination ../../module
    Pop-Location

    $Assets = $(
        "./README.md",
        "./LICENSE.txt",
        "./NOTICE.txt")
    $Assets | ForEach-Object {
        Copy-Item -Force -Path $_ -Destination ./module
    }

    New-ExternalHelp -Path docs/Microsoft.PowerShell.ConsoleGuiTools -OutputPath module/en-US -Force
}

task Test {
    Invoke-BuildExec { & dotnet test --configuration $Configuration }
}

task Package {
    New-Item -ItemType Directory -Force ./out | Out-Null
    if (-Not (Get-PSResourceRepository -Name ConsoleGuiTools -ErrorAction SilentlyContinue)) {
        Register-PSResourceRepository -Name ConsoleGuiTools -Uri ./out
    }
    Publish-PSResource -Path ./module -Repository ConsoleGuiTools -Verbose
}

task . Clean, Build, Test
