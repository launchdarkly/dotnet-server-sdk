
This is an *internal* namespace for `IDataStore` implementations and related internal SDK types.

These types should not be public and should not appear in documentation.

The implementations for specific database integrations are in separate packages such as `LaunchDarkly.ServerSdk.Redis`, but this namespace contains the standard `PersistentStoreWrapper` that is used with all of them.
