
# See: https://github.com/launchdarkly/project-releaser/blob/master/docs/templates/dotnet-windows.md

# Terminate the script if any PowerShell command fails
$ErrorActionPreference = "Stop"

# Disable PowerShell progress bars, which cause problems in CircleCI ("Access is denied ... while
# reading the console output buffer")
$ProgressPreference = "SilentlyContinue"

$scriptDir = split-path -parent $MyInvocation.MyCommand.Definition
Import-Module "$scriptDir\publish-helpers.psm1" -Force

$packages = PreparePackagesToPublish

$nugetApiKey = get-content -path "${env:LD_RELEASE_SECRETS_DIR}/dotnet_nuget_api_key" -raw

foreach ($package in $packages) {
    $name = $package.name
    $pkg = $package.path

    Write-Host "[$name]: publishing $pkg"
    ExecuteOrFail { dotnet nuget push $pkg --source "https://www.nuget.org" --api-key "$nugetApiKey" }
    Write-Host "[$name]: published $pkg"
}
