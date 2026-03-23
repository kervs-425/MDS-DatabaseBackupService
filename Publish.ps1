param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$project = Join-Path $PSScriptRoot "MDS.DatabaseBackupService.csproj"

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true

Write-Host ""
Write-Host "Published to: $(Join-Path $PSScriptRoot "bin\$Configuration\net8.0\$Runtime\publish")"
