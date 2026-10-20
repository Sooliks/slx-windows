# SLX Windows

Version: 0.1.0-alpha.11. STATUS: PARTIAL until required Windows acceptance passes.

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

## Alpha.11 — restart recovery, updates and native UI

Owner requested broader stabilization, automatic update offers and site-aligned native UI with adaptive icon navigation.
Owner explicitly requested production distribution in this iteration: publish the verified installer and source artifacts
through the existing GitHub release channel, preserving prerelease status while required system acceptance stays open.
The friend's alpha.10 screenshot establishes TRAFFIC_PROTECTION_FAILED after Connect, reported after a reboot;
the exact native cause remains unconfirmed. Missing versioned executable paths are a reproduced failure mechanism.
Resolve the full native application-ID set before replacing any WFP policy, retain stage/native codes without paths,
include disabled owned rules in scoped cleanup, and keep IPC/recovery available when startup restore fails.
Only exact verified Discord/Roblox sibling executable paths are refreshed before connect and during service startup
resume; unresolved paths fail closed. One repaired list feeds protection/core launch, and encrypted intent changes
only after the complete native-ID preflight and WFP replacement commit succeed.
Explicit off remains available when receipt capacity is reached; ownership releases only after confirmed complete off.

Updates use the existing public Sooliks/slx-windows releases over bounded HTTPS. Auto checks at startup/6h are optional,
with a persisted 24h reminder. Download and install are separate explicit actions. SHA-256 matches the published
same-repository checksum; this does not add publisher signing. Protected per-user staging, MOTW and launch-time hash
verification preserve Windows UAC. Installation requires a fresh disconnected/unprotected service observation and
holds the local connection mutation gate through the installer handoff. It never disconnects the VPN automatically.
After successful handoff the UI exits so it cannot reconnect during the installer's welcome screen; wizard cancellation
requires reopening SLX. UAC cancellation keeps the current UI and verified installer available for explicit retry.
Old clients need one manual installer update; no new provider, updater service, dependency, server or DB change.

Native WPF stays olive/lime and Terminal, with static vector cut-X geometry and translucent surfaces. The same sidebar
buttons collapse from 204 to 76px below 1080px window width; supported minimum remains 940x660. Tooltips, UIA names,
selected-section announcements and keyboard focus remain. No ambient timers, fake activity or network telemetry.
Owner's Full HD / 27-inch feedback requires denser native typography: Medium body, stronger captions and Bold headings,
readable small labels, without bundling replacement fonts. Subsequent owner feedback rejected pixel-snapped text;
the final renderer uses Ideal/Grayscale throughout, with 13px secondary text and 14px simple mode choices:
«Приложения» / «Весь ПК» (Apps / Whole PC), preserving routing semantics and accessible names.
Connection facts appear only with confirmed connection and a known connection start; off/unknown/recovery/first-use
screens omit the statistics block rather than display dash placeholders. Unknown protocol is omitted independently.
Help copies only local safe state/version/codes at explicit request. See docs/UPDATES.md and docs/RELEASE_NOTES_ALPHA11.md.

Owner's explicit alpha.11 override: Windows autostart may automatically connect VPN, replacing the historical alpha.9
startup-only behavior below. AutoStart remains opt-in/default false. New optional AutoConnectOnStartup defaults true
and is separately switchable; it applies only to an actual --tray startup with saved AutoStart enabled and a subscription.
Normal launch and render/test initialization never connect automatically. Wait for local network readiness, bounded
subscription refresh (valid cached profiles survive failure) and successful idle service observation. Consume one attempt
before submitting; unknown/rejected replies are never auto-replayed. Manual off/connect/import/remove/exit, opt-out and
update installation cancel the pending attempt. Existing active/protected/recovering service intent is respected.
If a successfully loaded empty startup intent encounters a transient WFP cleanup failure, the service retries only that
owned off-cleanup with pacing. SERVICE_STARTUP_CLEANUP_PENDING is a distinct unprotected fault: the UI waits without
sending a connection and publishes a preparation state. Successful cleanup confirms Disconnected; unreadable intent
never authorizes removal, and explicit Stop cancels this startup-only retry even when the stop itself fails.

Additive local IPC v2 action connect-if-idle is guarded atomically inside the dispatcher: only Disconnected/unprotected
can start; other phases return a recorded no-op without replacement/owner claim. Update UI/service together by installer;
old services reject the new action, with no ordinary-connect fallback. Actor, plan, session, fingerprint and capacity
guards remain. Preference addition and repaired encrypted intent keep the existing shapes; no DB/server migration.

Migration: install UI and service together using the new-version installer; optional preference fields default safely.
Retain installation ID, encrypted subscription and protection intent. Rollback: explicitly disconnect, install alpha.10;
new preference fields are ignored. No provider-ID migration or destructive cleanup. Reboot/ExitLag/clean-VM acceptance
is still required; a build or fixture is not proof of the friend's repair or working installed traffic isolation.

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
Alpha.5: persistent WFP traffic protection, indefinite paced recovery, encrypted service resume intent, protected shared DNS, local shortcut discovery and exact-path Codex / ChatGPT group. Exclude uninstallers/Package Cache. Windows 10 target remains unaccepted. Optional additive ProtectionActive status field stays on IPC v2; update service/UI together. New service-local encrypted intent and owned WFP policy need explicit uninstall cleanup. No server/DB migration. See docs/NETWORK.md and docs/COMPATIBILITY.md.

Alpha.6: visible discovery activity before scanning, background frozen-icon cache warmup, retained old results on failure. Networking, WFP, IPC and storage unchanged from alpha.5. See RELEASE_NOTES and VERIFICATION.

Alpha.7: resolve Desktop/CommonDesktop Steam URL shortcuts for installed CS2/Rust using bounded local VDF/ACF reads. Exact EXE rules; no launching, inventory upload or privileged network changes.

Alpha.9: restore the saved per-user autostart choice on normal UI launch, repair obsolete executable paths and report registration failures in RU/EN. Render checks do not write startup registration. StartupApproved is not modified. Autostart means --tray, not a new VPN-connect command. Additive optional IPC v2 Recovery observation records the count, last reason enum and UTC time of service-observed automatic recoveries; it contains no paths/URLs/keys, survives successful tunnel launch in service memory and resets with the service. Update service/UI through the installer; no DB/server migration. Recovery timings, WFP, TUN and routing rules remain unchanged.

Alpha.10: an isolated health-probe failure invalidates the success observation but retains the running core while confirming it. After one second, two fresh authenticated-proxy HTTPS requests check gstatic (204) and SLX robots.txt (200) concurrently, each within the existing five-second request budget. A valid response plus live owned core/TUN retains ConnectedAt; no response means normal protected recovery. Core exit/adapter loss still recovers immediately. WFP, DNS routing, explicit-off semantics and retry backoff do not change. Startup readiness remains gstatic 204 plus owned TUN/DNS checks. Optional IPC v2 HealthCheck adds only bounded in-memory counters, target/result enums, HTTP codes, durations and time, no error messages/keys/URLs/inventory; it survives launch until service restart. Update UI/service together with installer, no server or DB migration. Actual PC ProbeFailed trigger is established; transport root cause and Pixel sleep issue are not established.
