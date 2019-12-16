# Change log

All notable changes to the LaunchDarkly .NET Server-Side SDK will be documented in this file. This project adheres to [Semantic Versioning](http://semver.org).

## [5.11.0] - 2019-12-13
### Added:
- With `FileDataSourceFactory`, it is now possible to specify that duplicate flag keys in data files should be ignored rather than causing an error; in this mode, it will use only the first occurrence of each flag key. This allows, for instance, implementing rolling updates of flag data by putting the newest data in a file that is specified first in your file list. (Thanks, [JeffAshton](https://github.com/launchdarkly/dotnet-server-sdk/pull/123)!)

### Fixed:
- In rare circumstances (depending on the exact data in the flag configuration, the flag's salt value, and the user properties), a percentage rollout could fail and return a default value, logging the error "Data inconsistency in feature flag ... variation/rollout object with no variation or rollout". This would happen if the user's hashed value fell exactly at the end of the last "bucket" (the last variation defined in the rollout). This has been fixed so that the user will get the last variation.

## [5.10.0] - 2019-11-12
### Added:
- Added `ILdClient` extension methods `EnumVariation` and `EnumVariationDetail`, which convert strings to enums.
- Added `LaunchDarkly.Logging.ConsoleAdapter` as a convenience for quickly enabling console logging; this is equivalent to `Common.Logging.Simple.ConsoleOutLoggerFactoryAdapter`, but the latter is not available on some platforms.
- `LdValue` helpers for dealing with array/object values, without having to use an intermediate `List` or `Dictionary`: `BuildArray`, `BuildObject`, `Count`, `Get`.
- `LdValue.Parse()`. It is also possible to use `Newtonsoft.Json.JsonConvert` to parse or serialize `LdValue`, but since the implementation may change in the future, using the type's own methods is preferable.

### Changed:
- `EvaluationReason` properties all exist on the base class now, so for instance you do not need to cast to `RuleMatch` to get the `RuleId` property. This is in preparation for a future API change in which `EvaluationReason` will become a struct instead of a base class.

### Fixed:
- Improved memory usage and performance when processing analytics events: the SDK now encodes event data to JSON directly, instead of creating intermediate objects and serializing them via reflection.
- `LdValue.Equals()` incorrectly returned true for object (dictionary) values that were not equal.

### Deprecated:
- `EvaluationReason` subclasses. Use only the base class properties and methods to ensure compatibility with future versions.

## [5.9.0] - 2019-10-07
### Added:
- `IUserBuilder.AnonymousOptional` allows setting the `Anonymous` property to `null` (necessary for consistency with other SDKs). See note about this under Fixed.
- `FileDataSourceBuilder.WithSkipMissingPaths` allows suppressing file-not-found errors in `FileDataSource`, if you have a test setup that may add or remove data files dynamically. (Thanks, [JeffAshton](https://github.com/launchdarkly/dotnet-server-sdk/pull/114)!)
 
### Changed:
- It is now possible to specify an infinite cache TTL for persistent feature stores by setting the TTL to a negative number, in which case the persistent store will never be read unless the application restarts. Use this mode with caution as described in the comment for `FeatureStoreCacheConfig.Ttl`.
- Improved the performance of `InMemoryFeatureStore` by using an `ImmutableDictionary` that is replaced under a lock whenever there is an update, so reads do not need a lock. (Thanks, [JeffAshton](https://github.com/launchdarkly/dotnet-server-sdk/pull/111)!)
- The SDK now has a dependency on `System.Collections.Immutable`. It refers to version 1.2.0 because the SDK does not use any APIs that were added or changed after that point, but if you want to use that package yourself it is best to declare your own dependency rather than relying on this transitive dependency, since there may have been fixes or improvements in other APIs.
 
### Fixed:
- `IUserBuilder` was incorrectly setting the user's `Anonymous` property to `null` even if it had been explicitly set to `false`. Null and false behave the same in terms of LaunchDarkly's user indexing behavior, but currently it is possible to create a feature flag rule that treats them differently. So `IUserBuilder.Anonymous(false)` now correctly sets it to `false`, just as the deprecated method `UserExtensions.WithAnonymous(false)` would.
- `LdValue.Convert.Long` was mistakenly converting to an `int` rather than a `long`. (CommonSdk [#32](https://github.com/launchdarkly/dotnet-sdk-common/issues/32))
- `FileDataSource` could fail to read a file if it noticed the file being modified by another process before the other process had finished writing it. This fix only affects Windows, since in Windows it is not possible to replace a file's contents atomically; in a Unix-like OS, the preferred approach is to create a temporary file and rename it to replace the original file. (Thanks, [JeffAshton](https://github.com/launchdarkly/dotnet-server-sdk/pull/108/files)!)

## [5.8.0] - 2019-10-01
### Added:
- Added support for upcoming LaunchDarkly experimentation features. See `ILdClient.Track(string, User, LdValue, double)`.
 
### Fixed:
- Fixed a bug due to incorrect use of a lock that could cause a read from `InMemoryFeatureStore` to fail if done at the same time as an update. (Thanks, [JeffAshton](https://github.com/launchdarkly/dotnet-server-sdk/pull/109)!)

## [5.7.1] - 2019-09-13
_(The changes below were originally released in 5.7.0, but that release was broken; 5.7.1 is its replacement.)_

This release includes new types and deprecations that correspond to upcoming changes in version 6.0.0. Developers are encouraged to start adopting these changes in their code now so that migrating to 6.0.0 in the future will be easier. Most of these changes are related to the use of mutable types, which are undesirable in a concurrent environment. `User` and `Configuration` are currently mutable types; they will be made immutable in the future, so there are now builders for them. Arbitrary JSON values are currently represented with the `Newtonsoft.Json` type `JToken`, which is mutable (if it contains an array or a JSON object); the new type `LdValue` is safer, and will eventually completely replace `JToken` in the public API.
 
Also, generated HTML documentation for all of the SDK's public types, properties, and methods is now available online at https://launchdarkly.github.io/dotnet-server-sdk/. Currently this will only show the latest released version.
 
### Added:
- `Configuration.Builder` provides a fluent builder pattern for constructing `Configuration` objects. This is now the preferred method for building a user, rather than using `ConfigurationExtension` methods like `WithStartWaitTime()` that modify the existing configuration object.
- `Configuration.EventCapacity` and `Configuration.EventFlushInterval` (new names for `EventQueueCapacity` and `EventQueueFrequency`, for consistency with other LaunchDarkly SDKs).
- `User.Builder` provides a fluent builder pattern for constructing `User` objects. This is now the preferred method for building a user, rather than setting `User` properties directly or using `UserExtension` methods like `AndName()` that modify the existing user object.
- `User.IPAddress` is equivalent to `User.IpAddress`, but has the standard .NET capitalization for two-letter acronyms.
- The new `LdValue` type is a better alternative to using `JToken`, `JValue`, `JArray`, etc. for arbitrary JSON values (such as the return value of `JsonVariation`, or a custom attribute for a user).
- There is now more debug-level logging for stream connection state changes.
- XML documentation comments are now included in the package for all target frameworks. Previously they were only included for .NET Standard 1.4.
 
### Changed:
- Calls to flag evaluation methods such as `BoolVariation` are now somewhat more efficient because they no longer convert the default value to a `JToken` internally; also, user attributes no longer need to be converted to `JToken` internally when evaluating flag rules. If flag evaluations are very frequent, this reduces the number of ephemeral objects created on the heap.

### Fixed:
- Due to the default parsing behavior of `Newtonsoft.Json`, strings in the date/time format "1970-01-01T00:00:01Z" or "1970-01-01T00:00:01.001Z" would not be considered equal to an identical string in a flag rule.

### Deprecated:
- All `ConfigurationExtension` methods are now deprecated.
- `Configuration.SamplingInterval`. The intended use case for the `SamplingInterval` feature was to reduce analytics event network usage in high-traffic applications. This feature is being deprecated in favor of summary counters, which are meant to track all events.
- `Configuration.EventQueueCapacity` and `Configuration.EventQueueFrequency` (see new names above).
- `User` constructors (use `User.WithKey` or `User.Builder`).
- `User.IpAddress` (use `IPAddress`).
- All `UserExtension` methods are now deprecated. The setters for all `User` properties should also be considered deprecated, although C# does not allow these to be marked with `[Obsolete]`.
- `IBaseConfiguration` and `ICommonLdClient` interfaces.
- The `InMemoryFeatureStore` constructor. Use `Components.InMemoryFeatureStore`.

## [5.6.5] - 2019-05-30
### Fixed:
- If streaming is disabled, polling requests could stop working if the client ever received an HTTP error from LaunchDarkly. This bug was introduced in the 5.6.3 release.

## [5.6.4] - 2019-05-10
### Changed:
- The NuGet package name and assembly name have changed: they are now `LaunchDarkly.ServerSdk` instead of `LaunchDarkly.Client`. There are no other changes in this release; the namespace used in .NET code is still `LaunchDarkly.Client`. Substituting `LaunchDarkly.Client` version 5.6.3 with `LaunchDarkly.ServerSdk` version 5.6.4 will not affect functionality.

## [5.6.3] - 2019-05-10
### Fixed:
- If `Track` or `Identify` is called without a user, the SDK now will not send an analytics event to LaunchDarkly (since it would not be processed without a user).

# Note on future releases

The LaunchDarkly SDK repositories are being renamed for consistency. This repository is now `dotnet-server-sdk` rather than `dotnet-client`.

The NuGet package name and assembly name will also change. In the 5.6.3 release, it is still `LaunchDarkly.Client`; in all releases after 5.6.3, it will be `LaunchDarkly.ServerSdk`. No further updates to the `LaunchDarkly.Client` package will be published after this release.

## [5.6.2] - 2019-03-26
### Changed:
- The default value for the configuration property `capacity` (maximum number of events that can be stored at once) is now 10000, consistent with the other SDKs, rather than 500.

### Fixed:
- Under some circumstances, a `CancellationTokenSource` might not be disposed of after making an HTTP request, which could cause a timer object to be leaked. ([#100](https://github.com/launchdarkly/dotnet-server-sdk/issues/100))
- In polling mode, if the client received an HTTP error it would retry the same request one second later. This was inconsistent with the other SDKs; the correct behavior is for it to wait until the next scheduled poll.
- The `HttpClientTimeout` configuration property was being ignored when making HTTP requests to send analytics events.

## [5.6.1] - 2019-01-14
### Fixed:
- The assemblies in this package now have Authenticode signatures.

## [5.6.0] - 2019-01-09
### Added:
- There are now helper classes that make it much simpler to write a custom `IFeatureStore` implementation. See the `LaunchDarkly.Client.Utils` namespace.
- The new `FeatureStoreCaching` class will be used by database feature store integrations in the future. It is not used by the SDK client itself.

### Changed:
- Some support code has been moved into a separate assembly, [`LaunchDarkly.Cache`](https://github.com/launchdarkly/dotnet-cache).
- The published assemblies are now built in Release configuration and no longer contain debug information.
- If you are building a copy of the SDK yourself, the Debug configuration no longer does any signing, so does not require a key file.

## [5.5.0] - 2018-10-30
### Added:
- It is now possible to inject feature flags into the client from local JSON or YAML files, replacing the normal LaunchDarkly connection. This would typically be for testing purposes. See `LaunchDarkly.Client.Files.FileComponents`.

- The `AllFlagsState` method now accepts a new option, `FlagsStateOption.DetailsOnlyForTrackedFlags`, which reduces the size of the JSON representation of the flag state by omitting some metadata. Specifically, it omits any data that is normally used for generating detailed evaluation events if a flag does not have event tracking or debugging turned on.

- The non-strong-named version of this library (`LaunchDarkly.Common`) can now be used with a non-strong-named version of LaunchDarkly.Client, which does not normally exist but could be built as part of a fork of the SDK.

### Changed:
- Previously, the delay before stream reconnect attempts would increase exponentially only if the previous connection could not be made at all or returned an HTTP error; if it received an HTTP 200 status, the delay would be reset to the minimum even if the connection then immediately failed. Now, if the stream connection fails after it has been up for less than a minute, the reconnect delay will continue to increase.

### Fixed:
- Fixed an [unobserved exception](https://blogs.msdn.microsoft.com/pfxteam/2011/09/28/task-exception-handling-in-net-4-5/) that could occur following a stream timeout, which could cause a crash in .NET 4.0.

- Fixed a `NullReferenceException` that could sometimes appear in the log if a stream connection failed.

- Fixed the documentation for `Configuration.StartWaitTime` to indicate that the default is 10 seconds, not 5 seconds. (Thanks, [KimboTodd](https://github.com/launchdarkly/dotnet-server-sdk/pull/95)!)

- JSON data from `AllFlagsState` is now slightly smaller even if you do not use the new option described above, because it completely omits the flag property for event tracking unless that property is true.

## [5.4.0] - 2018-08-30
### Added:
- The new `LDClient` methods `BoolVariationDetail`, `IntVariationDetail`, `DoubleVariationDetail`, `StringVariationDetail`, and `JsonVariationDetail` allow you to evaluate a feature flag (using the same parameters as you would for `BoolVariation`, etc.) and receive more information about how the value was calculated. This information is returned in an `EvaluationDetail` object, which contains both the result value and an `EvaluationReason` which will tell you, for instance, if the user was individually targeted for the flag or was matched by one of the flag's rules, or if the flag returned the default value due to an error.

### Fixed:
- When evaluating a prerequisite feature flag, the analytics event for the evaluation did not include the result value if the prerequisite flag was off.

## [5.3.1] - 2018-08-30
### Fixed:
- Fixed a bug in streaming mode that prevented the client from reconnecting to the stream if it received an HTTP error status from the server (as opposed to simply losing the connection). ([#88](https://github.com/launchdarkly/dotnet-server-sdk/issues/88))
- Numeric flag values can now be queried with either `IntVariation` or `FloatVariation` and the result will be coerced to the requested type, as long as it is numeric. Previously, if the type of value that came from LaunchDarkly in JSON (or, more specifically, the type that Newtonsoft.Json decided to decode the value as) was different, it was considered an error and the default value would be returned. This change makes the .NET SDK consistent with the Go and Java SDKs.

## [5.3.0] - 2018-08-27
### Added:
- The new `ILdClient` method `AllFlagsState()` should be used instead of `AllFlags()` if you are passing flag data to the front end for use with the JavaScript SDK. It preserves some flag metadata that the front end requires in order to send analytics events correctly. Versions 2.5.0 and above of the JavaScript SDK are able to use this metadata, but the output of `AllFlagsState()` will still work with older versions.
- The `AllFlagsState()` method also allows you to select only client-side-enabled flags to pass to the front end, by using the option `FlagsStateOption.ClientSideOnly`.

### Deprecated:
- `ILdClient.AllFlags()`

## [5.2.2] - 2018-08-02
- In streaming mode, if the stream connection fails, there should be an increasing backoff interval before each reconnect attempt. Previously, it would log a message about waiting some number of milliseconds, but then not actually wait.
- The required package `LaunchDarkly.EventSource` no longer has `PackageReference`s to System assemblies.

## [5.2.1] - 2018-08-01
### Fixed:
- The internal classes representing feature flag and segment data were not JSON-serializable. This did not affect the SDK itself, but prevented any `IFeatureStore` implementation based on Json.Net serialization from working.
- The event processor did not post to the correct URI if the base events URI was set to a custom value with a non-root path. This did not affect normal usage, but would be a problem if events were being redirected to some other service.

## [5.2.0] - 2018-07-27
### Added:
- New configuration property `UseLdd` allows the client to use the "LaunchDarkly Daemon", i.e. getting feature flag data from a store that is updated by an [`ld-relay`](https://docs.launchdarkly.com/docs/the-relay-proxy) instance. However, this will not be usable until the Redis feature store integration is released (soon).

### Changed:
- If you attempt to evaluate a flag before the client has established a connection, but you are using a feature store that has already been populated, the client will now use the last known values from the store instead of returning default values.
- The `LaunchDarkly.Common` package, which is used by `LaunchDarkly.Client`, has been renamed to `LaunchDarkly.Common.StrongName`. Note that you should not have to explicitly install this package; it will be imported automatically.

### Fixed:
- The SDK was referencing several system assemblies via `<PackageReference>`, which could cause dependency conflicts. These have been changed to framework `<Reference>`s. A redundant reference to `System.Runtime` was removed. ([#83](https://github.com/launchdarkly/dotnet-server-sdk/issues/83))
- The client was logging (at debug level) a lengthy exception stacktrace whenever a string comparison operator was applied to a user property that was null. It no longer does this.

## [5.1.1] - 2018-07-02
### Changed:
- When targeting the .NET 4.5 framework, the dependency on Newtonsoft's JSON.Net framework has been changed: the minimum version is now 6.0.1 rather than 9.0.1. This was changed in order to support customer code that uses older versions of JSON.Net. For most applications, this change should have no effect since it is only a _minimum_ version, which can be overridden by any higher version specified in your own dependencies. Note that when targeting .NET Standard, the minimum JSON.Net version is still 9.0.1 because earlier versions were not compatible with .NET Standard.
- The `Identify` method has been moved back into `ILdClient` rather than being in `ILdCommonClient`.

## [5.1.0] - 2018-06-26
### Added:
- A new overload of `LDClient.Track` allows you to pass any kind of JSON value for the custom event data, not just a string.
- The `User` class now implements `Equals` and `GetHashCode`, and has a copy constructor.

### Changed:
- Some classes and interfaces have been moved into a separate assembly, `LaunchDarkly.Common` (source code [here](https://github.com/launchdarkly/dotnet-sdk-common/)), because they will also be used by the LaunchDarkly Xamarin SDK. The names and namespaces have not changed, so you do not need to make any code changes. `LaunchDarkly.Common` will be installed automatically when you upgrade `LaunchDarkly.Client`; all other dependencies are unchanged.
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
- Fixed a [bug](https://github.com/launchdarkly/dotnet-server-sdk/issues/75) in the event sampling feature that was introduced in 4.1.0: sampling might not work correctly if events were generated from multiple threads.

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
- Addresses Unnecesary Lock contention when polling: https://github.com/launchdarkly/dotnet-server-sdk/issues/18
- Logging framework: Now using Microsoft.Extensions.Logging
- No longer depending on ASP.NET: https://github.com/launchdarkly/dotnet-server-sdk/issues/8

### Added
- Support for .NET core: https://github.com/launchdarkly/dotnet-server-sdk/issues/9
- Http client now has a request timeout: https://github.com/launchdarkly/dotnet-server-sdk/issues/27

### Deprecated
- File-based configuration override option has been removed

## [2.0.3] - 2016-10-10
### Changed
- Code cleanup

## [2.0.2] - 2016-10-06
### Added
- Address https://github.com/launchdarkly/dotnet-server-sdk/issues/27
- Improve error logging- we're now logging messages from inner exceptions.

## [2.0.1] - 2016-10-05
### Changed
- Async http client code improvements.

## [2.0.0] - 2016-08-10
### Added
- Support for multivariate feature flags. New methods `StringVariation`, `JsonVariation` and `IntVariation` and `FloatVariation` for multivariates.
- New `AllFlags` method returns all flag values for a specified user.
- New `SecureModeHash` function computes a hash suitable for the new LaunchDarkly [JavaScript client's secure mode feature](https://docs.launchdarkly.com/docs/js-sdk-reference#section-secure-mode).

### Changed
- LdClient now implements a new interface: ILdClient

### Deprecated
- The `Toggle` call has been deprecated in favor of `BoolVariation`.
