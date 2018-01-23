Contributing
------------

We encourage pull-requests and other contributions from the community. We've also published an [SDK contributor's guide](http://docs.launchdarkly.com/docs/sdk-contributors-guide) that provides a detailed explanation of how our SDKs work.


Getting Started
-----------------

Mac OS:

1. [Download .net core and follow instructions](https://www.microsoft.com/net/core#macos) (make sure you have 1.0.4 or higher)
1. Building for the net45 target doesn't currently work with the current set of tooling... you'll have to build the nuget artifact in windows. ~~Install Mono 5.0 from [here](http://www.mono-project.com/download/)~~
1. ~~Add `export FrameworkPathOverride=/Library/Frameworks/Mono.framework/Versions/5.0.0/lib/mono/4.5`(or similar depending on the mono version that was installed) to your profile so that `dotnet` can find the .NET Framework 4.5 assemblies. [Source](https://github.com/dotnet/netcorecli-fsc/wiki/.NET-Core-SDK-rc4#using-net-framework-as-targets-framework-the-osxunix-build-fails)~~
1. Run ```dotnet restore``` to pull in required packages
1. Make sure you can build and run tests from command line:

```
dotnet build src/LaunchDarkly.Client -f netstandard1.4
dotnet test test/LaunchDarkly.Tests/LaunchDarkly.Tests.csproj
```

To package for local use:
1. Adjust VersionPrefix and VersionSuffix elements in `/src/LaunchDarkly.Client/LaunchDarkly.Client.csproj` and in dependency declaration in your local app
1. `dotnet pack src/LaunchDarkly.Client`
1. Restore your app using the output directory of the previous command:
```
dotnet restore -s [.net-client repo root]/src/LaunchDarkly.Client/bin/Debug/
```