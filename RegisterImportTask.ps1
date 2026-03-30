#Requires -RunAsAdministrator

param(
    [string]$ScriptPath    = "C:\Program Files\WinUpdateHelper\AutoImport.ps1",
    [string]$RunAt         = "06:00",
    [string]$TaskUser      = $env:USERNAME,
    [string[]]$Folders     = @("H:\My Drive\UpdateCache")
)

$folderArgs = ($Folders | ForEach-Object { "`"$_`"" }) -join ","
$taskName = "WinUpdateHelper-AutoImport"
$action   = New-ScheduledTaskAction `
    -Execute "powershell.exe" `
    -Argument "-NonInteractive -ExecutionPolicy Bypass -File `"$ScriptPath`" -GoogleDriveFolders $folderArgs"

$trigger  = New-ScheduledTaskTrigger -Daily -At $RunAt

$settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Hours 1) `
    -RestartCount 2 `
    -RestartInterval (New-TimeSpan -Minutes 5)

$principal = New-ScheduledTaskPrincipal `
    -UserId $TaskUser `
    -LogonType Interactive `
    -RunLevel Highest

Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

Register-ScheduledTask `
    -TaskName  $taskName `
    -Action    $action `
    -Trigger   $trigger `
    -Settings  $settings `
    -Principal $principal | Out-Null

Write-Host "Task '$taskName' registered. Runs daily at $RunAt as $TaskUser." -ForegroundColor Green
Write-Host ""
Write-Host "  Test now:  Start-ScheduledTask -TaskName '$taskName'"
Write-Host "  View log:  Get-Content 'C:\ProgramData\WinUpdateHelper\Logs\import.log' -Tail 20"
