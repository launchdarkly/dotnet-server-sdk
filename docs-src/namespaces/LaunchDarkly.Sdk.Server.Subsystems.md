Interfaces for implementation of custom LaunchDarkly components.

Most applications will not need to refer to these types. You will use them if you are creating a plugin component, such as a database integration. They are also used as interfaces for the built-in SDK components, so that plugin components can be used interchangeably with those: for instance, the configuration method <xref LaunchDarkly.Sdk.Server.ConfigurationBuilder.DataStore> references <xref LaunchDarkly.Sdk.Server.Subsystems.IDataStore> as an abstraction for the data store component.

The namespace also includes concrete types that are used as parameters within these interfaces.
