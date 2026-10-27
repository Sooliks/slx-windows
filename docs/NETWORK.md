# Network model and limits — alpha.23

## Alpha.23 ordinary traffic readiness and evidence limits

The friend's alpha.22 report was captured at 2026-10-25T13:55:36Z. It contains an off-state publication from
13:55:15Z and successful startup DNS from 13:54:36Z. That DNS host-route success does not establish ordinary data
routes or packet flow at capture time. Disconnected/Code:none does not identify who requested off or prove a manual
Disconnect: the UI previously skipped polling during a pending mutation while labelling its old snapshot current.
The report does not establish Wi-Fi, ISP/DNS filtering or a specific application's executable as the cause.

Xray's owned autoSystemRoutingTable now uses 0.0.0.0/1 and 128.0.0.0/1, plus ::/1 and 8000::/1, instead of ordinary
/0 defaults. The existing 1.1.1.1/32 and 2606:4700:4700::1111/128 DNS routes remain. A split default outranks a
competing /0 by prefix; more-specific routes and equal-prefix competition still exist. The client does not delete
foreign routes, change physical DNS/interface metrics, disable another VPN or weaken WFP. Xray owns lifecycle.

After authenticated proxy, owned-adapter and protected DNS readiness, launch samples ordinary GetBestRoute2 routes
to fixed representative destinations in both IPv4 halves. It also samples a representative IPv6 route only if an up
interface has a preferred usable public IPv6 source. No usable source means IPv6 is explicitly not checked, not an
assertion of IPv6 success. At most six snapshots and 500 ms between unsuccessful samples share a three-second
budget; cancellation stops acceptance. These are read-only route snapshots, with no packets or DNS queries sent to
those representative destinations. They are not an every-destination, every-app, MTU or real throughput test.

Each required destination in a passing snapshot must select the recorded owned TUN. Failure reports
VPN_TRAFFIC_ROUTE_FAILED independently
from VPN_DNS_ROUTE_FAILED/VPN_DNS_FAILED. First-connect rollback and established-session protection retain their
existing ownership rules. Periodic health requires another ordinary-route snapshot before retaining Connected; a
route failure publishes Reconnecting/ProbePassed=false before awaiting owned cleanup, then uses protected recovery.
The last TrafficRoutes observation survives rollback/off and resets at the next core launch. Help reports only UTC,
fixed sample labels, route outcome and optional numeric native error/prefix/metric, without addresses/interface IDs.

Desktop polls during mutations with one bounded request at a time. Service-read time is distinct from state
ObservedAt and from historical DNS/health/traffic-route times. Generation/revision order replies; clock comparison
cannot authorize a mutation or replay an unknown result. A duplicate UI launch only activates the existing window,
preventing a second model's exit path from accidentally sending off to the shared tunnel.
If a read/unknown marker advanced while a mutation reply was pending, its command outcome is kept separately from its old
snapshot. One bounded fresh read reconciles state within the current operation deadline; failure remains unknown,
and explicit off can supersede/cancel it. No connect/update is resent to obtain a newer observation.

Selected-app identity is separate from ordinary-route readiness. Enabled exact executable paths are canonicalized
for casing through bounded local handle traversal without following reparses; ancestor/file handles prevent rename
until original and canonical native WFP IDs compare equal. Only case/slash differences are accepted. Short names,
reparses, remote/device/remapped aliases fail with TRAFFIC_APPLICATION_PATH_UNSUPPORTED; differing IDs fail with
TRAFFIC_APPLICATION_IDENTITY_MISMATCH. Start, app update, restore and recovery use the same prepared list for WFP,
Xray, revision and desired intent. Whole-PC and disabled rules do not resolve native application identities. This
addresses a separate selected-app mismatch risk, without explaining the friend's whole-PC case or proving real flow.

Installed acceptance remains open: both modes on the friend's and owner's PCs, ordinary IPv4/IPv6 route ownership,
real selected/unselected app traffic and shared DNS, existing-session behavior, route cleanup, foreign-route/DNS
preservation, reboot/sleep/network changes, Windows 10, physical DPI, upgrade/rollback and CS2. Unit/native fixtures
cannot establish all-PC/all-network reliability or successful friend acceptance. STATUS remains PARTIAL.

