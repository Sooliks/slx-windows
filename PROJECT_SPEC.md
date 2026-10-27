# SLX Windows

Version: 0.1.0-alpha.26. STATUS: PARTIAL until required Windows acceptance passes.

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

## Alpha.26 — selected-app recovery must not disable shared DNS

Owner authorizes immediate production-channel release after mandatory checks. Alpha.25 owner diagnostics show two
ProbeFailed recoveries, latest00:08:35 UTC27Oct, Connected00:09:27, and current healthy state at00:13:58. These times
do not prove the server rebooted or measure every packet outage. Code establishes shared public DNS was VPN-routed
and globally blocked outside TUN in selected mode, so unselected name resolution depended on VPN recovery.

In SelectedApplications only, remove the global port53 WFP deny while retaining all selected exact-EXE blocks and
existing LAN/TUN exemptions. Route the same shared HTTPS resolver peers directly; probe inbound remains VPN-only.
WholeComputer DNS route/global protection stay unchanged. Windows shared DNS cannot identify its original caller:
selected mode now explicitly allows shared name resolution outside VPN, including physical Windows DNS while TUN
is absent. App-owned encrypted DNS follows its process rule. Do not claim DNS anonymity or replace system resolvers.

For selected mode remote-only health failures, retain a still-owned healthy core/TUN/routes and existing direct flows
for a bounded monotonic90-second grace, using Checking/ProbePassed=false and five-second check spacing. Success
retains ConnectedAt; core/adapter/route/native-policy loss immediately recovers normally. Explicit off, replacement
and successful health reset grace. No indefinite stuck-core deferral, fake Connected or protection release is allowed.
Keep IPCv2 wire shape unchanged for the installed alpha.25 updater's strict result decoder. DNS diagnostics distinguish
the shared-resolver policy from a local OwnedTunnel route. No server/website/DB/live-machine network mutation.

## Alpha.25 — integrated updates with tunnel ownership

Owner authorizes commit/push and release through the existing production update channel. Preserve alpha.24 network,
discovery, application identity, runtime and storage contracts. Add a small unprivileged WPF updater staged outside
the installation. Background download cannot disconnect VPN; default active behavior is deferral. Separate automatic
installation consent defaults false, with a cancellable 15-second off-only countdown. Explicit active update requires
confirmation and confirmed owned off. Service-owned typed GUID maintenance excludes reconnect during replacement and
survives service restart. No arbitrary update command/path or privileged downloader is exposed over IPC.

Only the verified native installer gets fixed silent/no-reboot arguments after hash/MOTW locking. After UAC, a fixed
installed service CLI claims the same maintenance session before Stop. Observe real exit, matching app/service versions,
fresh service/off state and maintenance release before success. Reboot, cancellation, mixed versions and unknown result
are distinct. No atomic rollback/power-loss guarantee. Consume bounded DPAPI result before a single same-config resume
through connect-if-idle. Settings/subscription keep their stores; checking does not imply installation consent. Update
UI/service together. Alpha.24 needs one ordinary bootstrap; alpha.25 integrates later upgrades. See UPDATES.md.

## Alpha.24 — practical library order and resilient local state

Use one presentation-only ordering policy for cached, progressive, final, searched and source-limited application
lists: browsers, Telegram, Discord, recognized games/platforms, then the rest. Rank before the 300-item source/final
limit; keep the 600-row transient bound and stable card objects. Installed exact rules precede explicit Find EXE
recommendations. Classification is a display heuristic, never native identity, routing permission or proof of installation.
Discovery cannot alter saved selections, toggle states or send a VPN control mutation. Unknown custom games remain
searchable/manual additions without generic executable-name rules or inventory upload.

Subscription HTTP work is separate from a tunnel mutation. A confirmed off state during refresh keeps Connect labelled
correctly and disabled until work finishes; an active tunnel retains explicit Disconnect, which cancels the subscription
request and fences its late result. Unknown tunnel outcomes continue to offer safe explicit off. Update-install admission
cannot overwrite a newer accepted service read; a newer confirmed off can admit installation, while active/unknown and
failed reads reject it. A confirmed active reply remains current, rather than being relabelled as a transport failure.

