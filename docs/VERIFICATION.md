# Verification — 0.1.0-alpha.16 (2026-10-23)

STATUS: PARTIAL. Owner explicitly requested build/publication without a full verification run. No verify.ps1, unit or WPF tests executed for this release; newly added tests are unexecuted. Release build and packaging are performed by the standard scripts; exact identity is recorded in provenance. Installed upgrade, receipt expiry behavior, staged-installer reuse and the reported long-uptime failure remain unverified. Historical alpha.15 results below do not validate alpha.16.

# Verification — 0.1.0-alpha.15 (2026-10-23)

STATUS: PARTIAL. Codex ran `pwsh -NoProfile -File scripts/verify.ps1` on the owner's Windows host via an evidence
wrapper: final run 03:05:15–03:05:46 UTC, exit 0. Locked restore, Release solution build with zero warnings/errors,
379 unit/Windows checks and one expanded WPF scenario passed, zero failed/skipped, source <=200 nonblank lines.
SHA-256 inventories of all non-ignored source files before/after the gate match. Local evidence:
artifacts/handoff/verify-alpha15.txt and source-alpha15-before/after.json. Only release documentation was finalized
after this run; runtime/test files were unchanged. Earlier integration runs also passed. Claude authored the initial
UI pass; Codex ran the host commands and performed release review. The original runs 1–2 caught a new scroll-memory
defect, corrected before the successful run 3 and this final gate; no gate was weakened.

Expanded WPF evidence covers page scroll memory, service-confirmed status tone, blocked subscription removal,
non-persistent removal, and deferred app edits submitted once after a fresh observation without replaying unknown
mutations. Saved-choice labels remain explicit at no observation/off/unconfirmed/unknown states in both languages;
13px labels fit the narrow/wide layouts. High Contrast fixtures switch one owned resource layer on/off, preserve
foreign resources and restore the selected theme without accumulating layers. These fixtures inject preferences;
they never change the owner's actual Windows accessibility settings.

Separate unsuppressed native-window fixtures start real WPF animation clocks. Mid-flight hide, minimize, unload,
policy-off and render suppression apply final values and remove clocks/subscriptions; finite fade/route completion
does the same. Route base Progress stays 1 and losing confirmation cancels the ring. This is native lifecycle
evidence, not a PresentMon/ETW performance study or a physical-monitor motion review.

Caption checks cover equal 46x42 hit targets, 12x12 glyph areas and numerical centering, keyboard-focus/chrome flags,
UIA maximize/restore invocation and RU/EN names/tooltips. Maximize happens only while the isolated fixture is hidden.
Caption normal/restored and hover/press PNGs were viewed; hover/press previews force final template layers and are
not evidence of real mouse input. Subtree capture now normalizes its origin, avoiding empty offset screenshots.
Current populated/update PNGs were viewed at RU 940x660 and EN 1200x820, Olive/Terminal. The synthetic updater offer
is alpha.16 for installed alpha.15; it is not a public alpha.16 release. 224 matching RU/EN keys across four owners,
no duplicates. git diff --check passed. Olive uses native Segoe UI if site fonts are absent; no font is installed.

The isolated `dotnet run --project src/Slx.Client.Desktop/Slx.Client.Desktop.csproj -c Release --no-build --no-restore
-- --render-check` refreshes empty/import/help/settings PNGs without reading stored user data, contacting the service
or starting update checks. Final packaging identity belongs to clean-build provenance and SHA256SUMS.txt.

No installed UI/service, saved subscription/preferences, startup registration, WFP, adapter, account, website,
server or DB was changed for acceptance. No network/Core/Windows/Service, IPC, dependency or stored-data format change.
Real Windows animation/High Contrast toggles, physical DPI 125/150/200, Narrator/keyboard in the installed product,
live performance, Windows 10/clean VM, multi-user, reboot/BFE/ExitLag, sleep/network transitions and installed
upgrade/UAC/cancel/rollback remain open. The friend's reboot failure is not declared reproduced or fully fixed.
The real-core API contract from alpha.14 remains historical evidence; this UI-only release does not rerun or extend
packet/TUN/DNS isolation or connection-continuity acceptance.

