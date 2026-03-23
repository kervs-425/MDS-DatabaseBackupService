<#
.SYNOPSIS
    Restores a .sql dump file into a MySQL database.

.EXAMPLE
    .\Restore.ps1 -DumpFile "G:\My Drive\BranchBackups\branch1_20260323_220000.sql" -Database "mdsbillingdbv5"

.EXAMPLE
    .\Restore.ps1 -DumpFile "C:\backup.sql" -Database "mdsbillingdbv5" -Host "localhost" -Port 3306 -User "root"
#>

param(
    [Parameter(Mandatory)]
    [string]$DumpFile,

    [Parameter(Mandatory)]
    [string]$Database,

    [string]$Host = "localhost",
    [int]$Port = 3306,
    [string]$User = "root",
    [string]$MysqlPath = ""
)

# Auto-detect mysql.exe
if (-not $MysqlPath) {
    $candidates = @(
        "C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe",
        "C:\Program Files\MySQL\MySQL Workbench 8.0 CE\mysql.exe",
        "C:\Program Files (x86)\MySQL\MySQL Server 5.7\bin\mysql.exe"
    )
    $MysqlPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $MysqlPath) {
        Write-Host "mysql.exe not found. Specify -MysqlPath." -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-Path $DumpFile)) {
    Write-Host "Dump file not found: $DumpFile" -ForegroundColor Red
    exit 1
}

$fileSize = [math]::Round((Get-Item $DumpFile).Length / 1MB, 2)
Write-Host "Restoring '$DumpFile' ($fileSize MB) into database '$Database' on $Host`:$Port" -ForegroundColor Cyan
Write-Host ""

$password = Read-Host "Enter MySQL password for '$User'" -AsSecureString
$plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))

$env:MYSQL_PWD = $plainPassword

Write-Host "Restoring... this may take a while." -ForegroundColor Yellow
& $MysqlPath --host=$Host --port=$Port --user=$User $Database -e "source $DumpFile" 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Restore completed successfully." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Restore failed." -ForegroundColor Red
}

$env:MYSQL_PWD = ""