## Alpha.22 health-check state and evidence

After a failed primary periodic probe, the service now reports Checking with ProbePassed=false. It retains the
owned core/TUN and existing protection while the same bounded confirmation requests run. This state is not a fresh
healthy observation and does not authorize installation, release ownership or remove protection. Explicit Disconnect
remains available. A successful confirmation plus a live owned core and ready TUN restores Connected without changing
ConnectedAt. Actual recovery reports Reconnecting and uses the existing protected stop/relaunch path.

The owner's alpha.21 report contains 10 incidents with 10 retained connections and no Recovery observation. It
establishes confirmation without automatic restart for those recorded incidents; it cannot establish the cause of
the primary failures or a causal link to Steam. Its DNS check is a separate startup observation: the first query
timed out after 3006 ms, and the second succeeded in 62 ms on the owned /32 route with route metric 0. A successful
fallback is not an observed route mismatch, and owner evidence does not establish acceptance on the friend's PC.

Help now includes the primary, repeat and alternate HealthCheck results already held by the service: fixed target
and outcome enums, elapsed time and an optional HTTP status. No raw exception, URL, credential, process path or
packet is exposed. Incident/retained counters and the last incident survive tunnel launches until service restart;
compare timestamps or before/after counter values when investigating an application launch. Old startup DNS results
must not be described as the reason for a later periodic HTTPS failure.

Probe targets, request budgets, the one-second confirmation pause, retry pacing, DNS and WFP rules remain unchanged.
Steam discovery does not start a reconnect. Selected-app changes continue to affect new sessions, so a previously
connected Telegram session can retain its old route; exit that application fully to test a new connection. Shared
Windows DNS still affects unselected applications. See [the alpha.22 guide](UPDATE_ALPHA22_RU.md) for installed checks.

## Alpha.21 DNS route readiness

The friend's alpha.20 diagnostics show three immediate RouteMismatch/OtherInterface results. The service did not
send those DNS queries: the ordinary-route guard rejected the selected Windows interface first. This narrows the
failure to the observed route, without identifying a particular adapter, a persistent competing route or a resolver
outage. The captured Disconnected/Protection=false reports successful rollback of that initial connection attempt.

Typed Xray TUN configuration now includes 1.1.1.1/32 and 2606:4700:4700::1111/128 as well as the existing
0.0.0.0/0 and ::/0. These are the already configured TUN DNS addresses, not additional resolvers. Their host prefixes
prevent a less-specific ordinary route from winning DNS traffic merely by being more specific than a default route.
An equal-prefix foreign route can still compete; the route guard must reject it if Windows chooses another interface.
Creation and cleanup remain with the pinned core's existing owned-TUN route lifecycle. No physical-interface DNS,
interface metric, foreign route, WFP priority or socket binding is changed by the client.

The route may not be ready at the first observation. Within each existing three-second attempt, sample at most six
times with 500 ms between unsuccessful observations, sharing the existing nine-second total budget. Do not count
three immediate rejections as elapsed network attempts. Cancellation stops settling immediately. Only the owned
route permits an unbound UDP send, and the owned route is checked again before accepting a validated DNS response.
Waiting does not waive route ownership or DNS response validation. Expired work cannot confirm a connection.

The existing three attempt observations retain their typed outcomes. A final route failure reports
VPN_DNS_ROUTE_FAILED with distinct RU/EN guidance; DNS transport/response failure retains VPN_DNS_FAILED.
Optional PrefixLength and RouteMetric describe the successfully read native route without exposing its address or
interface identity. RouteMetric is the route's own offset, not the complete route-plus-interface preference.
Failed native reads do not supply fabricated numeric evidence. The result survives initial rollback as before.

