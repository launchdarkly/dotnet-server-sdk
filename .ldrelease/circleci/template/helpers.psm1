
# Helper functions used by the dotnet-windows scripts.

$ErrorActionPreference = "Stop"

# This helper function is based on https://github.com/psake/psake - it allows us to terminate the
# script if any external command (like "aws" or "dotnet", rather than a PowerShell cmdlet) returns
# an error code, which PowerShell won't otherwise do.
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

# This helper function for code-signing currently uses certutil and signtool rather than the equivalent
# PowerShell 6 commands, because CircleCI only provides PowerShell 5.
function SignFilesWithPFXCertificate {
    param(
        [Parameter(Mandatory)][string]$certFile,
        [Parameter(Mandatory)][string]$certPassword,
        [Parameter(Mandatory)][string[]]$filesToSign
    )
    # We must import the certificate before signing, because even though signtool supports .p12 files,
    # signtool won't actually look in the .p12 file to get the private key.
    $localStoreName = "CA"
    $certNameSubstring = "Catamorphic Co."
    ExecuteOrFail { certutil -f -p "$certPassword" -importpfx "$localStoreName" "$certFile" }
    ExecuteOrFail { signtool sign /s "$localStoreName" /n "$certNameSubstring" $filesToSign }
}

# The following helper functions assume that the source files are laid out like this:
# ./
#   AnySolutionName.sln
#   src/
#     AssemblyName1/
#       AssemblyName1.csproj
#     AssemblyName2/
#       AssemblyName2.csproj
#   test/
#     AnyTestProjectName/
#       AnyTestProjectName.csproj

# Returns array of hashtables with name, sourceDir, projectFilePath, buildProductsDir
function GetSourceProjects {
    param(
        [Parameter()][string]$targetFramework
    )
    return dir src | %{@{
        name = $_.Name;
        sourceDir = $_.FullName;
        projectFilePath = "$($_.FullName)\$($_.Name).csproj";
        debugProductsBase = "$($_.FullName)\bin\Debug";
        releaseProductsBase = "$($_.FullName)\bin\Release";
        buildProductsDir = $(if ($targetFramework) { "$($_.FullName)\bin\Debug\$targetFramework" } else { "" });
    }}
}
