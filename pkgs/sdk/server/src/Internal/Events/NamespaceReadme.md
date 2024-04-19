
This is an *internal* namespace for types related to analytics event processing.

Most of the implementation of analytics events is in the `LaunchDarkly.InternalSdk` package. However, to insulate the public API against changes in internal architecture, none of those types are surfaced directly in the SDK. Instead, we provide a public `IEventProcessor` interface with a default implementation that delegates to the underlying internal code.

These types should not be public and should not appear in documentation.
