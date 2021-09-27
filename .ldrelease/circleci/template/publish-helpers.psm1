
$ErrorActionPreference = "Stop"

# Disable PowerShell progress bars, which cause problems in CircleCI ("Access is denied ... while
# reading the console output buffer")
$ProgressPreference = "SilentlyContinue"

# Import helper functions
$scriptDir = split-path -parent $MyInvocation.MyCommand.Definition
import-module "$scriptDir\helpers.psm1" -force


# Does all of the pre-publication steps but does not push any packages. Returns an
# array of objects, one per package, where .name is the project name and .path is the
# file path.
function PreparePackagesToPublish {
    $env:Path += ";C:\Program Files (x86)\Windows Kits\10\bin\10.0.17763.0\x86"  # for signtool

    $repoDir = "${env:LD_RELEASE_PROJECT_DIR}"
    
    if ($shouldSign) {
        # Get the code signing certificate and password
        $certFile = "catamorphic_code_signing_certificate.p12"
        copy-item -path "${env:LD_RELEASE_SECRETS_DIR}/dotnet_code_signing_certificate" -destination $certFile
        $certPassword = get-content -path "${env:LD_RELEASE_SECRETS_DIR}/dotnet_code_signing_password" -raw
    }
    
    Write-Host
    Write-Host "Cleaning before release build"
    ExecuteOrFail { dotnet clean } | Out-Null
    
    $projects = GetSourceProjects
    Write-Host
    Write-Host "Projects to be built: $($projects | %{$_.name})"
    
    # If any strong-naming key files were downloaded from AWS (see update-version.sh), copy them into the project directory.
    copy-item -path "${env:LD_RELEASE_SECRETS_DIR}/*.snk" -destination $repoDir
    
    foreach ($project in $projects) {
        $name = $project.name
        Write-Host
        Write-Host "[$name]: building in Release configuration"
        ExecuteOrFail { dotnet build -c Release $project.projectFilePath }
    
        if ($shouldSign) {
            $dlls = dir -Path $project.releaseProductsBase -Filter "$name.dll" -Recurse | %{$_.FullName}
            Write-Host
            Write-Host "[$name]: signing assemblies"
            SignFilesWithPFXCertificate -certFile $certFile -certPassword $certPassword -filesToSign $dlls  # from helpers
        }
    
        Write-Host
        Write-Host "[$name]: creating package"
        del "$($project.releaseProductsBase)\*.nupkg"
        ExecuteOrFail { dotnet pack -c Release --no-build $project.projectFilePath }
    }
    
    $packagesOut = @()
    foreach ($project in $projects) {
        $name = $project.name
        $pkg = dir -Path "$($project.releaseProductsBase)\*.nupkg" | %{$_.FullName}
        $sourcePkg = dir -Path "$($project.releaseProductsBase)\*.snupkg" | %{$_.FullName}
        $packagesOut += @{
            name = $project.name;
            path = $pkg;
        }
        if ($sourcePkg) {
            $packagesOut += @{
                name = $project.name;
                path = $sourcePkg;
            }    
        }
    }
    return $packagesOut
}