# Verification — 0.1.0-alpha.14 (2026-10-21)

STATUS: PARTIAL. `pwsh -NoProfile -File scripts/verify.ps1` on the Windows host exited 0: locked restore,
Release solution build with zero warnings/errors, 379 unit/Windows checks and one expanded WPF scenario, zero
failed/skipped, <=200 nonblank source gate. The first build found CS8631 in three new nullable JSON assertions;
explicit non-null string assertions corrected them without weakening tests or production guards. After adding the
final native UI evidence scenario, targeted `dotnet test tests/Slx.Client.UiTests/Slx.Client.UiTests.csproj -c Release
--no-restore` also exited 0, one expanded scenario and no failures/skips/warnings.

New isolated evidence covers effective app-set equality, strict profile/mode comparison, separate dispatch path,
SID/session/receipt-capacity guards, idle update rejection without ownership capture, strict replay after stop and
app updates never invoking Start/Stop. WFP planner checks preserve existing filter IDs, no-op zero transactions,
full preflight, union/rollback snapshots and baseline invalidation after native failure. They never call real WFP.
Coordinator checks cover union → durable desired intent → one core replacement → final policy, initial/final storage
failure, cancelled caller, lost acknowledgement reporting old/new/unrelated revisions and read-only reconciliation
without replay. A failed final native transaction retains protection and unconfirmed state, not a false success.

WPF checks cover coalescing, disabled removal/toggle-back using app-only IPC, final app off, separate mode/profile
reconnect including changed same-name profile, Whole-PC library changes, explicit reconnect, unknown outcome without
automatic replay, stale response/revision versus newer edit and live protected failure feedback. Current native RU
940x660 and EN 1200x820 live/unconfirmed PNGs were inspected: hints and persistent scoped feedback wrap; connection
action stays available, no horizontal overflow. These are synthetic profiles and controlled status, not real VPN proof.
218 paired RU/EN keys across four owners, no duplicates; source size and git diff --check pass.

Separate real-core command: `dotnet run --project tests/Slx.XrayRouteContract/Slx.XrayRouteContract.csproj -c Release
--no-build -- C:/Users/xls/Desktop/slx-windows/.runtime/xray.exe` exited 0 after a zero-warning targeted build. The
fixture verifies the pinned executable SHA, starts its own Xray with config in stdin, authenticated loopback HTTP and
local echo only. Two actual full routing replacements and ListRule revision readbacks passed; the same existing TCP
stream continued. Unauthenticated CONNECT returned 407 and wrong-password AddRule was rejected. A first fixture launch
rejected a documentation IP as INVALID_PROFILE; its synthetic hostname was corrected, without changing runtime policy.
This is an actual pinned API contract, not TUN, installed service, WFP or Internet traffic acceptance.

No installed application/service, user preferences/subscription, firewall, adapter, server, account or DB was mutated.
Existing sessions in the changed app can retain their old route until that app reconnects. Windows ALE can reauthorize
flows after policy changes, so retained core/TUN/filter identities do not establish every flow's packet continuity.
Installed app-rule changes and packet isolation/continuity, unusual Shell providers, Windows 10, multi-user loopback
port races, boot/sleep/network changes, upgrade/UAC/rollback, DPI/High Contrast/live accessibility remain open.
Final build/installer identity belongs to release provenance; publication is not installed-system acceptance.

# Verification — 0.1.0-alpha.13 (2026-10-20)

STATUS: PARTIAL. `pwsh -NoProfile -File scripts/verify.ps1` on the Windows host exited 0:
locked restore, Release solution build with zero warnings/errors, 330 unit/Windows checks and one expanded
WPF scenario; zero failed/skipped, <=200 nonblank authored source gate. An initial build found a nullable
test assertion (CS8602); it was corrected without weakening gates. Final targeted
`dotnet test tests/Slx.Client.UiTests/Slx.Client.UiTests.csproj -c Release --no-restore` exited 0 after
the Settings spacing and update copy changes: one expanded WPF scenario, zero failed/skipped or build warnings.

