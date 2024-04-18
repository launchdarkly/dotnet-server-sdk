
This is an *internal* namespace for SDK implementation types.

Most non-public SDK code should be in one of the more specific sub-namespaces; types should only be put directly into this namespace if they do not fall into one of those specific areas of functionality. These types should not be public and should not appear in documentation.

The namespace `LaunchDarkly.Sdk.Server.Subsystems` is _not_ for the implementations of components like data stores and data sources; that is for the public interfaces and parameter types that describe those components abstractly.
