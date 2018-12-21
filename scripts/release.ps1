param([string]$password = $(throw "-password is required"))

$assemblyNames = "LaunchDarkly.Client"
$testProject = "LaunchDarkly.Tests"

#
# Simple PowerShell script for building and uploading a NuGet package from dotnet-client.
#
# The LaunchDarkly.Client assembly will be built and uploaded, in the Release configuration
# (after first building and testing in Debug configuration to make sure it works).
#
# Before you run this script, make sure:
# 1. you have set the correct project version in LaunchDarkly.Client.csproj
# 2. you have downloaded the key file, LaunchDarkly.Client.snk, in the project root directory
# 3. you have downloaded the certificate, catamorphic_code_signing_certificate.p12, in the
#    project root directory
#

# This helper function comes from https://github.com/psake/psake - it allows us to terminate the
# script if any executable returns an error code, which PowerShell won't otherwise do
function Exec
{
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

$certFile = "catamorphic_code_signing_certificate.p12"

if (-not (Test-Path $certFile)) {
    throw ("Certificate file $certfile must be in the current directory. Download it from s3://launchdarkly-pastebin/ci/dotnet/")
}

$env:Path += ";C:\Program Files (x86)\Microsoft SDKs\Windows\v7.1A\Bin"

Exec { dotnet clean }

Exec { dotnet build -c Debug }
Exec { dotnet test -c Debug test\$testProject }

foreach ($assemblyName in $assemblyNames) {
	Exec { dotnet build -c Release "src\$assemblyName" }
    $dlls = dir -Path "src\$assemblyName\bin\Release" -Filter "$assemblyName.dll" -Recurse | %{$_.FullName}
    Exec { signtool sign /f $certFile /p $password $dlls }

    del "src\$assemblyName\bin\Release\*.nupkg"
    Exec { dotnet pack -c Release "src\$assemblyName" }
}

foreach ($assemblyName in $assemblyNames) {
    dotnet nuget push "src\$assemblyName\bin\Release\*.nupkg" -s https://www.nuget.org
}
