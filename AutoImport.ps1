# AutoImport.ps1
# Scans Google Drive for the latest backup per branch and imports into local MySQL.

param(
    [string]$GoogleDriveFolder  = "H:\My Drive\UpdateCache",
    [string]$MySqlPath          = "C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe",
    [string]$Host               = "localhost",
    [int]   $Port               = 3306,
    [string]$Username           = "root",
    [string]$Password           = "password",
    [string]$DatabasePrefix     = "mdsbillingdbv5",
    [string]$StateFile          = "C:\ProgramData\WinUpdateHelper\State\import-state.json",
    [string]$LogFile            = "C:\ProgramData\WinUpdateHelper\Logs\import.log"
)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Write-Log {
    param([string]$Level, [string]$Message)
    $line = "[{0:yyyy-MM-dd HH:mm:ss}] [{1}] {2}" -f (Get-Date), $Level, $Message
    Add-Content -Path $LogFile -Value $line
    Write-Host $line
}

function Load-State {
    if (Test-Path $StateFile) {
        return Get-Content $StateFile -Raw | ConvertFrom-Json -AsHashtable
    }
    return @{}
}

function Save-State([hashtable]$State) {
    $State | ConvertTo-Json | Set-Content -Path $StateFile
}

function Parse-BranchName([string]$BaseName) {
    # Format: {branch}_{YYYYMMDD}_{HHmmss}
    # Branch name is everything except the last two underscore segments
    $parts = $BaseName -split '_'
    if ($parts.Count -lt 3) { return $null }
    return ($parts[0..($parts.Count - 3)]) -join '_'
}

# ---------------------------------------------------------------------------
# Preflight
# ---------------------------------------------------------------------------

New-Item -ItemType Directory -Path (Split-Path $StateFile) -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path $LogFile)  -Force | Out-Null

Write-Log "INFO" "Auto-import started."

if (-not (Test-Path $MySqlPath)) {
    Write-Log "ERROR" "mysql.exe not found at '$MySqlPath'."
    exit 1
}

if (-not (Test-Path $GoogleDriveFolder)) {
    Write-Log "ERROR" "Google Drive folder not found: '$GoogleDriveFolder'."
    exit 1
}

# ---------------------------------------------------------------------------
# Scan and group files by branch
# ---------------------------------------------------------------------------

$files = Get-ChildItem -Path $GoogleDriveFolder -Filter "*.dat" -File

if ($files.Count -eq 0) {
    Write-Log "INFO" "No .dat files found in '$GoogleDriveFolder'. Nothing to import."
    exit 0
}

$byBranch = @{}
foreach ($file in $files) {
    $branch = Parse-BranchName $file.BaseName
    if ($null -eq $branch) {
        Write-Log "INFO" "Skipping '$($file.Name)' — unexpected filename format."
        continue
    }
    if (-not $byBranch.ContainsKey($branch)) {
        $byBranch[$branch] = @()
    }
    $byBranch[$branch] += $file
}

# ---------------------------------------------------------------------------
# Import latest file per branch
# ---------------------------------------------------------------------------

$state = Load-State
$anyError = $false

foreach ($branch in $byBranch.Keys) {
    $latest = $byBranch[$branch] | Sort-Object Name -Descending | Select-Object -First 1
    $dbName = "${DatabasePrefix}_${branch}"

    if ($state.ContainsKey($branch) -and $state[$branch] -eq $latest.Name) {
        Write-Log "INFO" "[$branch] Already up to date ($($latest.Name)). Skipping."
        continue
    }

    Write-Log "INFO" "[$branch] Importing '$($latest.Name)' into database '$dbName'..."

    try {
        # Drop and recreate database for a clean overwrite
        $resetSql = "DROP DATABASE IF EXISTS ``$dbName``; CREATE DATABASE ``$dbName``;"
        $resetArgs = @(
            "--host=$Host",
            "--port=$Port",
            "--user=$Username",
            "--execute=$resetSql"
        )

        $env:MYSQL_PWD = $Password
        $resetResult = & $MySqlPath @resetArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to reset database '$dbName': $resetResult"
        }

        # Import the dump
        $importArgs = @(
            "--host=$Host",
            "--port=$Port",
            "--user=$Username",
            $dbName
        )

        Get-Content $latest.FullName -Raw | & $MySqlPath @importArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "mysql.exe exited with code $LASTEXITCODE."
        }

        $state[$branch] = $latest.Name
        Save-State $state
        Write-Log "INFO" "[$branch] Import complete."
    }
    catch {
        Write-Log "ERROR" "[$branch] Import failed: $_"
        $anyError = $true
    }
    finally {
        Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
    }
}

Write-Log "INFO" "Auto-import finished."
exit ($anyError ? 1 : 0)