Validate bounded preferences and saved-subscription structure before UI use. Load their independent stores separately.
Unreadable preferences remain untouched and block subsequent writes in that store instance, including automatic startup
reconciliation; show a persistent Settings warning about session-only changes. No automatic reset or migration is added.
Preserve a valid installation ID when explicitly replacing a malformed subscription snapshot. Startup refresh's own
timeout and a skipped live-state response identify cached profiles; explicit cancellation does not invent a network error.

Retain the existing typed protection, cancellation, recovery, service ownership and exact executable identity rules.
During automatic recovery and restored intent, refresh only recognized Discord/Roblox sibling layouts through held
local no-reparse directory/executable handles; fully prepare the proposed plan before adopting current/pinned intent.
Disabled exact paths remain unchanged and cannot be enabled by sibling expansion. On a failed native application
protection commit, clear the claimed revision and schedule full protected recovery of the authoritative before/after
plan. Never replay an AddRule or publish healthy success from the same health tick after protection reconciliation failed.
Current results and any additional recovery correction belong in VERIFICATION.md and versioned release notes.
The owner has authorized Codex commit/push and publication through the existing Windows workflow. Source base is exact
v0.1.0-alpha.23 at 85b514ffc317c92c14e34153bfbdc611cdebfe34; website/server/DB and live-machine settings are out of scope.

## Alpha.23 — ordinary traffic routes, live observations and local application identity

The friend's alpha.22 report captured 25 October 2026 at 13:55:36 UTC contains Disconnected, protection/probe false,
Code:none and state publication at 13:55:15 UTC, with a successful owned DNS /32 observation from 13:54:36 UTC.
It does not establish current ordinary application routes, a resolver failure, a Wi-Fi cause or manual Disconnect.
Desktop previously suspended status reads during mutations while retaining the old snapshot's current flag; the
report may describe that earlier snapshot. Do not claim this explains the friend's actual packet failure.

Replace the owned Xray default entries with 0.0.0.0/1, 128.0.0.0/1, ::/1 and 8000::/1; retain the two existing DNS
host routes. Xray owns their creation/removal. No foreign-route deletion, physical DNS/metric change or WFP weakening.
Launch readiness reads ordinary Windows routes for representative destinations in both IPv4 halves, plus IPv6 only
when an up interface has a preferred usable public IPv6 source. Allow at most six snapshots, 500 ms between failed
samples, within a three-second budget. The snapshots send no packets and do not test every destination or application.
Unconfirmed ordinary routes report VPN_TRAFFIC_ROUTE_FAILED and cannot publish Connected. Periodic health also
requires ordinary-route ownership; route failure invalidates success before owned cleanup and protected recovery.
Optional IPC v2 TrafficRoutes holds only bounded route enums, numeric errors/prefix/metric and UTC observation time.

Keep one independent status request in flight during UI mutations, using the existing ten-second status timeout and
four-slot service transport. Mutation generation plus local observation revision reject obsolete replies/errors;
timestamps never order operations. Starting a mutation invalidates the previous snapshot until another accepted
read. Help separates last accepted service-read time, pending UI action/start time and service state publication;
DNS/health/route observations retain their own timestamps. An uncertain mutation is never automatically replayed.
If a read or unknown marker advanced the local revision while awaiting a mutation reply, retain its command Success/Code
separately and discard the reply's older status snapshot. Fence pending reads, invalidate freshness and perform one
bounded fresh status read linked to the current operation deadline. Failed reconciliation stays unknown; superseding
off changes generation/cancels it. Clear the pending-action label once its command result is known, even if the fresh
read is still pending. Neither an old reply nor wall-clock comparison can restore a claimed success.

