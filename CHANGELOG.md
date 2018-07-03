# Change log

All notable changes to the LaunchDarkly .NET SDK will be documented in this file. This project adheres to [Semantic Versioning](http://semver.org).

## [5.1.1] - 2018-07-02
### Changed:
- When targeting the .NET 4.5 framework, the dependency on Newtonsoft's JSON.Net framework has been changed: the minimum version is now 6.0.1 rather than 9.0.1. This was changed in order to support customer code that uses older versions of JSON.Net. For most applications, this change should have no effect since it is only a _minimum_ version, which can be overridden by any higher version specified in your own dependencies. Note that when targeting .NET Standard, the minimum JSON.Net version is still 9.0.1 because earlier versions were not compatible with .NET Standard.
- The `Identify` method has been moved back into `ILdClient` rather than being in `ILdCommonClient`.

## [5.1.0] - 2018-06-26
### Added:
- A new overload of `LDClient.Track` allows you to pass any kind of JSON value for the custom event data, not just a string.
- The `User` class now implements `Equals` and `GetHashCode`, and has a copy constructor.

### Changed:
- Some classes and interfaces have been moved into a separate assembly, `LaunchDarkly.Common` (source code [here](https://github.com/launchdarkly/dotnet-client-common/)), because they will also be used by the LaunchDarkly Xamarin SDK. The names and namespaces have not changed, so you do not need to make any code changes. `LaunchDarkly.Common` will be installed automatically when you upgrade `LaunchDarkly.Client`; all other dependencies are unchanged.
- The client now treats most HTTP 4xx errors as unrecoverable: that is, after receiving such an error, it will not make any more HTTP requests for the lifetime of the client instance, in effect taking the client offline. This is because such errors indicate either a configuration problem (invalid SDK key) or a bug, which is not likely to resolve without a restart or an upgrade. This does not apply if the error is 400, 408, 429, or any 5xx error.
- During initialization, if the client receives any of the unrecoverable errors described above, the client constructor will return immediately; previously it would continue waiting until a timeout. The `Initialized()` method will return false in this case.

### Fixed:
- Ensured that all `HttpClient` instances managed by the client are disposed of immediately if you call `Dispose` on the client.
- Passing `null` for user when calling `Identify` or `Track` no longer causes a `NullReferenceException`. Instead, the appropriate event will be sent with no user.

## [5.0.0] - 2018-05-10

### Changed:
- To reduce the network bandwidth used for analytics events, feature request events are now sent as counters rather than individual events, and user details are now sent only at intervals rather than in each event. These behaviors can be modified through the LaunchDarkly UI and with the new configuration option `InlineUsersInEvents`. For more details, see [Analytics Data Stream Reference](https://docs.launchdarkly.com/v2.0/docs/analytics-data-stream-reference).
- The `IStoreEvents` interface has been renamed to `IEventProcessor`, has slightly different methods, and includes `IDisposable`. Also, the properties of the `Event` classes have changed. This will only affect developers who created their own implementation of `IStoreEvents`.

### Added:
- New extension methods on `Configuration` (`WithUpdateProcessorFactory`, `WithFeatureStoreFactory`, `WithEventProcessorFactory`) allow you to specify different implementations of each of the main client subcomponents (receiving feature state, storing feature state, and sending analytics events) for testing or for any other purpose. The `Components` class provides factories for all built-in implementations of these.

### Deprecated:
- The `WithFeatureStore` configuration method is deprecated, replaced by the new factory-based mechanism described above.
- The `LdClient` constructor overload that takes an `IEventProcessor` (formerly `IStoreEvents`) is deprecated, replaced by `WithEventProcessorFactory`.

## [4.1.1] - 2018-03-23
### Fixed
- Fixed a [bug](https://github.com/launchdarkly/.net-client/issues/75) in the event sampling feature that was introduced in 4.1.0: sampling might not work correctly if events were generated from multiple threads.

## [4.1.0] - 2018-03-05
### Added
- `Configuration` now has an `EventSamplingInterval` property. If greater than zero, this causes a fraction of analytics events to be sent to LaunchDarkly: one per that number of events (pseudo-randomly). For instance, setting it to 5 would cause 20% of events to be sent on average.
### Changed
- `ConfigurationExtensions.WithPollingInterval` will no longer throw an exception if the parameter is lower than the minimum. Instead, it will simply set the value to the minimum and log a warning.

## [4.0.0] - 2018-02-21
### Added
- Support for a new LaunchDarkly feature: reusable user segments.

### Changed
- The client now uses [Common.Logging](https://net-commons.github.io/common-logging/).
- The `FeatureStore` interface has been changed to support user segment data as well as feature flags. Existing code that uses `InMemoryFeatureStore` or `RedisFeatureStore` should work as before, but custom feature store implementations will need to be updated.
- Some previously public classes that were not meant to be public are now internal.

### Fixed
- All previously undocumented methods now have documentation comments.

### Removed
- Obsolete/deprecated methods have been removed.
- Removed `Configuration.WithLoggerFactory` since the logging framework has changed. For more details on setting up logging, see [here](https://docs.launchdarkly.com/docs/dotnet-sdk-reference#section-logging).

## [3.6.1] - 2018-02-21
### Fixed
- Improved performance of the semantic version operators by precompiling a regex.

## [3.6.0] - 2018-02-19
### Added
- New property `LdClient.Version` returns the client's current version number.
- Adds support for a future LaunchDarkly feature, coming soon: semantic version user attributes.
- Custom attributes can now have long integer values.

### Changed
- It is now possible to compute rollouts based on an integer attribute of a user, not just a string attribute.

## [3.5.0] - 2018-01-29
### Added
- Support for specifying [private user attributes](https://docs.launchdarkly.com/docs/private-user-attributes) in order to prevent user attributes from being sent in analytics events back to LaunchDarkly. See the `AllAttributesPrivate` and `PrivateAttributeNames` methods on `Configuration` as well as the `AndPrivateX` methods on `User`.

### Changed
- The stream connection will now restart when a large feature flag update fails repeatedly to ensure that the client is using most recent flag values.
- Client no longer reconnects after detecting an invalidated SDK key.

## [3.4.1] - 2018-01-19
### Added
- Framework target for netstandard1.4 and netstandard2.0. Thanks @nolanblew and @ISkomorokh!
- Added the Apache 2.0 license to `LaunchDarkly.Client.csproj`

### Changed
- Fixed a bug causing ASP.NET applications to be blocked during client initialization.
- Removed unused and transitive dependencies.
- Improved logging. Thanks @MorganVergara and @JeffAshton!

## [3.4.0] - 2017-11-29
### Added
- :rocket: Support for Streaming via [Server-Sent Events](http://html5doctor.com/server-sent-events/) as an alternative to Polling. [HTTP-based streaming](https://launchdarkly.com/performance.html) is favored over polling to reduce network traffic and propagate feature flag updates faster. :rocket:
- New builder parameters to complement streaming functionality
  - `WithIsStreamingEnabled`: Set whether streaming mode should be enabled, `true` by default.
  - `WithStreamUri`: Set the base URL of the LaunchDarkly streaming server. May be used in conjunction with the [LaunchDarkly Relay Proxy](https://github.com/launchdarkly/ld-relay).
  - `WithReadTimeout`: The timeout when reading data from the streaming API. Defaults to 5 minutes
  - `WithReconnectTime`: The time to wait before attempting to reconnect to the streaming API. Defaults to 1 second
- Apache 2.0 License

### Changed
- Streaming is now used to retrieve feature flag configurations by default.
- Minimum (and default) polling interval changed from 1 second to 30 seconds.
- `PollingProcessor` no longer retries failed feature flag polling attempts.

## [3.3.2] - 2017-08-30
### Changed
- Updated dependency versions. Thanks @ISkomorokh!
- Exceptions in `FeatureRequestor` are rethrown without replacing stack information

## [3.3.1] - 2017-07-14
### Fixed
- `UserExtensions.AndName` updates `user.Name` instead of `user.LastName`

## [3.3.0] - 2017-06-16
### Added
- Config option to use custom implementation of IFeatureStore
- Artifact is now signed
### Changed
- Removed NETStandard.Library from dependencies so it isn't brought in by non-.NET core projects.
- Project files migrated to current `*.csproj` standard
- Fixed release that inadvertently removed the ability to set a custom HttpClientHandler

## [3.2.0] - 2017-05-25
### Added
- Config option to use custom implementation of IFeatureStore
- Artifact is now signed
### Changed
- Removed NETStandard.Library from dependencies so it isn't brought in by non-.NET core projects.
- Project files migrated to current `*.csproj` standard

## [3.1.1] - 2017-01-16
### Changed
- Improved error handling when sending events

## [3.1.0] - 2016-12-07
### Added
- Configurable http request timeout

### Changed
- Made http requests more resilient and logging more informative.

## [3.0.0] - 2016-10-27
### Changed
- Addresses Unnecesary Lock contention when polling: https://github.com/launchdarkly/.net-client/issues/18
- Logging framework: Now using Microsoft.Extensions.Logging
- No longer depending on ASP.NET: https://github.com/launchdarkly/.net-client/issues/8

### Added
- Support for .NET core: https://github.com/launchdarkly/.net-client/issues/9
- Http client now has a request timeout: https://github.com/launchdarkly/.net-client/issues/27

### Deprecated
- File-based configuration override option has been removed

## [2.0.3] - 2016-10-10
### Changed
- Code cleanup

## [2.0.2] - 2016-10-06
### Added
- Address https://github.com/launchdarkly/.net-client/issues/27
- Improve error logging- we're now logging messages from inner exceptions.

## [2.0.1] - 2016-10-05
### Changed
- Async http client code improvements.

## [2.0.0] - 2016-08-10
### Added
- Support for multivariate feature flags. New methods `StringVariation`, `JsonVariation` and `IntVariation` and `FloatVariation` for multivariates.
- New `AllFlags` method returns all flag values for a specified user.
- New `SecureModeHash` function computes a hash suitable for the new LaunchDarkly [JavaScript client's secure mode feature](https://github.com/launchdarkly/js-client#secure-mode).

### Changed
- LdClient now implements a new interface: ILdClient

### Deprecated
- The `Toggle` call has been deprecated in favor of `BoolVariation`.
