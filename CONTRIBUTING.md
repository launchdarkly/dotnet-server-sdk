# Contributing to the LaunchDarkly Server-Side SDK for .NET

LaunchDarkly has published an [SDK contributor's guide](https://docs.launchdarkly.com/sdk/concepts/contributors-guide) that provides a detailed explanation of how our SDKs work. See below for additional information on how to contribute to this SDK.

## Submitting bug reports and feature requests

The LaunchDarkly SDK team monitors the [issue tracker](https://github.com/launchdarkly/dotnet-server-sdk/issues) in the SDK repository. Bug reports and feature requests specific to this SDK should be filed in this issue tracker. The SDK team will respond to all newly filed issues within two business days.
 
## Submitting pull requests
 
We encourage pull requests and other contributions from the community. Before submitting pull requests, ensure that all temporary or unintended code is removed. Don't worry about adding reviewers to the pull request; the LaunchDarkly SDK team will add themselves. The SDK team will acknowledge all pull requests within two business days.

## Build instructions
 
### Prerequisites

To set up your SDK build time environment, you must [download .NET development tools and follow the instructions](https://dotnet.microsoft.com/download). .NET 5.0 is preferred, since the .NET 5.0 tools are able to build for all supported target platforms.

This SDK shares part of its implementation and public API with the [Xamarin SDK](https://github.com/launchdarkly/xamarin-client-sdk). The shared code is in two other packages:

* `LaunchDarkly.CommonSdk` (in [`launchdarkly/dotnet-sdk-common`](https://github.com/launchdarkly/dotnet-sdk-common): Types such as `User` that are part of the SDK's public API, but are not specific to server-side or client-side use.
* `LaunchDarkly.InternalSdk` (in [`launchdarkly/dotnet-sdk-internal`](https://github.com/launchdarkly/dotnet-sdk-internal): Support code that is not part of the SDK's public API, such as the implementation of analytics event processing. These types are public in order to be usable from outside of their assembly, but they are not included in the SDK's public API or documentation.

Other support code is in the packages [`LaunchDarkly.EventSource`](https://github.com/launchdarkly/dotnet-eventsource) and [`LaunchDarkly.Logging`](https://github.com/launchdarkly/dotnet-logging).

### Building
 
To install all required packages:

```bash
dotnet restore
```

Then, to build the SDK for all target frameworks:

```bash
dotnet build src/LaunchDarkly.ServerSdk
```

Or, in Linux:

```bash
make
```

Or, to build for only one target framework (in this example, .NET Standard 2.0):

```bash
dotnet build src/LaunchDarkly.ServerSdk -f netstandard2.0
```

### Testing
 
To run all unit tests:

```bash
dotnet test test/LaunchDarkly.ServerSdk.Tests/LaunchDarkly.ServerSdk.Tests.csproj
```

Or, in Linux:

```bash
make test
```

Note that the unit tests can only be run in Debug configuration. There is an `InternalsVisibleTo` directive that allows the test code to access internal members of the library, and assembly strong-naming in the Release configuration interferes with this.

To run the SDK contract test suite in Linux (see [`contract-tests/README.md`](./contract-tests/README.md)):

```bash
make contract-tests
```

## Documentation in code

All public types, methods, and properties should have documentation comments in the standard C# XML comment format. These will be automatically included in the [HTML documentation](https://launchdarkly.github.io/dotnet-server-sdk) that is generated on release; this process also uses additional Markdown content from the `docs-src/` subdirectory.

See [`docs-src/README.md`](./docs-src/README.md) for more details.