One UI instance per Windows user/session owns a local mutex until App.Exit. Repeated manual launch sends only a
show-window event; duplicate --tray stays quiet. Neither duplicate constructs a model or sends service commands.
--render-check bypasses this guard. Registered waits and handles are disposed; no elevated activation channel exists.
Installed-app discovery parses DisplayIcon's final unquoted numeric index without truncating commas inside paths.
Known Codex/ChatGPT package paths refresh for the current user before connect/reconnect, retaining unresolved exact
intent for validation. No generic executable-name matching, forced application termination or inventory upload.
For enabled selected-app rules, resolve casing through a bounded local-drive, relative-handle walk without following
reparse points. Hold ancestor/file handles against rename until original and canonical native WFP application IDs
match. Permit only case/slash changes; reject short-name, reparse, device, remote or remapped aliases with
TRAFFIC_APPLICATION_PATH_UNSUPPORTED, and mismatched native IDs with TRAFFIC_APPLICATION_IDENTITY_MISMATCH.
Use the same canonical list for WFP, Xray, desired intent and revision at start, app update, restore and recovery.
Whole-PC and disabled rules require no native application resolution. This separate selected-app defect does not
establish the cause of the friend's whole-PC failure; native fixture results do not substitute for installed traffic.

UI and service install together; dependency/driver, stored-plan, server and database contracts remain unchanged.
The source/archive base is the exact published v0.1.0-alpha.22 tag at f694b51c8a815ce041fe67b26c8e21627aaa5cda.
Current source preparation is not publication or installed acceptance. Deliver changed/full source archives and
commands for the owner's commit
and push to main; the existing Windows CI owns verification, installer build and prerelease publication. Record actual
current results in VERIFICATION.md, separately from the owner's historical alpha.22 report of 538 checks plus WPF.

## Alpha.22 — distinguish health confirmation from tunnel recovery

The owner's alpha.21 diagnostics captured 25 October 2026 show Connected/Protection=true/ProbePassed=true,
10 health incidents and 10 retained connections, with no Recovery observation. All recorded failed-primary health
checks retained the running tunnel after confirmation; this does not identify why those primary requests failed or
attribute them to opening Steam. The last incident predates the capture. The separate startup DNS check timed out
once on an owned /32 route (metric 0), then succeeded in 62 ms; the owner's result does not establish the friend's fix.

Append TunnelPhase.Checking without changing prior enum values. A failed primary periodic check publishes this
phase with ProbePassed=false while the existing core/TUN and traffic protection remain owned. Successful confirmation
returns Connected with the original ConnectedAt; an actual recovery enters Reconnecting before replacing the core.
Keep disconnect and controller ownership correct in Checking. The interface must distinguish unconfirmed health
from a healthy connection and from recovery, using RU/EN copy without changing status-card geometry or themes.
Existing probe targets, timeouts, confirmation pause, retry pacing, WFP, DNS and selected-app routing are unchanged.

Help diagnostics print the existing HealthCheck primary/repeat/alternate observations: bounded enum target/outcome,
elapsed milliseconds and optional HTTP status. Do not print raw exceptions, URLs, credentials, app paths or traffic.
The counters describe service-lifetime observations, not just the latest connection; compare timestamps and deltas.
App switches still apply to new sessions; existing Telegram sessions can retain their old route until that app exits.
No Steam-start reconnect hook or forced process/session termination is introduced.

UI and service must be installed together because Checking is a new status value in the existing IPC v2 contract.
There is no new action, stored-data, dependency, driver, server or database migration. Source/release base is the exact
published alpha.21 tag. Distribution remains an authorized prerelease with PARTIAL installed-system acceptance;
alpha.21 CI results are historical evidence, not proof of alpha.22 tests or publication.

## Alpha.21 — explicit DNS routes and bounded route readiness

The friend's alpha.20 diagnostics captured 25 October 2026 show all three DNS attempts as RouteMismatch,
OtherInterface and 0 ms. The ordinary route guard rejected them before sending DNS; this does not establish a
Hysteria2/DoH outage or identify the competing adapter. Disconnected/Protection=false is the reported completed
first-connect rollback. The owner reports a working PC; friend/Wi-Fi/CS2 acceptance remains open.

