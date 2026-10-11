# Fork maintenance

First milestone: build the existing .NET Framework 4.7.2 application and correct TCP framing. Authentication and outbound cryptography against the current live game remain unverified.

## Build and test

On Windows with the .NET 10 SDK and .NET Framework 4.7.2 runtime:

```powershell
./build.ps1
```

The script downloads NuGet 6.14.0 locally, restores packages, builds the WinForms application, and runs offline transport regression tests. Reference assemblies are restored as a development dependency; the machine does not need a separately installed developer pack. `System.Resources.Extensions` enables resource generation using SDK MSBuild. Application output: `bin/Debug/Mir4NetworkBot.exe`.

The test harness compiles the actual `Network/Session.cs` with minimal context/packet adapters. Five checks cover header-only heartbeat, fragmentation, concatenation and receive-queue integrity, 32-bit lengths, and invalid lengths. Four failed against the original implementation and all five pass after correction. Tests do not load the encryption DLL or contact the game. Send-count fixes are code-reviewed but forced partial-send behavior has not yet been covered by this harness.

Inbound frames are limited to 1 MiB as a defensive allocation bound. Revisit this bound if a validated current capture contains larger frames. The existing extra 15-byte handler buffer is retained to avoid changing handler interfaces in this milestone.

Next milestone: validate current version discovery, login responses and native encryption dependency; do not infer live compatibility from successful compilation.