Installed acceptance must confirm the host routes on the actual SLX TUN while connected, ordinary-route ownership,
real DNS through the selected VPN, removal of owned routes on Disconnect, and preservation of pre-existing foreign
routes/DNS settings. Both modes, the owner's working PC and the friend's Wi-Fi case remain required. No unit test
proves that alpha.21 works on the friend's machine, and this change does not provide per-app shared-DNS isolation.
See [the alpha.21 rollout guide](UPDATE_ALPHA21_RU.md) and [verification](VERIFICATION.md).

## Alpha.20 DNS readiness correction

Pinned Xray 26.9.9 treats `disableFallback` as selection among configured resolvers. With the previous `true` value and
no per-server domain matches, its empty candidate selection reverted to the first server only; the listed second
HTTPS server was never tried. Alpha.20 uses `disableFallback=false` and `enableParallelQuery=true` for the same exact
Cloudflare and Google HTTPS endpoints. Both are eligible and may be queried concurrently; an available peer can answer
without waiting for a failed peer's four-second timeout. This adds no `localhost`, plaintext DNS or `+local` transport.
The `resolver` inbound still routes through VPN before the self-process bypass, in both routing modes.

The startup DNS check remains an ordinary unbound IPv4 UDP query to the TUN-captured public DNS destination. Before
each send and again before accepting a valid answer, read the unrestricted Windows best route and require the captured
SLX TUN identity. A missing/foreign route cannot send or pass this probe. This also closes a readiness gap in whole-PC
mode, where the service's existing WFP exemption could otherwise permit a physical DNS answer. No WFP priorities,
routes, adapter settings or socket binding are changed. A route snapshot narrows a race; it does not prove every packet.

The complete response must match the query and contain a valid IN/A answer for its owner, as produced by the pinned
Xray DNS outbound; malformed/truncated packets, failed RCODEs and claimed-but-missing answers cannot pass. The three
attempts retain three-second individual and nine-second total budgets, with caller cancellation propagated.
Help diagnostics retain at most three typed results (timeout, socket error, DNS rejection, invalid response or route
failure), numeric error codes and UTC time. They contain no payloads, addresses, adapter identities or credentials.
The result survives a failed first-connect rollback and resets when the next core launch begins.

The friend's alpha.19 screenshot establishes this stage, not its native cause. Resolver selection was reproduced with
the actual pinned core and controlled local resolvers. Actual DoH through HY2/VLESS, Windows routes, the friend's Wi-Fi
and CS2 still require installed testing; shared Windows DNS remains shared.

## Alpha.18 recovery changes

The service accepts concurrent status/off requests while a connect is waiting for readiness. Mutations remain
serialized; only an authorized explicit off can interrupt active work. A failed off is retained and retried with
bounded pacing, rather than leaving protection stranded after a single adapter/BFE cleanup failure.

A definitively failed new connection from confirmed Disconnected/unprotected state rolls back its own attempt.
Successful rollback persists off, waits for the owned process/TUN, removes SLX protection and flushes DNS; the
original failure code remains visible. Cancellation, lost replies, unreadable startup state and established/protected
sessions do not trigger automatic release. Explicit off remains available for an unknown result.

Before each TUN launch, read the Windows best route to the pinned public VPN endpoint and pass that exact interface
alias to Xray. This replaces the pinned core's preference for an available Wi-Fi interface with the Windows-selected
route. A missing/down/loopback/own-TUN mapping produces OUTBOUND_INTERFACE_UNAVAILABLE or TUN_ADAPTER_CONFLICT.
It does not modify another VPN or physical routes. The core still binds outbound sockets to one interface;
distinct IPv4/IPv6 uplinks and ExitLag coexistence remain installed acceptance requirements.

Startup readiness can succeed through either the existing gstatic HTTPS 204 or SLX HTTPS 200 origin; requests still
use the authenticated loopback proxy and validate TLS/status without accepting redirects. TUN/DNS readiness remains
mandatory. Shared public DNS policy is unchanged: unselected CS2/Steam can still depend on that shared resolver.
No observed packet trace establishes a CS2-specific UDP defect or complete transport isolation in this release.

