# AutoImport.ps1
# Scans Google Drive for the latest backup per branch and imports into local MySQL.

param(
    [string[]]$GoogleDriveFolders = @("H:\My Drive\UpdateCache"),
    [string]$MySqlPath         = "",
    [string]$MySqlHost         = "localhost",
    [int]   $Port              = 3306,
    [string]$Username          = "root",
    [string]$Password          = "password",
    [string]$DatabasePrefix    = "mdsbillingdbv5",
    [string]$StateFile         = "C:\ProgramData\WinUpdateHelper\State\import-state.json",
    [string]$LogFile           = "C:\ProgramData\WinUpdateHelper\Logs\import.log"
)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

if ([string]::IsNullOrEmpty($MySqlPath)) {
    $MySqlPath = Get-ChildItem "C:\Program Files\MySQL" -Recurse -Filter "mysql.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like "*Server*" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

function Write-Log {
    param([string]$Level, [string]$Message)
    $line = "[{0:yyyy-MM-dd HH:mm:ss}] [{1}] {2}" -f (Get-Date), $Level, $Message
    Add-Content -Path $LogFile -Value $line
    Write-Host $line
}

function Load-State {
    if (Test-Path $StateFile) {
        $raw = Get-Content $StateFile -Raw
        $obj = ConvertFrom-Json $raw
        $ht = @{}
        $obj.PSObject.Properties | ForEach-Object { $ht[$_.Name] = $_.Value }
        return $ht
    }
    return @{}
}

function Save-State([hashtable]$State) {
    $State | ConvertTo-Json | Set-Content -Path $StateFile
}

function Parse-BranchName([string]$BaseName) {
    $parts = $BaseName -split '_'
    if ($parts.Count -lt 3) { return $null }
    return ($parts[0..($parts.Count - 3)]) -join '_'
}

function Run-Mysql([string]$Sql) {
    $env:MYSQL_PWD = $Password
    $result = echo $Sql | & $MySqlPath "--host=$MySqlHost" "--port=$Port" "--user=$Username" 2>&1
    $code = $LASTEXITCODE
    Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
    if ($code -ne 0) { throw $result }
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

# ---------------------------------------------------------------------------
# Scan and group files by branch (across all folders)
# ---------------------------------------------------------------------------

$files = @()
foreach ($folder in $GoogleDriveFolders) {
    if (-not (Test-Path $folder)) {
        Write-Log "ERROR" "Google Drive folder not found: '$folder'. Skipping."
        continue
    }
    $files += Get-ChildItem -Path $folder -Filter "*.dat" -File
}

if ($files.Count -eq 0) {
    Write-Log "INFO" "No .dat files found. Nothing to import."
    exit 0
}

$byBranch = @{}
foreach ($file in $files) {
    $branch = Parse-BranchName $file.BaseName
    if ($null -eq $branch) {
        Write-Log "INFO" "Skipping '$($file.Name)' - unexpected filename format."
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
    $dbName = $DatabasePrefix + "_" + $branch

    if ($state.ContainsKey($branch) -and $state[$branch] -eq $latest.Name) {
        Write-Log "INFO" "[$branch] Already up to date ($($latest.Name)). Skipping."
        continue
    }

    Write-Log "INFO" "[$branch] Importing '$($latest.Name)' into '$dbName'..."

    try {
        $dropSql   = "DROP DATABASE IF EXISTS " + $dbName + ";"
        $createSql = "CREATE DATABASE " + $dbName + ";"

        Run-Mysql $dropSql
        Run-Mysql $createSql

        $env:MYSQL_PWD = $Password
        Get-Content $latest.FullName -Raw | & $MySqlPath "--host=$MySqlHost" "--port=$Port" "--user=$Username" $dbName 2>&1
        $code = $LASTEXITCODE
        Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue

        if ($code -ne 0) { throw "mysql.exe exited with code $code." }

        $state[$branch] = $latest.Name
        Save-State $state
        Write-Log "INFO" "[$branch] Import complete."
    }
    catch {
        Write-Log "ERROR" "[$branch] Import failed: $_"
        $anyError = $true
    }
}

Write-Log "INFO" "Auto-import finished."
exit $(if ($anyError) { 1 } else { 0 })
