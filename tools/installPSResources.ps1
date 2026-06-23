# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
param(
    [ValidateSet("PSGallery", "CFS")]
    [string]$PSRepository = "PSGallery"
)

if ($PSRepository -eq "CFS" -and -not (Get-PSResourceRepository -Name CFS -ErrorAction SilentlyContinue)) {
    Register-PSResourceRepository -Name CFS -Uri "https://pkgs.dev.azure.com/powershell/PowerShell/_packaging/PowerShellGalleryMirror/nuget/v3/index.json"
}

# NOTE: Due to a bug in Install-PSResource with upstream feeds, we have to
# request an exact version. Otherwise, if a newer version is available in the
# upstream feed, it will fail to install any version at all.
$resources = @{
    InvokeBuild = @{
        version = "5.12.1"
        repository = $PSRepository
      }
    platyPS = @{
        version = "0.14.2"
        repository = $PSRepository
    }
}

# Retry: the PowerShell Gallery intermittently returns 5xx (e.g. 504 Gateway
# Time-out), which otherwise fails CI on an infra hiccup rather than a real bug.
$maxAttempts = 5
for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
    try {
        Install-PSResource -Verbose -TrustRepository -RequiredResource $resources
        break
    }
    catch {
        if ($attempt -eq $maxAttempts) { throw }
        $delay = [Math]::Min(60, [Math]::Pow(2, $attempt))   # 2s, 4s, 8s, 16s
        Write-Warning "Install-PSResource attempt $attempt/$maxAttempts failed: $($_.Exception.Message). Retrying in ${delay}s..."
        Start-Sleep -Seconds $delay
    }
}
