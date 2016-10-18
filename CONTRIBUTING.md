Contributing
------------

We encourage pull-requests and other contributions from the community. We've also published an [SDK contributor's guide](http://docs.launchdarkly.com/v1.0/docs/sdk-contributors-guide) that provides a detailed explanation of how our SDKs work.


Getting Started
-----------------

Mac OS:

1. [Download .net core and follow instructions](https://www.microsoft.com/net/core#macos)

1. Run ```dotnet restore``` to pull in required packages
1. Make sure you can build and run tests from command line:

```
dotnet build src/LaunchDarkly.Client 
dotnet test test/LaunchDarkly.Tests
```
