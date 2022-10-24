Helper classes and methods for interoperability with JSON.

The NuGet package containing these types is [`LaunchDarkly.CommonSdk`](https://www.nuget.org/packages/LaunchDarkly.CommonSdk). Normally you should not need to reference that package directly; it is loaded automatically as a dependency of the main SDK package.

Any LaunchDarkly SDK type that has the marker interface <xref:LaunchDarkly.Sdk.Json.IJsonSerializable> has a canonical JSON encoding that is consistent across all LaunchDarkly SDKs. There are three ways to convert any such type to or from JSON:

* On platforms that support the `System.Text.Json` API, these types already have the necessary attributes to behave correctly with that API.
* You may use the <xref:LaunchDarkly.Sdk.Json.LdJsonSerialization> methods <xref:LaunchDarkly.Sdk.Json.LdJsonSerialization.SerializeObject``1(``0)> and <xref:LaunchDarkly.Sdk.Json.LdJsonSerialization.DeserializeObject``1(System.String)> to convert to or from a JSON-encoded string.
* You may use the lower-level `LaunchDarkly.JsonStream` API (https://github.com/launchdarkly/dotnet-jsonstream) in conjunction with the converters in <xref:LaunchDarkly.Sdk.Json.LdJsonConverters>.

Earlier versions of the LaunchDarkly SDKs used `Newtonsoft.Json` for JSON serialization, but current versions have no such third-party dependency. Therefore, these types will not work correctly with the reflection-based `JsonConvert` methods in `Newtonsoft.Json` without some extra logic. There is an add-on package, [`LaunchDarkly.CommonSdk.JsonNet`](https://github.com/launchdarkly/dotnet-sdk-common/tree/main/src/LaunchDarkly.CommonSdk.JsonNet), that provides an adapter to make this work; alternatively, you can call <xref:LaunchDarkly.Sdk.Json.LdJsonSerialization.SerializeObject``1(``0)> and put the resulting JSON output into a `Newtonsoft.Json.Linq.JRaw` value.
