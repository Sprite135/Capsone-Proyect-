$env:DOTNET_CLI_HOME = "$PSScriptRoot\.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:APPDATA = "$PSScriptRoot\.appdata"

$globalPackages = Join-Path $env:USERPROFILE ".nuget\packages"
if (Test-Path $globalPackages) {
    $env:NUGET_PACKAGES = $globalPackages
}

dotnet run --project "$PSScriptRoot\LicitIA.Api"