Add only the existing configured TUN DNS destinations as host routes: 1.1.1.1/32 and
2606:4700:4700::1111/128, alongside the existing IPv4/IPv6 defaults in typed Xray autoSystemRoutingTable.
The pinned core owns creation and cleanup on the owned TUN, using its existing route lifecycle. This prevents a
less-specific route from outranking the DNS path by prefix; it does not promise to override a competing host route.
No new resolver, physical-adapter DNS/metric change, foreign route deletion, socket binding or WFP change.
The IPv4 readiness probe still uses ordinary Windows routing and requires the owned TUN before sending and after
the validated DNS answer. Neither a proxy health response nor a forced-interface answer substitutes for shared DNS.

Allow bounded route settling inside the existing three-second attempt/nine-second total budgets: at most six
route samples with 500 ms between unsuccessful samples. Cancellation interrupts the wait; expired readiness cannot
publish Connected. Up to three attempt observations remain, rather than retaining every route sample. A final
unconfirmed DNS route reports VPN_DNS_ROUTE_FAILED separately from DNS response/transport failure. Protection and
confirmed-first-attempt rollback retain their existing ownership rules.

Optional numeric PrefixLength/RouteMetric extend only the existing optional DnsCheck route observation. Native
reads must not invent a prefix/metric after a failed lookup; route metrics are not the full interface-plus-route
selection cost. Safe diagnostics include these numbers without names, identifiers, addresses or raw native text.
Update the service and UI together; no stored-data, IPC version, driver, external dependency or server migration.
Alpha.21 uses the exact published alpha.20 tag as its release/source-archive base. Production distribution remains
the authorized prerelease channel with PARTIAL acceptance; no claimed friend success or publication before evidence.

## Alpha.20 — resilient protected DNS and precise readiness evidence

Owner supplied a friend's alpha.19 screenshot: selected-app Hysteria2, Discord enabled, VPN_DNS_FAILED and off state.
That code occurs after the authenticated HTTPS/TUN checks; the screenshot does not establish the failed DNS transport.
The pinned Xray 26.9.9 resolver selection was reproduced independently: disableFallback=true with bare server entries
uses only the first resolver. Enable fallback among the exact two configured HTTPS peers and race equivalent peers,
so the first resolver's four-second timeout cannot consume a three-second readiness attempt. Both remain routed through
the VPN; no system/plain DNS fallback or new provider is added. Both resolvers failing still rejects readiness.

Keep the real unbound IPv4 UDP probe, three attempts/one nine-second budget and explicit caller cancellation. Observe
the ordinary Windows route before sending and before accepting a positive answer; require the captured owned TUN GUID.
Do not force the socket onto TUN to hide a broken shared route. No route, WFP, adapter or physical-DNS mutation is added.
Validate the complete bounded DNS response, question, flags and actual IN/A answer for the queried owner, as emitted by
the pinned Xray DNS outbound. A header claiming answers is insufficient. Route snapshots are not packet-level proof.

Optional IPC v2 DnsCheck retains at most three typed attempt results: fixed target/outcome enums, elapsed time, RCODE,
numeric socket/native-route errors and UTC time. No raw packets, exception text, queried names, interface IDs or server
addresses are exposed. Preserve this observation after failed-connect rollback, reset it for the next core launch and
include it in Help's safe diagnostics. Give DNS failures their own RU/EN copy. Installation updates service/UI together;
old statuses without DnsCheck remain readable. No stored-data, driver, dependency or server migration. The existing
fresh-connect rollback and established-session protection rules remain intact. Real friend/Wi-Fi/CS2 acceptance is open.

## Alpha.19 — bounded update checks on slow connections