`scripts/repair-network.ps1` performs an explicit administrator off when ordinary control is unavailable. It checks
the installed service path, stops the service, refuses surviving/ambiguous core processes, waits for captured present
PnP adapter identities to disappear, clears only SLX intent/WFP and the DNS cache, then starts the service idle.
It never deletes adapters, force-kills a process, resets the firewall or changes a foreign VPN's settings.

Startup resume also refreshes recognized exact versioned paths before native-ID preflight. One validated application
list feeds WFP and Xray; changed encrypted intent is saved after committed protection. Unresolved paths remain fail-closed.
Configured Windows autostart can send one atomic connect-if-idle after readiness; it never replaces an active/recovering
intent or blindly retries an unknown command. This is source/contract evidence, not a real reboot or packet capture.

The unprivileged UI sends a typed, SID-authorized plan. The LocalSystem service validates public endpoint DNS answers, pins the selected IP and preserves TLS hostname validation. Only pinned Xray 26.9.9 and vendor-signed Wintun execute. No server/Happ/key/HWID changes.

## Traffic selection
A dual-stack TUN handles IPv4/IPv6. Exact full executable paths select VPN; unmatched processes stay direct in selected mode. Whole-computer mode proxies public traffic. LAN/private and loopback remain direct. Application helpers need their own exact paths; the same display names merge into one card/toggle while preserving all exact EXE rules. The known OpenAI.Codex desktop family expands locally into ChatGPT.exe, codex.exe and codex-code-mode-host.exe. No wildcard shell/Node rules or application inventory upload.

Alpha.11 refreshes recognized Discord `Discord/app-*/Discord.exe` and Roblox `Roblox/Versions/version-*/RobloxPlayerBeta.exe` siblings locally before connect/reconnect. Existing selected paths remain included; a removed version can be replaced by an existing sibling, and other existing version executables receive their own exact-path rules. The bounded lookup rejects reparse-point roots/candidates and does not search unrelated applications or accept UNC roots. An unresolved path remains in the plan for service validation; it is never silently omitted to obtain a successful connection. This is a local pre-connect refresh, not a watcher for application updates during an active tunnel.

## Protection and recovery
WFP owns one persistent provider/sublayer, applied atomically before starting the core. At ALE_AUTH_CONNECT_V4/V6, protected applications are blocked except LAN/loopback and the owned TUN next-hop interface. Whole-computer protection exempts only the verified core and network service so they can reconnect. Soft permits do not override other providers' firewall blocks. Only provider-owned filters are replaced/deleted.
The TUN permit is nonpersistent: an interface LUID from an earlier boot must not become an exception for another interface. Persistent blocking rules survive core/service exit; on service startup encrypted intent reapplies the protection before recovery. This is not a promise about packets before Windows BFE has initialized at boot.
Health failure or core exit schedules retries with pauses 2/5/10/20/40/60 seconds, capped at 60, without a total attempt limit. The saved pinned public endpoint enables service recovery without depending on a working shared DNS path. The original hostname is retained in encrypted intent; a validated cached IP is reused for the same endpoint on reconnect. Changing endpoint addresses needs an explicit refreshed connection. Multiple interactive users prevent recovery while protection stays on. SCM also restarts a failed SLX service.
Explicit Disconnect and the narrowly scoped alpha.18 initial-attempt rollback write off-intent and release protection after the owned child/adapter stops. Service shutdown preserves protection and intent. Uninstall and emergency recovery are explicit removal operations and invoke the installed service's restricted release command. Encrypted intent under ProgramData/SLX.NetworkService is SYSTEM/Administrators-only with machine DPAPI; it contains the local plan and owner SID, never plaintext subscription/configuration.
Application-list edits use a separate validated operation and keep the owned core/TUN/ConnectedAt. Effective no-ops
(disabled removal, metadata/order/case changes, toggles returning to the old set) perform no WFP or Xray mutation.
Actual changes first resolve every new native ID, add union protection, persist the desired plan, atomically replace
Xray rules, then remove obsolete app-filter pairs. Unchanged application filters and global DNS/LAN/TUN exemptions
keep their identities. Profile/mode changes and explicit Reconnect still restart; final selected app off disconnects.