Icon fixtures cover monotonic negative-cache expiry, one bounded cancellable retry, weak-target lifetime,
group-member fallback without changing routing rules, explicit same-path refresh and frozen native images.
An isolated Unicode temporary path starts missing, receives a copy of the built apphost and recovers its actual
resource icon on retry. The apphost is never executed. Real Yandex and TeamSpeak executables on this PC also
returned native images through the actual alpha.13 loader in a read-only probe; alpha.12 decoded them in the
same observation. The user's original transient failure is not reproduced. Permanent negative caching and
first-member-only image lookup are confirmed code defects; missing explicit Shell COM setup is an API-contract
hardening change, not proof of the original cause. No preferences, subscription data or service state were changed.

The controlled WPF scenario checks the floating card at RU 940x660 and EN 1200x820. Connection and subscription
rectangles remain identical before/after the offer and dismissal; the card does not intersect Connect or steal
focus, and modal flows suppress it. Dismissal during download keeps the transfer running and progress visible
in Settings. Tests cover fresh service-state installation guards, mutation-lease races, UAC cancellation,
checksum failure, prepared-file retention and rejection of late progress from an older download. Fixtures never
launch an installer or UAC. Current native PNGs for offers, download, deferred Settings and failure/wait states
were visually inspected without clipping or horizontal overflow. The synthetic offer is alpha.14 for installed
alpha.13; it is not a published-release or installed-upgrade claim. 215 matching RU/EN resource keys across
the main, library and updates owners; no duplicate keys. `git diff --check` passed.

No installed client, startup registration, VPN service, firewall, adapter, account, server or DB changes were made
for acceptance. Installed upgrade/cancel/rollback, clean Windows 10/11, unusual Shell providers, physical-monitor
rendering at 125/150/200% DPI, High Contrast and live accessibility remain open. Historical reboot/network acceptance
gaps remain open. Final clean source/installer identity is recorded in release provenance; publication does not
establish installed-system acceptance.

# Verification — 0.1.0-alpha.12 (2026-10-20)

STATUS: PARTIAL. Final `pwsh -NoProfile -File scripts/verify.ps1` on the Windows host exited 0:
locked restore, Release solution build with zero warnings/errors, 330 unit/Windows checks and one expanded
WPF scenario; zero failed/skipped, <=200 nonblank authored source gate. An earlier targeted cache/scanner
run hit sandbox DPAPI restrictions (6 failures of 42); the same targeted host run passed all 42. No gate was weakened.

New unit evidence covers schema/count/size/Unicode/path/UTC cache validation, CurrentUser DPAPI round trip,
corrupt/foreign-purpose/missing cache, atomic replacement and independent preferences/vault behavior in isolated
fixture directories. Discovery checks cover fast-source publication before a blocked source, concurrency three,
source failure isolation, cancellation quiescence/no late publication and total shortcut-directory queue bounds.

The controlled WPF scenario covers saved rows before a blocked scan, fast batches, single refresh, search and
explicit addition during refresh, 300 previous plus 300 new transient rows retained on partial failure, unchanged
observation time and no cache write after incomplete scan. Complete scans prune old rows and save once; queued
progress cannot restore an old row while the final cache save is blocked. A recent automatic revisit reuses the
complete result; manual refresh bypasses that interval. Missing cached files cannot become selected routes.
Discovery does not mutate selected rules or call the service. Pagination and search across the available list pass.

Async icon fixtures check two workers, coalescing, cache access during blocked extraction, LRU eviction, frozen
fallback, caller cancellation, origin Dispatcher, weak target lifetime and disposal. UIA/search fixtures preserve
query/caret/logical focus and the same card when a later source adds a second executable path. RU 940x660 and
EN 1200x820 WPF PNGs were inspected for cached refresh/search/partial failure, without clipping or horizontal
overflow. These are synthetic fixtures with fallback icons, not actual third-party icons or a physical-monitor study.
201 matching RU/EN resource keys across the main and library owners; no duplicate keys. git diff --check passed.

