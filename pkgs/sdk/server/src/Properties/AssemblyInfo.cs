using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("186f235c-0e7c-419a-b279-5e851e18a41a")]

#if DEBUG
// Allow unit tests to see internal classes (note, the test assembly is not signed;
// tests must be run against the Debug configuration of this assembly)
[assembly: InternalsVisibleTo("LaunchDarkly.ServerSdk.Tests")]

// Allow mock/proxy objects in unit tests to access internal classes
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
#endif
