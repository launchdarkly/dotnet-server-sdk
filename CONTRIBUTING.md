# Contributing to the LaunchDarkly Server-side SDK for .NET

LaunchDarkly has published an [SDK contributor's guide](https://docs.launchdarkly.com/docs/sdk-contributors-guide) that provides a detailed explanation of how our SDKs work. See below for additional information on how to contribute to this SDK.

## Submitting bug reports and feature requests

The LaunchDarkly SDK team monitors the [issue tracker](https://github.com/launchdarkly/dotnet-server-sdk/issues) in the SDK repository. Bug reports and feature requests specific to this SDK should be filed in this issue tracker. The SDK team will respond to all newly filed issues within two business days.
 
## Submitting pull requests
 
We encourage pull requests and other contributions from the community. Before submitting pull requests, ensure that all temporary or unintended code is removed. Don't worry about adding reviewers to the pull request; the LaunchDarkly SDK team will add themselves. The SDK team will acknowledge all pull requests within two business days.
 
## Build instructions
 
### Prerequisites

To set up your SDK build time environment, you must [download .NET Core and follow the instructions](https://dotnet.microsoft.com/download) (make sure you have 1.0.4 or higher).
 
### Building
 
To install all required packages:

```
dotnet restore
```

Then, to build the SDK without running any tests:

```
dotnet build src/LaunchDarkly.Client -f netstandard1.4
```
 
### Testing
 
To run all unit tests:

```
dotnet test test/LaunchDarkly.Tests/LaunchDarkly.Tests.csproj
```

## Miscellaneous

This project imports the `dotnet-base` repository as a subtree. See the `README.md` file in that directory for more information.

Releases are done using the release script in `dotnet-base`. Since the published package includes a .NET Framework 4.5 build, the release must be done from Windows.