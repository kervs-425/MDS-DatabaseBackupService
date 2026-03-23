#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Downloads and installs MDS Database Backup Service as a Windows Service.

.DESCRIPTION
    Run this script on any branch PC to install the backup service.
    It downloads the latest release from GitHub, installs to Program Files,
    and registers as a Windows Service.

.EXAMPLE
    # Run in elevated PowerShell:
    irm https://github.com/kervs-425/MDS-DatabaseBackupService/releases/latest/download/Install.ps1 | iex
#>

$ServiceName = "MDS-DatabaseBackup"
$InstallDir = "C:\Program Files\MDS-DatabaseBackupService"
$ExeName = "MDS.DatabaseBackupService.exe"
$RepoOwner = "kervs-425"
$RepoName = "MDS-DatabaseBackupService"

function Get-LatestReleaseUrl {
    $apiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
    try {
        $release = Invoke-RestMethod -Uri $apiUrl -Headers @{ "User-Agent" = "MDS-Installer" }
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

Write-Host "=== MDS Database Backup Service Installer ===" -ForegroundColor Cyan
Write-Host ""

# Stop existing service if running
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Stopping existing service..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "Removing existing service..."
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}

# Download latest release
Write-Host "Getting latest release..."
$downloadUrl = Get-LatestReleaseUrl
$tempZip = Join-Path $env:TEMP "MDS-BackupService.zip"

Write-Host "Downloading from: $downloadUrl"
Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing

# Extract
Write-Host "Installing to: $InstallDir"
if (Test-Path $InstallDir) {
    # Preserve existing config
    $configBackup = $null
    $configPath = Join-Path $InstallDir "branchsettings.json"
    if (Test-Path $configPath) {
        $configBackup = Get-Content $configPath -Raw
        Write-Host "Existing config preserved."
    }

    Remove-Item "$InstallDir\*" -Recurse -Force
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Expand-Archive -Path $tempZip -DestinationPath $InstallDir -Force
Remove-Item $tempZip -Force

# Restore config if it existed
if ($configBackup) {
    Set-Content -Path (Join-Path $InstallDir "branchsettings.json") -Value $configBackup
}

# Register Windows Service
$exePath = Join-Path $InstallDir $ExeName
Write-Host "Registering Windows Service..."
sc.exe create $ServiceName binPath= "`"$exePath`"" start= auto displayname= "MDS Database Backup" | Out-Null
sc.exe description $ServiceName "Automated MySQL database backup service for MDS Water Billing" | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null

# Start service
Write-Host "Starting service..."
Start-Service -Name $ServiceName

Write-Host ""
Write-Host "=== Installation Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Edit config:  notepad `"$InstallDir\branchsettings.json`""
Write-Host "  2. Restart:      Restart-Service $ServiceName"
Write-Host "  3. Check status: Get-Service $ServiceName"
Write-Host "  4. View logs:    Get-Content `"C:\ProgramData\MDS-BranchBackup\Logs\backup.log`" -Tail 20"
Write-Host ""