Owner diagnostics on 2026-10-24: the exact public GitHub release feed returned HTTP 200 with 56/60 API requests
remaining, but curl timed out at 15 seconds after receiving 126432 of 149771 bytes. The checksum URL returned
302 then 200. This establishes a body-transfer timeout in that direct request, not an API quota failure; the old UI
did not expose the exact updater failure. The alpha.17 parser accepts the published alpha.18 metadata/checksum.
Request gzip for the release catalog only, decode within both encoded/decoded resource bounds, and accept identity
when compression is not used. Preserve the same exact repository, newest-20/SemVer selection, HTTPS and asset checks.
Use a bounded 30-second check budget covering the feed and checksum; caller cancellation remains cancellation.
Expose only localized whitelisted timeout/invalid-data messages. Unknown codes retain generic copy; no raw upstream
body, exception, signed CDN query, subscription or installation identity is shown or logged. Download/install consent,
prepared-offer preservation and the network-service installation gate stay intact. No proxy/transport route migration.
Alpha.18 remains immutable; alpha.19 uses its exact tag as the source-archive/release base. A client stuck on alpha.17/18
may need one explicit manual alpha.19 installer update before benefiting from the new updater. No server, dependency,
IPC, storage, network-service or driver change. Real slow-network and installed upgrade acceptance remain required.

## Alpha.18 — responsive control and failed-connect recovery

Owner report 2026-10-24: two friends cannot connect; SERVICE_UNAVAILABLE, prolonged lost Internet after an error,
and an unselected CS2 session affected by selected-app VPN. Screenshots establish symptoms, not each native cause.
Keep four bounded authenticated pipe instances, with a first-instance anchor and unchanged protected ACL.
Read status under a short ownership lock; serialize mutations and allow validated explicit off to interrupt readiness.
Failed explicit off retains a paced owned cleanup retry, suppressing reconnect until complete. An unknown UI state
keeps explicit Disconnect reachable, including after a first-connect lost reply or without a saved subscription.
Only a definitively failed new connect from confirmed off rolls back its new intent/core/WFP policy and returns the
original failure code with confirmed off. Cancellation/unknown outcomes and existing protected sessions stay protected.
Read Windows GetBestRoute2 for the pinned endpoint before each new TUN and pass the exact chosen interface to Xray.
Reject the owned TUN identity, unresolved/down/loopback mapping; do not change foreign routes or adapter settings.
Startup HTTPS readiness accepts either existing strict authenticated-proxy origin; DNS readiness remains mandatory.
Shared public Windows DNS remains VPN-protected and can affect unselected applications. Do not claim per-app DNS
isolation, verified CS2 UDP behavior, or ExitLag coexistence from unit tests. The interface now explains this scope.
Add explicit administrator repair-network.ps1 for dead/unavailable service recovery; it releases only SLX intent/WFP
after confirmed service/core/adapter stop, and never resets the Windows firewall or deletes a foreign adapter.
Update UI/service together with the alpha.18 installer. No IPC schema, stored-data or dependency version migration.
Synchronize stale UI lock files with the existing ProtectedData dependency. Source build/portable tests are recorded
separately from pending Windows native/WPF/installed acceptance in docs/VERIFICATION.md and UPDATE_ALPHA18_RU.md.

## Alpha.17 — stable layout for transient activity

Owner report 2026-10-23: "Please wait…" / loading copy was inserted for ~0.5 s and made the interface jump.
Transient copy no longer inserts rows: the status card keeps fixed geometry (two reserved detail lines and one status
line shared by the service code and activity copy; a reported code wins). Activity copy appears only after 400 ms and,
once shown, stays at least 700 ms (Presentation/Presence), so short operations never flash text; it is Hidden, never
Collapsed, so space stays reserved. Pending application changes show inline beside the "Added apps" title instead
of a notice that pushed the list. Messages float bottom-right above the page with a close button; confirmations
(imported, updated, diagnostics copied) clear themselves after 4 s, warnings and errors stay until closed or replaced.
Library loading and the import dialog reserve their status lines. Presence never invents or extends a state: it
only delays and holds the visibility of copy the view model reports. Not covered: the update download progress row
(floating banner and Updates settings) still appears with the download.

## Alpha.16 — switch-only app choice and receipt expiry

