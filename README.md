# LaunchDarkly Server-Side SDK for .NET

[![CircleCI](https://circleci.com/gh/launchdarkly/dotnet-server-sdk/tree/master.svg?style=svg)](https://circleci.com/gh/launchdarkly/dotnet-server-sdk/tree/master)

## LaunchDarkly overview
[LaunchDarkly](https://www.launchdarkly.com) is a feature management platform that serves over 100 billion feature flags daily to help teams build better software, faster. [Get started](https://docs.launchdarkly.com/docs/getting-started) using LaunchDarkly today!
 
[![Twitter Follow](https://img.shields.io/twitter/follow/launchdarkly.svg?style=social&label=Follow&maxAge=2592000)](https://twitter.com/intent/follow?screen_name=launchdarkly)

## Supported .NET versions

This version of the LaunchDarkly SDK is compatible with .NET Framework version 4.5.2 and above, or .NET Standard 2.0 (.NET Core 2.1 and above).

## Getting started

Refer to the [SDK documentation](https://docs.launchdarkly.com/docs/dotnet-sdk-reference#section-getting-started) for instructions on getting started with using the SDK.

## Signing

The published version of the `LaunchDarkly.Client` assembly is digitally signed with Authenticode and [strong-named](https://docs.microsoft.com/en-us/dotnet/framework/app-domains/strong-named-assemblies). The public key file is in this repository at `LaunchDarkly.pk` as well as here:

```
Public Key:
0024000004800000940000000602000000240000525341310004000001000100f121bbf427e4d7
edc64131a9efeefd20978dc58c285aa6f548a4282fc6d871fbebeacc13160e88566f427497b625
56bf7ff01017b0f7c9de36869cc681b236bc0df0c85927ac8a439ecb7a6a07ae4111034e03042c
4b1569ebc6d3ed945878cca97e1592f864ba7cc81a56b8668a6d7bbe6e44c1279db088b0fdcc35
52f746b4

Public Key Token: f86add69004e6885
```

Building the code locally in the default Debug configuration does not sign the assembly and does not require a key file.

## Learn more

Check out our [documentation](https://docs.launchdarkly.com) for in-depth instructions on configuring and using LaunchDarkly. You can also head straight to the [complete reference guide for this SDK](https://docs.launchdarkly.com/docs/dotnet-sdk-reference).

The authoritative description of all types, properties, and methods is in the [generated API documentation](https://launchdarkly.github.io/dotnet-server-sdk/).

## Testing
 
We run integration tests for all our SDKs using a centralized test harness. This approach gives us the ability to test for consistency across SDKs, as well as test networking behavior in a long-running application. These tests cover each method in the SDK, and verify that event sending, flag evaluation, stream reconnection, and other aspects of the SDK all behave correctly.
 
## Contributing
 
We encourage pull requests and other contributions from the community. Check out our [contributing guidelines](CONTRIBUTING.md) for instructions on how to contribute to this SDK.

## About LaunchDarkly
 
* LaunchDarkly is a continuous delivery platform that provides feature flags as a service and allows developers to iterate quickly and safely. We allow you to easily flag your features and manage them from the LaunchDarkly dashboard.  With LaunchDarkly, you can:
    * Roll out a new feature to a subset of your users (like a group of users who opt-in to a beta tester group), gathering feedback and bug reports from real-world use cases.
    * Gradually roll out a feature to an increasing percentage of users, and track the effect that the feature has on key metrics (for instance, how likely is a user to complete a purchase if they have feature A versus feature B?).
    * Turn off a feature that you realize is causing performance problems in production, without needing to re-deploy, or even restart the application with a changed configuration file.
    * Grant access to certain features based on user attributes, like payment plan (eg: users on the ‘gold’ plan get access to more features than users in the ‘silver’ plan). Disable parts of your application to facilitate maintenance, without taking everything offline.
* LaunchDarkly provides feature flag SDKs for a wide variety of languages and technologies. Check out [our documentation](https://docs.launchdarkly.com/docs) for a complete list.
* Explore LaunchDarkly
    * [launchdarkly.com](https://www.launchdarkly.com/ "LaunchDarkly Main Website") for more information
    * [docs.launchdarkly.com](https://docs.launchdarkly.com/  "LaunchDarkly Documentation") for our documentation and SDK reference guides
    * [apidocs.launchdarkly.com](https://apidocs.launchdarkly.com/  "LaunchDarkly API Documentation") for our API documentation
    * [blog.launchdarkly.com](https://blog.launchdarkly.com/  "LaunchDarkly Blog Documentation") for the latest product updates
    * [Feature Flagging Guide](https://github.com/launchdarkly/featureflags/  "Feature Flagging Guide") for best practices and strategies