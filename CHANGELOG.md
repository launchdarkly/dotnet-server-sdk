# Change log

All notable changes to the LaunchDarkly .NET SDK will be documented in this file. This project adheres to [Semantic Versioning](http://semver.org).

## [3.5.0] - 2018-01-TBD
### Added
- Support for specifying [private user attributes](https://docs.launchdarkly.com/docs/private-user-attributes) in order to prevent user attributes from being sent in analytics events back to LaunchDarkly. See the `AllAttributesPrivate` and `PrivateAttributeNames` methods on `Configuration` as well as the `AndPrivateX` methods on `User`.

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