A temporary read-only console called the actual DiscoveryScanner on this Windows PC, without writing a cache,
executing a shortcut, printing app names/paths or changing any network/service state. Exit 0: first nonempty batch
100ms, all six sources succeeded, total 1866ms. This is one observation with this PC's OS/file caches, not a benchmark
across users or a measurement of the complete WPF frame time. Optional persisted-cache behavior is fixture tested.

No installed client, startup registration, VPN service, firewall, adapter, server, account or DB changes were made
for acceptance. Required open evidence includes installed upgrade/cancel/rollback and first/returning startup latency
on clean Windows 10/11, unusual shell providers, DPI/High Contrast and live screen-reader behavior. Historical network
acceptance gaps remain open; the service and network policy are unchanged in alpha.12. Final clean build/installer
identity is recorded in release provenance; publication does not establish installed-system acceptance.

# Verification — 0.1.0-alpha.11 (2026-10-20)

STATUS: PARTIAL. Final `pwsh -NoProfile -File scripts/verify.ps1` outside the restricted sandbox exited 0:
locked restore, Release build with 0 warnings/errors, 285 unit/Windows checks and one expanded WPF scenario,
0 failures/skips, <=200 nonblank source gate. The initial sandbox restore could not access user NuGet.Config;
the host rerun used the same gate without weakening it. An earlier updater success fixture exceeded its artificial
2s disk-I/O budget; ordinary fixtures now use 15s, with dedicated 100ms timeout tests and production limits unchanged.

New isolated evidence covers complete native application-ID preflight, missing EXE/directory results 2/3,
safe stage-code preservation, scoped owned-filter cleanup, explicit-off receipt capacity and ownership release,
Discord/Roblox exact-path repair before connect and encrypted service resume, and atomic connect-if-idle no-op/replay.
Startup checks cover normal/manual launch guards, delayed network/service/profile readiness, cached refresh failure,
unknown acknowledgement without replay, manual/setup/update cancellation and existing protected intent.
Known-off startup cleanup pacing/cancellation is checked without WFP mutation; unreadable intent never authorizes cleanup.
The UI waits only for the distinct unprotected startup-cleanup code and cannot connect until confirmed ready.

Updater checks cover SemVer, complete same-repository release assets, bounded HTTP and redirects, staging ACL/MOTW,
size/SHA-256 verification, launch locking, cancellation and download/install state handling. Launcher fixtures never run
an installer or UAC. A temporary isolated console called the real public ReleaseUpdateClient for installed alpha.9:
exit 0, Available / 0.1.0-alpha.10 / checksum validated. It read metadata/checksums only.

Current real WPF RenderTargetBitmap snapshots were inspected at 1200x820 and 940x660: wide/compact navigation,
connection/fault/update states, import, long app names, library/loading, Terminal Help and RU/EN settings.
The disconnected/unknown/recovery/first-use facts block is absent; confirmed synthetic connection shows actual fixture
values and unknown protocol is omitted. Routing labels are «Приложения» / «Весь ПК» at 14px with Ideal/Grayscale,
13px secondary text and stronger native weights. Settings UIA toggles the new auto-connect preference; disabled startup
preserves its saved choice. These fixtures do not read real subscriptions or change Windows startup registration.
The hidden `--render-check` process exited 0. artifacts/ui contains generated images; 96-DPI evidence is not a physical
27-inch monitor, live screen-reader or 125/150/200% DPI acceptance.

An earlier unpublished `scripts/build.ps1 -Candidate` exited 0 and checked official Xray 26.9.9 SHA-256 and Valid vendor
Wintun signature. Intermediate typography binaries are preserved outside the final release directory; they are not
final installed-binary evidence. Final clean source/installer identity is recorded separately in artifact provenance.

The friend's screenshot/report establish alpha.10 TRAFFIC_PROTECTION_FAILED after reboot/Connect. Removed version
paths reproduce one mechanism; the exact friend's native error and ExitLag conflict remain unknown.
No installed service, live VPN, BFE/firewall/adapter, production account, server or DB mutation was performed.
Open acceptance: installed service/UI, real TUN/egress/isolation, actual reboot/ExitLag coexistence, installer
upgrade/UAC/cancel/rollback, clean VM, Windows 10, sleep/network changes, full IPv6, multi-user packet isolation,
physical-monitor rendering, system High Contrast and live accessibility/input. GitHub publication is not this acceptance.