The Xray RoutingService has no standalone listener. A fixed destination matches only the existing secret-authenticated
HTTP probe inbound and routes to the internal commander; ordinary TUN/unmatched traffic cannot access it. The service
sends bounded HTTP/2 unary gRPC, with a single-use request body and one connection attempt per mutation. A lost response
causes only a ListRule revision read, never an automatic AddRule replay. Unknown results retain union protection and the
running tunnel; periodic read-only reconciliation can finish a confirmed outcome. A final WFP failure stays unconfirmed
with protection retained; ordinary health recovery or explicit reconnect rebuilds the invalidated native baseline.

New rules choose routes for new sessions. Existing sessions in the changed app may retain their original route until
that app reconnects. No process is killed and other apps are not deliberately closed. Windows ALE policy changes can
reauthorize flows even when unchanged filter keys remain: preserving the core/TUN is not proof of universal packet
continuity. Existing flows, raw packets, IPv6 and network changes require broader installed acceptance.

Alpha.11 resolves the complete enabled application-ID set with Windows before opening the filter-replacement transaction. A selected executable removed by an application update returns `TRAFFIC_APPLICATION_MISSING`; Windows access denial returns `TRAFFIC_APPLICATION_DENIED`. Other native failures return `TRAFFIC_PROTECTION_<OPERATION>_<8HEX>` with an owned operation name and numeric Windows result. Operations cover engine open, provider/sublayer, transaction begin/commit, enumeration/deletion, IPv4/IPv6 filter add, application identity and adapter lookup. Bounded `APPLICATION_INVALID`/`ADAPTER_INVALID` validation codes contain no native text. No code includes executable paths, keys, URLs or raw Windows error messages. A rejected preflight leaves existing blocking filters untouched.

Replacement/removal enumerates only the SLX provider on its two ALE layers, includes disabled filters and additionally checks the SLX sublayer before deletion. Foreign providers, their filters and adapters are not deleted. The persistent blocking and nonpersistent TUN-permit model is unchanged. A failed native session is closed without removing persistent protection; the next attempt opens a new BFE RPC session. A recoverable restore failure no longer prevents the control pipe and monitor from starting: a loaded desired-on plan retains its intent and enters the existing paced retry path, while an unreadable intent produces a fault instead of an invented connection result. These changes do not establish boot-time or BFE-restart packet guarantees.

The 4096-entry replay receipt limit rejects new connect/reconnect operations inside a 10-minute retention window; after that window receipts expire (previously they lived for the whole service lifetime, so a long uptime blocked every new connection until restart). Valid explicit disconnect remains available at capacity without adding another receipt; existing request fingerprints are checked first, and invalid off payloads remain rejected. Ownership is released only after a successful explicit stop reports Disconnected with protection inactive. Failed, protected or uncertain stop results retain the owner. A subsequent single Windows user can then establish their own connection; concurrent multi-session routing is still unsupported.

If startup successfully reads no saved on-intent but owned WFP cleanup fails, startup-only paced cleanup retries
without starting a tunnel. The service reports SERVICE_STARTUP_CLEANUP_PENDING until removal succeeds and then
confirms Disconnected. Unreadable intent never enables this cleanup. New connection protection supersedes it;
an explicit Stop cancels it, including a failed stop. This does not add a boot-time packet guarantee.

## DNS — alpha.26
Windows shares its DNS service across applications and does not expose the original app caller to these routing rules.
SelectedApplications therefore routes the same HTTPS resolver peers directly, independently of VPN-server health, and
does not install a global public port53 deny. While core/TUN is absent, ordinary Windows DNS is available to unselected
programs; selected exact EXE public-data blocks remain. This explicitly trades VPN-confined shared DNS for isolation
of unselected applications from VPN outages. It does not promise selected-app DNS anonymity. App-owned encrypted DNS
continues to follow its exact process route. No system DNS address or foreign provider filter is changed.

WholeComputer retains HTTPS DNS through VPN and the persistent global port53 deny outside LAN/TUN. Same two peers,
TLS validation, parallel fallback and readiness budgets remain. In selected mode the DNS readiness response now proves
the direct shared resolver path reached through the owned local TUN, not a VPN-outbound DNS exchange. HTTP readiness
through the authenticated probe still uses VPN and ordinary data routes remain checked independently. Diagnostics say
which shared DNS policy applies when current mode is known; absent historical mode cannot be inferred from preferences.

