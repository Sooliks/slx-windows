# SLX Windows
Version: 0.1.0-alpha.1. STATUS: PARTIAL until required Windows acceptance passes.
Owner authorized a separate public Sooliks/slx-windows repository, source push, unsigned build and GitHub Release assets.
Desktop: C#/.NET10 LTS, WPF + MVVM, RU/EN resources; native SLX olive/lime and Terminal themes.
Windows11 x64 primary; Windows10 compatibility must be separately verified.
Single Xray core candidate26.9.9, official Wintun, per-process TUN routing; HY2 primary, VLESS/XHTTP fallback.
User interface runs without elevation. One Windows service owns all network/core changes.
No changes to production servers, website, subscriptions, device slots or HWID security semantics.
Data: DPAPI CurrentUser, user settings under LocalAppData; typed SID-authorized Named Pipes; credentials never logged.
UI imports only explicit SLX HTTPS subscription URLs. A previously Happ-bound link needs official website transfer.
Separate source repository only: no web/server history, env, database, subscription tokens or actual client profiles.
Source<=200 nonblank lines; own token UI; real statuses and recovery, no mock production features.
Build: dotnet restore --locked-mode; dotnet build Slx.Windows.slnx -c Release --no-restore.
Tests: dotnet test Slx.Windows.slnx -c Release --no-build; scripts/verify.ps1.
Package: scripts/build.ps1, InnoSetup6; alpha assets in GitHub Releases with provenance/checksums.
Full acceptance requires actual TUN process routing, both protocols, TCP/UDP/IPv4/IPv6, DNS, crash/sleep/network change, installer upgrade/uninstall, multi-user IPC and clean VM.
Do not call a build or parser check proof of those behaviors. Release remains prerelease until they pass.