# Verification — 0.1.0-alpha.10

STATUS: PARTIAL. On 2026-10-19 scripts/verify.ps1 exited 0: locked restore, Release build with zero warnings/errors, 193 unit/Windows checks and one WPF scenario; no failures/skips, source size passes. New cases cover first success, primary/alternate confirmation permutations, cancellation, exact HTTP codes, safe error categories and retained diagnostic facts. These fixtures do not prove a working VPN.

On the installed alpha.9, read-only status recorded Recovery Count=3 / ProbeFailed at 14:18:21Z, ConnectedAt 14:18:23Z. The owned Xray process was created at the same recovery time while the SLX service had started earlier at 14:12:42Z. This confirms automatic core replacement after a failed probe, not the underlying reason for that probe failure. No rule changes or intentional kills were performed for this observation.

The candidate installer exited 0, file version 0.1.0.10; the saved installation identity hash matched before/after. The actual LocalSystem service resumed the saved Hysteria2 selected-app connection with ProbePassed=true and ProtectionActive=true, ConnectedAt 14:26:51Z. A bounded read-only three-second sampler records core PID, authoritative status and safe diagnostic observations. At 14:29:06Z the same PID and ConnectedAt remained; no failed-primary incident had occurred yet. This is a short observation, not proof of long-term stability or the Pixel sleep case. The later final installer also needs its own acceptance; candidate evidence is not a final-binary claim.

Windows 10, clean VM, full IPv6, boot/sleep/network-change and packet-level acceptance remain open. No server/Happ settings or credentials change.
# Verification — 0.1.0-alpha.9

STATUS: PARTIAL. On 2026-10-19 scripts/verify.ps1 exited 0: locked restore, Release build with zero warnings/errors, 175 unit/Windows checks and one expanded WPF scenario; no failures/skips, source size passes. Five new isolated HKCU fixture cases cover missing/obsolete startup repair, opt-out, unrelated-value preservation and unsafe host/command rejection. Two recovery observation checks cover old IPC compatibility and diagnostic facts that do not manufacture connection success. RU/EN resources include a dedicated startup-registration error. Candidate installer exited 0 and installed file version 0.1.0.9. With the saved opt-in true and real Run entry absent, launching the installed EXE with --tray restored exactly the quoted installed path plus --tray. Shell_NotifyIconGetRect found one registered icon with its owning window hidden. The Xray PID stayed unchanged across this UI start; real service status remained Connected/ProbePassed=true and the installation ID hash matched before/after. This is startup-command acceptance, not an actual Windows sign-out/reboot. New recovery observations are unit/IPC checked; a natural failure on alpha.9 is not yet observed.

# Verification — 0.1.0-alpha.8

STATUS: PARTIAL. RU/EN help-copy correction only; network/IPC/storage unchanged from alpha.7.
168 unit/Windows checks and one expanded WPF scenario passed via scripts/verify.ps1; source size and locked restore passed.
Owner confirmed CS2/Rust with icons in installed alpha.7, and Claude grouping/right-edge scrollbar in alpha.5.
Historical native evidence and open system acceptance remain below.

# Verification — 0.1.0-alpha.7

STATUS: PARTIAL. Windows 11. scripts/verify.ps1: locked restore/Release build, 168 unit/Windows tests and one expanded WPF scenario; no skips/failures, source size passes.
Local Steam discovery returned Counter-Strike 2 (cs2.exe) and Rust (RustClient.exe, Rust.exe), including a second drive.
Twelve new cases cover URL allowlisting, ambiguous/argument rejection, VDF unescaping, traversal rejection and two-library exact EXE resolution.
No game, Steam protocol handler or anti-cheat was launched; gameplay/network behavior is not accepted by discovery tests.
The shared remote catalog stays at 18 entries. Steam discovery is local and does not upload app lists.
Network/service/WFP source unchanged from alpha.5; its native evidence remains below. Alpha.6 loading indicator and tests are retained.

