# Change log

All notable changes to the LaunchDarkly .NET SDK will be documented in this file. This project adheres to [Semantic Versioning](http://semver.org).

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