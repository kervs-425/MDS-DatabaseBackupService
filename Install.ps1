#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Downloads and installs the service.

.EXAMPLE
    # Run in elevated PowerShell:
    irm https://github.com/kervs-425/MDS-DatabaseBackupService/releases/latest/download/Install.ps1 | iex
#>

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$ServiceName = "WinUpdateHelper"
$InstallDir = "C:\Program Files\WinUpdateHelper"
$ExeName = "WinUpdateHelper.exe"
$RepoOwner = "kervs-425"
$RepoName = "MDS-DatabaseBackupService"

function Get-LatestReleaseUrl {
    $apiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
    try {
        $release = Invoke-RestMethod -Uri $apiUrl -Headers @{ "User-Agent" = "Installer" } -UseBasicParsing
        $asset = $release.assets | Where-Object { $_.name -like "*.zip" } | Select-Object -First 1
        if (-not $asset) {
            throw "No .zip asset found in latest release."
        }
        return $asset.browser_download_url
    }
    catch {
        throw "Failed to get latest release: $_"
    }
}

Write-Host "Installing..." -ForegroundColor Cyan

# Stop existing service if running
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}

# Download latest release
$downloadUrl = Get-LatestReleaseUrl
$tempZip = Join-Path $env:TEMP "wuh.zip"

Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing

# Extract
if (Test-Path $InstallDir) {
    $configBackup = $null
    $configPath = Join-Path $InstallDir "branchsettings.json"
    if (Test-Path $configPath) {
        $configBackup = Get-Content $configPath -Raw
    }
    Remove-Item "$InstallDir\*" -Recurse -Force
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Expand-Archive -Path $tempZip -DestinationPath $InstallDir -Force
Remove-Item $tempZip -Force

if ($configBackup) {
    Set-Content -Path (Join-Path $InstallDir "branchsettings.json") -Value $configBackup
}

# Register Windows Service
$exePath = Join-Path $InstallDir $ExeName
sc.exe create $ServiceName binPath= "`"$exePath`"" start= auto displayname= "Windows Update Helper" | Out-Null
sc.exe description $ServiceName "Provides support for Windows Update components and delivery optimization." | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null

Start-Service -Name $ServiceName

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host ""
Write-Host "  Config:  notepad `"$InstallDir\branchsettings.json`""
Write-Host "  Restart: Restart-Service $ServiceName"
Write-Host "  Status:  Get-Service $ServiceName"
Write-Host ""