# Verification — 0.1.0-alpha.6

STATUS: PARTIAL. Windows 11; Windows 10 and complete system acceptance remain open.
UI-only follow-up to published alpha.5. scripts/verify.ps1 exit 0: locked restore, Release build,
156 unit/Windows tests and one expanded WPF scenario, no skips/failures, zero warnings/errors, source size passes.
The blocked-first-discovery WPF scenario renders a visible indeterminate progress indicator before completion,
hides empty-state copy, disables refresh, then verifies success and retained results/message after failure.
No real network is used by this UI fixture. Icons are warmed on the worker task as frozen cached sources.
Network/service/WFP/IPC/storage source is unchanged from alpha.5; native evidence below belongs to alpha.5.

# Verification — 0.1.0-alpha.5

STATUS: PARTIAL. Actual Windows 11 execution; Windows 10 target remains unaccepted.

- scripts/verify.ps1 exit 0: locked restore, Release build, zero warnings/errors; 156 unit/Windows checks and one WPF scenario; no skips/failures; source size contract passes.
- Real pinned Xray isolated DNS A/B: plain DNS returns RCODE 3 / zero answers for youtube.com and www.youtube.com; DoH via HY2 and VLESS returns RCODE 0 / positive answers. No second TUN or server edits; configuration/credentials remain in memory.
- Local discovery observed one registered OpenAI desktop group, three exact executable paths and 77 readable shortcut targets. Counts only; inventory is not published or uploaded.
- Isolated real WFP two-EXE IPv4 TCP/UDP: before both pass; selected block fails both while control passes; owned TUN next-hop permit restores selected; persistent block survives engine close; explicit Remove restores both. Local-interface condition was insufficient; final code uses IP_NEXTHOP_INTERFACE.
- Installed candidate: setup exit 0, file version 0.1.0.5. Connected/ProbePassed/ProtectionActive true; YouTube resolves. Owner confirmed YouTube works and Codex / ChatGPT appears.
- Installed service stop: selected fresh IPv4 TCP and UDP fail, unselected control passes. Service start restores encrypted plan and protection. Three forced owned-core exits recover independently with ProtectionActive/ProbePassed true; VLESS also connects. Explicit off releases protection; original HY2/app plan restored.
- Final installed rerun after DNS readiness: first selected TCP/UDP probes pass after resume; three more forced core exits recover with protection, HY2/VLESS pass. Already-open selected TCP/TLS and UDP sockets pass before stop and fail while service/TUN is stopped. In whole-computer mode both test EXEs pass connected and both fail while stopped. Explicit off restores both; original HY2 plan restored. Public DNS is also blocked for the unselected control during recovery, as required by the shared resolver protection.
- Matching-name WPF scenario verifies one Claude card, two retained paths, both switched together and one card after settings reconstruction. Owner confirmed YouTube and OpenAI group in the earlier candidate.
- SCM restart policy confirmed 3/10/30 seconds. Persistent filters and machine-DPAPI resume state are owned locally; explicit uninstall releases only SLX policy.

Open: Windows 10/clean VM, full IPv6/ECH/HTTP3, existing flows, sleep/Wi-Fi/interface changes, reboot-before-BFE, multi-user packet isolation and all DPI/a11y combinations. Current native new-IPv4 socket evidence is not full packet-level/no-leak acceptance. Historical releases below retain their original limitations.

# Verification — 0.1.0-alpha.4

STATUS: PARTIAL. Windows 11 x64 prerelease. Historical alpha.3 and alpha.2 evidence remains below.

## Alpha.4 executed on 2026-10-18

