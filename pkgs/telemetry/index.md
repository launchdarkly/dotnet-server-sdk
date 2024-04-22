---
_layout: landing
---

# LaunchDarkly Server-Side SDK for .NET: Telemetry

This package contains telemetry components for the .NET Server-Side SDK. It is a separate package from the main SDK so 
that telemetry related changes will not impact the base SDK.

To get started, check out the documentation for [TracingHook](api/LaunchDarkly.Sdk.Server.Telemetry.TracingHook.yml). 

This class can be used to add custom tracing to the SDK via the `System.Diagnostics` API, for usage with with compatible 
systems like [OpenTelemetry](https://opentelemetry.io/docs/languages/net/).
