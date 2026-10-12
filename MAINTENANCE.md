# Fork maintenance

## Build and test

On Windows with the .NET 10 SDK and .NET Framework 4.7.2 runtime, run:

```powershell
./build.ps1 -NativeCompiler C:/path/to/zig.exe
```

The script restores NuGet packages, builds the WinForms application, builds `Mir4Encryption.dll`, and runs the transport and cipher regression suites. The native build requires `native/current-profile.json` and its generated `native/current_parameters.h`. These are intentionally excluded from Git because they are derived from the installed game client. Use `native/generate-parameters.py <verified-profile.json> <Mir4G.exe>` to generate them; the script checks the executable SHA-256 against the profile before writing build inputs. Do not claim a runnable native build from `-ManagedOnly`; that option skips DLL integration tests.

## Current-client status

The current profile was verified against installed MIR4 client version 1.133.1. `GetServerInfo`, `GetPatchInfo`, and `GetDataInfo` were exercised against the public service metadata for that version. This confirms version discovery and public patch/data metadata access, not account authentication or acceptance of encrypted game packets by a live session.

The packet framing and timestamp approach reuse the user-provided [Mir4-PacketEncryption](https://github.com/AlSch092/Mir4-PacketEncryption) reference. Its README describes AES-256-CBC, while the installed 1.133.1 client routine was independently validated as a modified 10-round AES-like transform using a 128-bit derived key. Do not substitute the older repository's hard-coded table/key or shellcode patching for the generated current-client profile. The emulator's existing login and session architecture remains the foundation.

The transport harness compiles the actual `Network/Session.cs` with minimal adapters. The cipher harness checks key expansion, block encryption, frame construction, the native DLL, the application's `Encryption.Encode` import, and invalid-input guards. Full current-client authenticated login and live encrypted-packet acceptance remain unverified.