- scripts/verify.ps1: exit 0; locked restore, Release build with zero warnings/errors, 128 unit/Windows checks and one expanded WPF scenario; zero failed/skipped.
- 152 matching RU/EN keys. WPF fixture renders of dashboard facts and catalog inspected at 940x660: switches remain present when disabled, long names wrap, facts fit, library uses outer page scrolling.
- Catalog parser rejects paths/commands and malformed schemas. Embedded fallback contains 18 entries. Discovery does not upload installed/running programs.
- Temporary IPv6 source-address experiment restored its change automatically. Owner confirmed ChatGPT/Claude opened during the experiment.
- Installed alpha.4 candidate: installer exit 0, file version 0.1.0.4, authoritative Connected/ProbePassed=true. Owned adapter address reports SkipAsSource=true automatically after connection.
- Owner confirmed both ChatGPT and Claude open after ordinary candidate connection, after the temporary test had restored. This is end-user evidence, not packet-level acceptance.
- Service change touches only its owned TUN address via native IP Helper. The UI and service remain unprivileged/privileged respectively; no physical-interface or global IPv6 changes.
- Website catalog deployment can follow later; embedded catalog remains available on 404/network/invalid response.

Open acceptance: process-specific UDP isolation, HTTP3, general IPv6/ECH, DNS isolation, sleep/network transitions, clean VM, multi-user and all DPI/accessibility combinations.
No kill switch. No server, Happ, secret, device-binding or schema changes in this client release.

# Verification — 0.1.0-alpha.3

STATUS: PARTIAL. Windows 11 x64 prerelease. Historical alpha.2 checks remain below.

## Alpha.3 executed on 2026-10-18

- scripts/verify.ps1: exit 0; locked restore, Release build, zero warnings/errors, 120 unit/Windows checks plus one expanded WPF scenario; zero skipped/failed.
- 146 matching RU/EN resource keys. Source <=200 nonblank lines and git diff --check passed.
- New WPF checks cover icon library wrapping/fallback/cache/manual picker, automatic coalesced app updates, last-app disconnect, explicit-stop cancellation and no retry after an unknown reply.
- Real local forwarder A/B: IPv6 YouTube TLS failed without SNI restoration and received HTTP/1.1 200 OK with the exact candidate exclusions on both HY2 and VLESS. Credentials/configuration stayed in memory; no server edits.
- Candidate installer exit 0, file version 0.1.0.3. The installed service restored HY2 selected mode with Connected/ProbePassed=true. Owner confirmed YouTube and Twitch both work afterward.
- Selected fresh-EXE TCP exit matched Amsterdam; unselected control was RU. Whole-mode TCP and STUN matched Amsterdam. Selected UDP had inconsistent exit observations and is not accepted as isolated.
- Forced HTTP3 timed out through both protocols and a separate localhost UDP forwarder; direct control passed. No blanket UDP block or unsupported HTTP3 success claim.
- IPv6 TLS restoration is a compatibility mitigation, not general IPv6 egress. IPv4/LAN destinations remain unchanged; known cloudflare-ech.com is excluded. General ECH acceptance remains open.
- Automatic rule application uses one bounded reconnect after debounce. Existing sockets can need application reconnection. It is not a seamless routing API update.
- Browser automation could not be used because native browser policy support was unavailable. Browser outcome is explicitly owner-reported, not automated E2E evidence.

Details: [network diagnosis](NETWORK_DIAGNOSIS.md). Sleep/network changes, clean VM, multi-user, full IPv6 and UDP isolation remain unverified. Kill switch is not implemented.

# Verification — 0.1.0-alpha.2

STATUS: PARTIAL. Windows 11 x64 prerelease. The checks below do not constitute full system/security acceptance.

## Executed on 2026-10-18

- `pwsh -File scripts/verify.ps1`: exit 0; locked restore, Release build with zero warnings/errors, 118 unit/Windows tests and one populated WPF UI scenario passed; zero failed/skipped. Authored source size contract passed.
- RU/EN: 145 matching resource keys; all literal XAML localization keys resolve.
- Ten native pipe contracts cover protected ACL, caller SID, service PID, invalid frames, delayed readers, thirty reconnects, bounded missing ACK and preserving a known reply when its ACK fails.
- Native PnP checks recognize the present physical adapter and reject a nonexistent GUID; invalid identities fail closed.
- Owned adapter cleanup tests cover delayed removal, cancellation, bounded timeout and preserving unrelated adapters. Background cleanup retains faults without stopping local control.
- Four synthetic production-emitter cases retain both HY2 and VLESS, including base64/CRLF and Cyrillic names; insecure HY2 fails the entire import rather than being silently filtered.
- Populated WPF checks cover profile templates, whole/selected modes, narrow layout, themed tray, unknown exit, fixed geometry for hover/focus, quick-app search/add and startup refresh success/failure/stale completion.
- Actual WPF renders under artifacts/ui were inspected. They contain synthetic data and are layout evidence only.
- The earlier testhost FailFast appeared during test teardown. The harness now owns a complete dispatcher lifecycle and joins its STA thread. An isolated rerun exited 0 with no remaining new testhost or new matching WER/.NET/ApplicationError events. Exact original native cause was not recovered.

