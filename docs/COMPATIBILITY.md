# Windows compatibility and library discovery

Target: Windows 10 build 19041 or newer and Windows 11. Architecture remains an engineering constraint in the installer/provenance, not a visible marketing label.
Installer MinVersion was 10.0.22000; alpha.5 changes it to 10.0.19041.
Application APIs use Windows pipe/ACL, DPAPI, IP Helper, SetupAPI and native window/icon functions available within this target.
The application manifest declares the shared Windows 10/11 supportedOS identity.

Actual automated and installed checks were executed on Windows 11 only. A successful build or lower installer threshold does not prove Windows 10 installation/TUN acceptance.
Windows 10 VM/device, sleep, driver, DNS and per-process UDP checks remain open. This is a prerelease compatibility target.
Microsoft's runtime servicing policy is separate from application compatibility:
https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md
The public page lists Windows 10 / 11 and the prerelease state; it does not claim Windows 10 certification.

Automatic discovery uses one filename policy across running programs, registry DisplayIcon and curated locations.
Standard uninstall / uninstaller / uninst / uninsNNN / unwise / deinstall executables are excluded regardless of product name or install directory.
A parent directory containing uninstall does not exclude a real app. Genuine user tools with unrelated executable names remain discoverable.
No user rules or application files are deleted. Manual executable selection remains available. Discovery does not launch executables.
