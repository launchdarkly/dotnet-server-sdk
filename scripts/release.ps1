#
# Simple PowerShell script for building and uploading a NuGet package from dotnet-client.
#
# The LaunchDarkly.Client will be built and uploaded, in the Release configuration (after first
# building and testing in Debug configuration to make sure it works).
#
# Before you run this script, make sure:
# 1. you have set the correct project version in LaunchDarkly.Client.csproj
# 2. you have downloaded the key file, LaunchDarkly.Client.snk, in the project root directory
#

dotnet clean
dotnet build -c Debug
dotnet test -c Debug test/LaunchDarkly.Tests/LaunchDarkly.Tests.csproj
dotnet build -c Release
del src\LaunchDarkly.Client\bin\Release\*.nupkg
dotnet pack -c Release
dotnet nuget push src\LaunchDarkly.Client\bin\Release\*.nupkg -s https://www.nuget.org