Selected-only remote HTTPS failures retain a healthy owned core, adapter and routes for at most90 monotonic seconds,
checking again with five-second gaps. Checking with ProbePassed=false remains unconfirmed; no healthy state is invented.
This avoids destroying unselected direct sessions merely because the remote VPN briefly disappeared. Successful checks
retain ConnectedAt; resource/route/native-policy failure and grace exhaustion still use protected core recovery.
Core restart necessarily can interrupt direct sessions traversing TUN; universal packet continuity is not claimed.
The service flushes the Windows resolver cache and performs a real DNS exchange before Connected. Owner-reported YouTube NXDOMAIN was reproduced on plain DNS; the new DoH path returned valid answers on both live protocols. No physical DNS settings are persisted.

## Acceptance
Previously recorded alpha.5 Windows 11 evidence covers isolated selected/unselected IPv4 TCP/UDP block, TUN permit, policy persistence, explicit removal; installed service-stop block and automatic resume; three forced core exits; HY2 and VLESS connectivity. One first DNS datagram timed out after the initial candidate restart, motivating the final readiness probe. This historical evidence is not a fresh alpha.11 installed-system result. Detailed evidence is in VERIFICATION.md.
Alpha.11 source verification passed; the read-only native app-ID regression reproduces Windows results 2/3 for a removed executable/version directory. The friend's exact cause remains unconfirmed. No actual alpha.11 installed-service, reboot or ExitLag-coexistence test was performed, and no production or live network configuration was changed.
Full IPv6/ECH/HTTP3, existing flows, sleep/network changes, clean VM, Windows 10, multi-user packet isolation and boot-time guarantees remain open. Do not describe these checks as full network/security acceptance.

## Primary references
- https://github.com/XTLS/Xray-core/tree/v26.9.9
- https://github.com/XTLS/Xray-core/blob/v26.9.9/app/router/router.go
- https://github.com/XTLS/Xray-core/blob/v26.9.9/app/commander/commander.go
- https://learn.microsoft.com/en-us/windows/win32/fwp/ale-re-authorization
- https://xtls.github.io/en/config/dns.html
- https://learn.microsoft.com/en-us/windows/win32/fwp/filtering-condition-identifiers-
- https://learn.microsoft.com/en-us/windows/win32/fwp/filter-arbitration
- https://microsoft.github.io/CsWin32/
- https://learn.microsoft.com/en-us/windows/win32/api/fwpmu/nf-fwpmu-fwpmgetappidfromfilename0
- https://learn.microsoft.com/en-us/windows/win32/api/fwpmtypes/ns-fwpmtypes-fwpm_filter_enum_template0
- https://learn.microsoft.com/en-us/windows/win32/fwp/object-management

## Alpha.10 health confirmation
A failed periodic request is not proof that the core or every VPN route is broken. The service immediately clears ProbePassed while retaining the owned core/TUN and existing WFP policy. Through alpha.21 this stage displayed Reconnecting; alpha.22 distinguishes it as Checking. After a one-second pause it makes independent fresh HTTPS requests via the same authenticated proxy to gstatic generate_204 (exact 204) and SLX robots.txt (exact 200), concurrently with the existing five-second timeout each. Redirects and TLS errors are not accepted. Either valid response, plus a still-live owned core and ready TUN, restores success without changing ConnectedAt. If neither passes, the core is replaced with the existing protected recovery procedure. Core/adapter loss does not wait for confirmation. Startup readiness accepts the authenticated HTTPS origins described in the alpha.18 section and still requires owned TUN/DNS readiness.
Optional HealthCheck IPC diagnostics retain only the last failed-primary incident and cumulative incident/retained counters until service restart. They contain enum categories, status codes, elapsed milliseconds and UTC time; no messages, credentials, URLs or app data. A caller cancellation propagates and is never accepted as health success.