Owner feedback 2026-10-23: the "Use VPN / No VPN" caption beside each app switch is unnecessary; the switch alone
shows the saved choice (names get the freed width; the switch keeps its accessible name and the RouteChoiceHint).
Owner report: after a long service uptime VPN could not be reconnected until the PC was restarted. Confirmed defect
with that symptom: the dispatcher's replay receipts were never removed, so after 4096 mutations every connect,
reconnect and app update returned IPC_RECEIPT_LIMIT until the service restarted. Receipts now expire after 10 minutes;
the 4096 burst cap, fingerprint/replay checks inside the window and explicit-off-at-capacity remain. The UI never
resends a request ID, so a request older than the window is equivalent to a new authenticated request. Whether this
was the owner's actual cause is not established: the visible code (Help → copy diagnostics) distinguishes it from
adapter/core faults such as TUN_ADAPTER_CONFLICT or TUN_CLEANUP_PENDING. Service and UI update together by installer;
no IPC shape, storage or dependency change. Rollback: install alpha.15 (the old unbounded-lifetime cache returns).
Owner request: an installer of the offered version that is already staged is not downloaded again. Download and
update checks reuse a file only inside our protected GUID staging folders and only if it still matches the published
size and SHA-256; Internet-zone metadata is restored if missing. Launch re-verifies as before; damaged or foreign
files are ignored and a fresh download follows. Render checks/UI fixtures never read the real staging folder.

## Alpha.15 — site-aligned design and native motion

Owner request of 2026-10-23: align every Windows surface with the current slxvpn.com design system, add tasteful
native animation, improve UX and fix confirmed stability defects; Codex owns review, integration and publication.
Owner explicitly allows motion now. This supersedes the alpha.11 "no ambient timers/decorative animation" wording for
interface feedback only: no loop, fake activity, traffic graph, ping or speed is introduced, and service health polling
is unchanged. Motion is one-shot (140/180/220/360/650 ms, site cubic-bezier(0.2,0.7,0.2,1)); interface feedback uses
Opacity and RenderTransform. The route scene redraws for at most 650 ms after a saved choice changes, without a
settled repaint loop. Motion never defines state and applies its final values when Windows animations are off,
High Contrast is on, the window is hidden/minimized, or in render checks/UI fixtures; active clocks are stopped too.
Tokens follow site tokens.css/themes.css: olive-black neutrals, lime accent, washes/lines, glass panels; Terminal is
black/white, square corners and mono. Faces: Geologica/IBM Plex Sans/JetBrains Mono when installed, otherwise native
Segoe UI in Olive and Cascadia Mono/Consolas in Terminal; no font binaries are bundled. Ideal/Grayscale rendering, Medium body,
13px minimum secondary text and the 204/76px adaptive sidebar remain. The background is the site route scene as a
static drawing that restates the saved mode and selected-app count only; it eases once after a change.
Adjacent fixes: subscription removal with an unconfirmed service state now explains why the link stays instead of
silently keeping the dialog open; non-persistent render/test models never clear the stored subscription; the empty
settings freshness dash is hidden; page scroll position survives navigation; window controls share centered glyphs,
stable hit targets, distinct close feedback and correct maximize/restore state; the tray tooltip states the confirmed
status and the tray menu offers Connect when allowed. No network, IPC, stored-data format, service or dependency change.
Rollback: revert the Desktop/UiTests diff; there is no data or IPC migration.

## Alpha.14 — live application routing

