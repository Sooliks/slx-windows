# Network model and limits — alpha.18

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

## DNS
Windows shares its DNS service across applications. Public port 53 captured by TUN routes to Xray DNS, whose HTTPS resolvers (literal 1.1.1.1 / 8.8.8.8, normal certificate validation) route via VPN before self-process bypass. There is no plain UDP/system DNS fallback in this path. This also affects unselected applications using the shared public resolver while connected. Private/LAN DNS and initial bootstrap resolution remain direct; public port 53 is blocked outside TUN during recovery, including the shared Windows resolver. This can pause DNS for unselected apps while VPN is recovering; app-owned HTTPS DNS follows its process rule. Selected mode is not per-app DNS anonymity.
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
A failed periodic request is not proof that the core or every VPN route is broken. The service immediately clears ProbePassed and shows Reconnecting while retaining the owned core/TUN and existing WFP policy. After a one-second pause it makes independent fresh HTTPS requests via the same authenticated proxy to gstatic generate_204 (exact 204) and SLX robots.txt (exact 200), concurrently with the existing five-second timeout each. Redirects and TLS errors are not accepted. Either valid response, plus a still-live owned core and ready TUN, restores success without changing ConnectedAt. If neither passes, the core is replaced with the existing protected recovery procedure. Core/adapter loss does not wait for confirmation. Startup readiness remains the original strict 204/TUN/DNS path.
Optional HealthCheck IPC diagnostics retain only the last failed-primary incident and cumulative incident/retained counters until service restart. They contain enum categories, status codes, elapsed milliseconds and UTC time; no messages, credentials, URLs or app data. A caller cancellation propagates and is never accepted as health success.
