# SLX Windows

Version: 0.1.0-alpha.5. STATUS: PARTIAL until required Windows acceptance passes.

Owner authorized the separate public Sooliks/slx-windows repository, source push, unsigned build and GitHub Release assets.
Desktop: C#/.NET 10 LTS, WPF + MVVM, RU/EN resources; native SLX olive/lime and Terminal themes.
Windows 10 build 19041+ and Windows 11 are compatibility targets; installed Windows 10 acceptance remains open.
Pinned Xray core 26.9.9, official Wintun, per-process TUN routing; HY2 primary, VLESS/XHTTP fallback.
The interface runs without elevation. One Windows service owns network/core changes.
No production server, website, subscription, device slot or HWID security changes.
Data: DPAPI CurrentUser, user settings under LocalAppData; typed SID-authorized Named Pipes; credentials never logged.
Import accepts only explicit SLX HTTPS subscription URLs. A Happ-bound link needs the official website transfer.
Separate source history: no website/server history, env, database, real tokens or actual client profiles.
Source <=200 nonblank lines; preserve SLX tokens; real statuses and recovery, no mock production features.

## Alpha.2 scope

Fix local request/impersonation ordering and unread reply loss with bounded ACK protocol v2; preserve all IPC guards.
Wait for the recorded owned TUN GUID to disappear before reconnecting; never remove or ignore foreign adapters.
Refresh an existing saved subscription once at startup without replacing a running tunnel or discarding a valid cache on failure.
Prioritize searchable running-application selection while retaining the manual executable picker.
Keep protected pipe ACL, service-process identity checks, actor SID authorization and full-path application matching.
OS-level same-SID pipe fixtures are useful evidence but do not establish successful installed LocalSystem service control.
Protocol v2 is a local public-contract change: update service and UI together using the installer and close the old UI.
No database/server migration is needed. Installed candidate evidence is recorded separately in docs/VERIFICATION.md.

## Build and release

Version, Windows file version and bundled .NET runtime are owned by Directory.Build.props.
SDK is owned by global.json; metadata helpers validate the exact release SemVer and numeric Windows file version.
Build: dotnet restore --locked-mode; dotnet build Slx.Windows.slnx -c Release --no-restore.
Tests: dotnet test Slx.Windows.slnx -c Release --no-build; scripts/verify.ps1.
Package from a reviewed clean commit: scripts/build.ps1, then scripts/package-source.ps1 -BaseRef <previous release tag>.
Build outputs are isolated under artifacts/work/<version> and artifacts/release/<version>.
For installed-system acceptance before the final commit, build.ps1 -Candidate uses artifacts/candidates/<version>/<timestamp>/.
Candidates always record sourceClean=false and publicationAllowed=false and cannot enter package-source or release publication.
Do not replace prior alpha.1 assets, installed files, existing published tags or release assets implicitly.
Release tooling requires matching clean build/commit/version/provenance/checksums before GitHub mutations.
Unsigned application and installer remain intended; Wintun must retain its valid vendor signature.

Full acceptance requires actual installed service/UI control, both live VPN protocols, selected/unselected application egress,
TCP/UDP/IPv4/IPv6, DNS, crash/sleep/network changes, installer upgrade/uninstall, multi-user IPC and a clean VM.
Parser, build, same-SID pipe fixtures and synthetic TUN sinks do not substitute for these checks.
The release remains a prerelease until the required acceptance passes.

## Alpha.3 candidate
See docs/NETWORK_DIAGNOSIS.md and docs/RELEASE_NOTES_ALPHA3.md. Add local icon library and automatic coalesced routing application with bounded reconnect. No retry after an unknown mutation result. IPv6 TLS hostname restoration preserves IPv4/LAN destinations and certificate validation; no broad IPv6/ECH guarantee.

## Alpha.4 candidate
See docs/RELEASE_NOTES_ALPHA4.md. Apply SkipAsSource only to the recorded owned TUN IPv6 address before Connected.
A curated public catalog has an embedded fallback; local discovery supports programs that are not running.
No inventory collection. Dashboard app switches apply automatically and remain present when disabled.
Connection facts derive from authoritative service observations; no invented speed or traffic counters.
The catalog API is an additive public contract with schemaVersion 1; no client/server credentials or DB migration.
Alpha.5: generic automatic-library uninstall filtering, Windows 10 installer threshold and shared Win10/11 manifest. No networking/configuration changes. See docs/COMPATIBILITY.md.
