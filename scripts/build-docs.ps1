
#
# This script builds HTML documentation for the SDK using Sandcastle Help File Builder. It assumes that
# the Sandcastle software is already installed on the host. The Sandcastle GUI is not required, only the
# core tools and the SandcastleBuilderUtils package that provides the MSBuild targets.
#
# The script takes no parameters; it infers the project version by looking at the .csproj file. It starts
# by building the project in Debug configuration.
#
# Since some public APIs are provided by the LaunchDarkly.CommonSdk package, the Sandcastle project is
# configured to merge that package's documentation into this one, which requires some special file
# copying as seen below.
#

# Terminate the script if any PowerShell command fails
$ErrorActionPreference = "Stop"

# Terminate the script if any external command fails
function ExecuteOrFail {
    [CmdletBinding()]
    param(
        [Parameter(Position=0,Mandatory=1)][scriptblock]$cmd,
        [Parameter(Position=1,Mandatory=0)][string]$errorMessage = ("Error executing command {0}" -f $cmd)
    )
    & $cmd
    if ($lastexitcode -ne 0) {
        throw ($errorMessage)
    }
}

ExecuteOrFail { dotnet clean }
ExecuteOrFail { dotnet build src\LaunchDarkly.ServerSdk\LaunchDarkly.ServerSdk.csproj -f net45 }

# Building the SDK causes the assemblies for all its package dependencies to be copied into bin\Debug\net45.
# The .shfbproj is configured to expect them to be there. However, we also need the XML documentation file
# for LaunchDarkly.CommonSdk, which isn't automatically copied. We can get it out of the NuGet package
# cache, but first we need to determine what version of it we're using.
$match = Select-String `
    -Path src\LaunchDarkly.ServerSdk\LaunchDarkly.ServerSdk.csproj `
    -Pattern "<PackageReference.*""LaunchDarkly.CommonSdk"".*""([^""]*)"""
if ($match.Matches.Length -ne 1) {
    throw "Could not find LaunchDarkly.CommonSdk version in project file"
}
$commonSdkVersion = $match.Matches[0].Groups[1].Value
Copy-Item `
    -Path $HOME\.nuget\packages\launchdarkly.commonsdk\$commonSdkVersion\lib\net45\LaunchDarkly.CommonSdk.xml `
    -Destination src\LaunchDarkly.ServerSdk\bin\Debug\net45

if (Test-Path docs\build) {
    Remove-Item -Path docs\build -Recurse -Force
}

$match = Select-String `
    -Path src\LaunchDarkly.ServerSdk\LaunchDarkly.ServerSdk.csproj `
    -Pattern "<Version>([^<]*)</Version>"
if ($match.Matches.Length -ne 1) {
    throw "Could not find SDK version string in project file"
}
$sdkVersion = $match.Matches[0].Groups[1].Value

[System.Environment]::SetEnvironmentVariable("LD_RELEASE_VERSION", $sdkVersion, "Process")

try
{
    Push-Location
    Set-Location docs
    ExecuteOrFail { msbuild project.shfbproj }
}
finally
{
    Pop-Location
}

# Add our own stylesheet overrides. You're supposed to be able to put customized stylesheets in
# ./styles (relative to the project file) and have them be automatically copied in, but that
# doesn't seem to work, so we'll just modify the CSS file after building.
Get-Content docs\launchdarkly.css | Add-Content docs\build\html\styles\branding-Website.css
