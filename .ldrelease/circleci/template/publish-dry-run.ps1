
# See: https://github.com/launchdarkly/project-releaser/blob/master/docs/templates/dotnet-windows.md

# Terminate the script if any PowerShell command fails
$ErrorActionPreference = "Stop"

# Disable PowerShell progress bars, which cause problems in CircleCI ("Access is denied ... while
# reading the console output buffer")
$ProgressPreference = "SilentlyContinue"

$scriptDir = split-path -parent $MyInvocation.MyCommand.Definition
Import-Module "$scriptDir\publish-helpers.psm1" -Force

$packages = PreparePackagesToPublish

foreach ($package in $packages) {
    $name = $package.name
    $pkg = $package.path

    Write-Host "DRY RUN: not publishing, only copying package: $pkg"
    copy-item -path $pkg -destination $env:LD_RELEASE_ARTIFACTS_DIR
}