## Installed candidate evidence

The owner had uninstalled alpha.1; this is fresh candidate installation followed by candidate replacement, not clean alpha.1 upgrade acceptance.

- Inno candidate installation/replacement exited 0; the actual SLXVPN LocalSystem service reached Running.
- Twelve sequential real status requests passed after ACK v2 was installed. Actual UI polling subsequently showed authoritative disconnected/connected states.
- Candidate with the final tunnel/presence logic: actual Hysteria2 and VLESS each connected and completed two explicit reconnects. Every result was Connected with ProbePassed=true.
- ProbePassed means a real HTTPS 204 through the authenticated local proxy and a ready dual-stack TUN owned by the pinned core. This alone does not prove selected/unselected application egress isolation.
- Both protocols then disconnected successfully; default route snapshots before/after were equal. No present SLX adapter remained after stop.
- A stale local cache contained only VLESS. A fresh response for this installation contained HY2 and VLESS. Automatic startup refresh was then observed in the installed UI with both methods available.
- Actual native UI showed correct profile names, the new background and inline quick selection. Manual .exe selection remains available.
- Native known-stop exit completed with zero UI processes, Disconnected state and no present adapter. Separately, explicit unknown-stop app-only exit terminated UI while leaving the service running, as disclosed.
- Final packaging uses a reviewed clean commit; provenance/checksums identify the exact installer and source. Published alpha.1 assets are not overwritten.

## Root causes and boundaries

The service previously disconnected a named pipe before the desktop consumed its reply. A delayed-reader OS test reproduced the loss. ACK v2 bounds reply acknowledgement before disconnect; failure to send an ACK after a complete reply does not make that known result unknown. UI and service must update together.

Windows retains detached adapter records in GetAdaptersAddresses even after the PnP device is gone. Presence now checks read-only SetupAPI DIGCF_PRESENT; a disabled but present adapter is still a conflict. Reconnect waits only for its recorded owned GUID. It never deletes another device or disables the conflict guard.

References:
- https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdigetclassdevsw
- https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream.runasclient?view=net-10.0
- https://github.com/XTLS/Xray-core/blob/v26.9.9/proxy/tun/tun_windows.go

## Historical alpha.1 scope

Alpha.1 had 93 passing unit/contracts, six valid core-parser configurations and two rejected invalid configurations.
An isolated two-EXE native IPv4 TCP/UDP classifier run used localhost synthetic sinks and restored routes/adapters.
It did not prove live VPN encryption, production DNS, IPv6 or selected/unselected public egress.
Historical sanitized evidence: docs/evidence/tun-classifier.json.

## Not verified

Independent selected/unselected application public egress; live UDP/IPv6 and DNS isolation;
sleep/resume, Wi-Fi changes, crash/cancelled-operation recovery; OS multi-user rejection;
all DPI/multiple-monitor/screen-reader cases; clean VM install, alpha.1 upgrade, repair/uninstall.
Windows 10 is not claimed. No full kill switch or packet-level Windows SID isolation is claimed.
Selected-app DNS uses the shared Windows resolver directly. Installation-ID first initialization across separate Windows sessions remains unverified.

Production servers, Happ, DB/device records and subscription keys were not changed.

Alpha.5 UI follow-up: the current WPF run verifies a 12px scrollbar hit area at the right frame edge (within 3px), successful scroll-to-bottom, and no horizontal overflow at 940x660. Current wide/narrow renders were inspected; thin thumb geometry remains fixed on hover.
