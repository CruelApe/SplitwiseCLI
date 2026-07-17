using System.Runtime.Versioning;

// This app is Windows-only (see CLAUDE.md) - it relies on Windows DPAPI (SecretProtector)
// to encrypt the saved API key at rest. Marking the whole assembly avoids having to
// annotate every individual class that touches config storage.
[assembly: SupportedOSPlatform("windows")]
