
#
# This script builds HTML documentation for the SDK using Sandcastle Help File Builder. It assumes that
# the Sandcastle software is already installed on the host. The Sandcastle GUI is not required, only the
# core tools and the SandcastleBuilderUtils package that provides the MSBuild targets.
#
# The script assumes that the SDK has already been built for the net45 target in Debug configuration.
# It takes a single parameter: the release version.
#
# Since some public APIs are provided by the LaunchDarkly.CommonSdk package, the Sandcastle project is
# configured to merge that package's documentation into this one, which requires some special file
# copying as seen below.
#

param(
    [Parameter(Mandatory=1)][string]$version
)

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

#ExecuteOrFail { dotnet build src\LaunchDarkly.ServerSdk\LaunchDarkly.ServerSdk.csproj -f net45 }

# Building the SDK causes the assemblies for all its package dependencies to be copied into bin\Debug\net45.
# The .shfbproj is configured to expect them to be there. However, we also need the XML documentation file
# for LaunchDarkly.CommonSdk, which isn't automatically copied. We can get it out of the NuGet package
# cache, but first we need to determine what version of it we're using.
$match = Select-String `
    -Path src\LaunchDarkly.ServerSdk\LaunchDarkly.ServerSdk.csproj `
    -Pattern "<PackageReference.*""LaunchDarkly.CommonSdk"".*""([^""]*)"""
if ($match.Matches.Length -eq 0) {
    throw "Could not find LaunchDarkly.CommonSdk version in project file"
}
$commonSdkVersion = $match.Matches[0].Groups[1].Value
Copy-Item `
    -Path $HOME\.nuget\packages\launchdarkly.commonsdk\$commonSdkVersion\lib\net45\LaunchDarkly.CommonSdk.xml `
    -Destination src\LaunchDarkly.ServerSdk\bin\Debug\net45

if (Test-Path docs\build) {
    Remove-Item -Path docs\build -Recurse -Force
}

[System.Environment]::SetEnvironmentVariable("LD_RELEASE_VERSION", $version, "Process")

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
