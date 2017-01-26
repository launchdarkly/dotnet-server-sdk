Contributing
------------

We encourage pull-requests and other contributions from the community. We've also published an [SDK contributor's guide](http://docs.launchdarkly.com/v1.0/docs/sdk-contributors-guide) that provides a detailed explanation of how our SDKs work.


Getting Started
-----------------

Mac OS:

1. [Download .net core and follow instructions](https://www.microsoft.com/net/core#macos)
1. Run ```brew install mono` to install the Mono for .NET Framework 4.5.
1. Add `export DOTNET_REFERENCE_ASSEMBLIES_PATH="/usr/local/lib/mono/xbuild-frameworks/"` to your profile so that `dotnet` can find the .NET Framework 4.5 assemblies.
1. Run ```dotnet restore``` to pull in required packages
1. Make sure you can build and run tests from command line:

```
dotnet build src/LaunchDarkly.Client 
dotnet test test/LaunchDarkly.Tests
```

To package for local use:
1. Bump version here in package.json and in dependency declaration in app
1. `dotnet pack src/LaunchDarkly.Client`
1. Restore your app using the output directory of the previous command:
```
dotnet restore -s [.net-client repo root]/src/LaunchDarkly.Client/bin/Debug/
```