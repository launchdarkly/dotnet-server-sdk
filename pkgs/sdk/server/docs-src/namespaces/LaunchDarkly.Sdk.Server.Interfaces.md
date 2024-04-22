Interfaces and types that are part of the public API, but not needed for basic use of the SDK.

Types in this namespace include:

* <xref:LaunchDarkly.Sdk.Server.Interfaces.ILdClient>, which allows the SDK client to be referenced via an interface 
rather than the concrete type <xref:LaunchDarkly.Sdk.Server.LdClient> (if for instance you want to create a mock implementation for testing).
* Types like <xref:LaunchDarkly.Sdk.Server.Interfaces.IFlagTracker> that provide a facade for some part of the SDK API; 
these are returned by properties like <xref:LaunchDarkly.Sdk.Server.Interfaces.ILdClient.FlagTracker>.
* Concrete types that are used as parameters within these interfaces, like <xref:LaunchDarkly.Sdk.Server.Interfaces.FlagChangeEvent>
