# Third-party components

SLX application source: MIT, [LICENSE](LICENSE).

- **Xray-core 26.9.9**, unmodified official executable: Mozilla Public License 2.0.
  License: [licenses/LICENSE](licenses/LICENSE).
  Corresponding source: https://github.com/XTLS/Xray-core/tree/v26.9.9
  Binary origin and hashes: [runtime/runtime-manifest.json](runtime/runtime-manifest.json).
  Build dependencies of Xray remain governed by their upstream licenses.
- **Wintun**, unmodified, vendor-signed DLL from the official Xray archive:
  WireGuard LLC Prebuilt Binaries License, [licenses/LICENSE-Wintun](licenses/LICENSE-Wintun).
  Source and official redistribution information: https://www.wintun.net/
- **.NET runtime and WPF**: Microsoft, MIT; notices are bundled in licenses/DOTNET-* and licenses/WPF-LICENSE.txt.
  https://github.com/dotnet/runtime and https://github.com/dotnet/wpf
- **CommunityToolkit.Mvvm 8.4.0**: MIT, licenses/MVVM-LICENSE.md and licenses/MVVM-THIRD-PARTY-NOTICES.txt; https://github.com/CommunityToolkit/dotnet
- **Microsoft.Extensions.* / ProtectedData**: MIT, https://github.com/dotnet/runtime
- Test-only dependencies xUnit, Microsoft.NET.Test.Sdk and VS test runner are not shipped as application dependencies.
  Exact direct/transitive versions are recorded in each project's packages.lock.json.

The SLX installer is unsigned. The Wintun DLL and its embedded driver retain vendor signatures.
SLX, Xray, Happ, WireGuard and other names belong to their respective owners; this client is not a Happ fork.

- **Microsoft.Windows.CsWin32 0.3.333**, Microsoft, MIT: official Windows API source generator (build only). https://github.com/microsoft/CsWin32
