using System.Runtime.Versioning;

// The test project references SplitwiseCLI, which is Windows-only (see its own
// AssemblyInfo.cs) - marking this assembly too avoids CA1416 warnings on every call.
[assembly: SupportedOSPlatform("windows")]