Owner requested application changes without a global reconnect, including removing an already disabled application.
Add SID-authorized typed IPC v2 action update-applications; preserve full validation, strict fingerprints, receipts,
limits and session checks. It cannot change profile/mode, start an idle tunnel or silently fall back to reconnect.
Effective equality ignores metadata, disabled rows, ordering and path case; service currentPlan is authoritative.
No-op changes only persist the revised intent. Explicit reconnect remains a forced restart.
Pinned Xray 26.9.9 atomically replaces all routing rules through RoutingService. The API has no new listener: only the
existing authenticated loopback probe can route a fixed control destination to the internal commander. Share typed
rule generation, bound gRPC/protobuf and never store plaintext configuration. Resolve all new native app IDs first.
WFP adds union protection before changing routes, then removes only changed app-filter pairs; DNS/LAN/TUN exemptions
and unchanged app-filter identities remain. Save desired intent before core mutation. Uncertain acknowledgement keeps
union protection and reconciles by reading the core revision, never by replaying AddRule or restarting the core.
Optional confirmed ApplicationRevision reconciles a UI target; ordinary Connected does not prove an applied app list.
Failed app updates retain the tunnel and show scoped recovery. Natural health recovery remains independent.
App edits coalesce; profile/mode changes reconnect, and disabling the final selected app explicitly disconnects.
Changes affect new sessions; an affected app's existing session may need that app to reconnect. Windows may reauthorize
ALE flows after policy changes: do not promise universal flow continuity without installed packet acceptance.
Update UI/service together; old services reject the additive action with no reconnect fallback. No server, DB, account,
provider-ID, credentials or dependency migration. Production distribution remains authorized with PARTIAL/prerelease.

## Alpha.13 — resilient icons and update notification

Owner reported persistent fallback icons for installed Yandex/TeamSpeak and requested clearer update UI. Read-only
probing confirms those EXEs exist and contain usable icon resources; the initial transient failure in the live app has
not been reproduced. Correct permanent negative caching, bounded group-candidate fallback and native icon extraction
while retaining two workers, frozen images, dispatcher delivery and weak lifetime. Resource extraction never executes
the application; Shell fallback balances explicit COM initialization. One bounded delayed retry may repair a transient
miss for a live consumer; no perpetual icon polling. Presentation candidates must never change selected routing paths.
Update notification floats above the workspace at the lower right, consumes no page-layout row and never steals focus.
It stays below import/remove modal overlays, clamps to the workspace, and exposes dismissal even during download
without cancelling transfer. Update notification stays nonmodal with explicit version, transfer progress, safe retry and clear install availability.
Current version, available/downloaded state and reminder remain reachable in Settings. Preserve separate download and
install actions, SHA/size validation, UAC and fresh-service-status mutation lease; never disconnect or install silently.
Late transfer progress cannot overwrite a later operation. No dependency, service, network, account or data migration.
Distribution continues through the authorized production GitHub channel, preserving prerelease/PARTIAL acceptance.

## Alpha.12 — responsive application library

Returning users see the previous local discovery snapshot before rescanning. Persist only bounded validated exact-path
AppRules/timestamp in a separate CurrentUser-DPAPI cache owned by UserSettingsStore; no account, profile, installation-ID
or server migration. Missing/corrupt cache is optional and must not prevent preferences/subscription use.
Independent discovery sources run with concurrency capped at three and publish completed batches without waiting for
the slowest source or icons. Keep previous results usable during refresh/failure; only a complete successful scan prunes
old rows and replaces the durable snapshot. Distinguish saved content, active request and verified observation time.
Automatic revisits reuse a recent successful in-process scan; manual Refresh remains available. Search, selected rules,
scroll/focus and navigation remain usable during background discovery. Revalidate cached paths before explicit Add;
discovery/cache alone never enables an application or triggers routing changes. Selected paths still use existing
explicit addition and connection-time repair/validation. Cancellation/disposal reject late results and duplicate scans.
Shell icon extraction is asynchronous, coalesced and capped at two; frozen fallback displays immediately, no native
extraction under a shared cache lock or on the UI thread. Preserve SLX themes, routing semantics and all service guards.
Distribution follows the previously authorized production GitHub channel with prerelease/PARTIAL acceptance status.
Rollback: install alpha.11 after explicit disconnect; the optional discovery cache is ignored by older clients.

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
The owner additionally requested publishing alpha.18 through GitHub so the site can consume the release.
Windows release automation may run the existing verification/build/package/publish sequence on main version bumps
or explicit manual dispatch. Its token path is limited to this repository, main and the exact workflow commit;
local owner authentication and all clean-source/provenance/hash/immutable-release guards remain required.
Hosted Windows checks do not establish real VPN, Windows 10, CS2 or the friends' machine acceptance.
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
