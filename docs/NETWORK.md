# Network model and limits — alpha.5

The unprivileged UI sends a typed, SID-authorized plan. The LocalSystem service validates public endpoint DNS answers, pins the selected IP and preserves TLS hostname validation. Only pinned Xray 26.9.9 and vendor-signed Wintun execute. No server/Happ/key/HWID changes.

## Traffic selection
A dual-stack TUN handles IPv4/IPv6. Exact full executable paths select VPN; unmatched processes stay direct in selected mode. Whole-computer mode proxies public traffic. LAN/private and loopback remain direct. Application helpers need their own exact paths; the same display names merge into one card/toggle while preserving all exact EXE rules. The known OpenAI.Codex desktop family expands locally into ChatGPT.exe, codex.exe and codex-code-mode-host.exe. No wildcard shell/Node rules or application inventory upload.

## Protection and recovery
WFP owns one persistent provider/sublayer, applied atomically before starting the core. At ALE_AUTH_CONNECT_V4/V6, protected applications are blocked except LAN/loopback and the owned TUN next-hop interface. Whole-computer protection exempts only the verified core and network service so they can reconnect. Soft permits do not override other providers' firewall blocks. Only provider-owned filters are replaced/deleted.
The TUN permit is nonpersistent: an interface LUID from an earlier boot must not become an exception for another interface. Persistent blocking rules survive core/service exit; on service startup encrypted intent reapplies the protection before recovery. This is not a promise about packets before Windows BFE has initialized at boot.
Health failure or core exit schedules retries with pauses 2/5/10/20/40/60 seconds, capped at 60, without a total attempt limit. The saved pinned public endpoint enables service recovery without depending on a working shared DNS path. The original hostname is retained in encrypted intent; a validated cached IP is reused for the same endpoint on reconnect. Changing endpoint addresses needs an explicit refreshed connection. Multiple interactive users prevent recovery while protection stays on. SCM also restarts a failed SLX service.
Only explicit Disconnect writes off-intent and releases protection after the owned child/adapter stops. Service shutdown preserves protection and intent. Uninstall is an explicit removal and invokes the installed service's restricted release command. Encrypted intent under ProgramData/SLX.NetworkService is SYSTEM/Administrators-only with machine DPAPI; it contains the local plan and owner SID, never plaintext subscription/configuration.
Changing app rules reconnects automatically; a short protected pause is expected. Existing flows, raw packets, IPv6 and network changes require broader acceptance; matching configuration and successful new IPv4 sockets do not prove every Windows flow is leak-free.

## DNS
Windows shares its DNS service across applications. Public port 53 captured by TUN routes to Xray DNS, whose HTTPS resolvers (literal 1.1.1.1 / 8.8.8.8, normal certificate validation) route via VPN before self-process bypass. There is no plain UDP/system DNS fallback in this path. This also affects unselected applications using the shared public resolver while connected. Private/LAN DNS and initial bootstrap resolution remain direct; public port 53 is blocked outside TUN during recovery, including the shared Windows resolver. This can pause DNS for unselected apps while VPN is recovering; app-owned HTTPS DNS follows its process rule. Selected mode is not per-app DNS anonymity.
The service flushes the Windows resolver cache and performs a real DNS exchange before Connected. Owner-reported YouTube NXDOMAIN was reproduced on plain DNS; the new DoH path returned valid answers on both live protocols. No physical DNS settings are persisted.

## Acceptance
Windows 11: isolated selected/unselected IPv4 TCP/UDP block, TUN permit, policy persistence, explicit removal; installed service-stop block and automatic resume; three forced core exits; HY2 and VLESS connectivity. One first DNS datagram timed out after the initial candidate restart, motivating the final readiness probe. Detailed final evidence is in VERIFICATION.md.
Full IPv6/ECH/HTTP3, existing flows, sleep/network changes, clean VM, Windows 10, multi-user packet isolation and boot-time guarantees remain open. Do not describe these checks as full network/security acceptance.

## Primary references
- https://github.com/XTLS/Xray-core/tree/v26.9.9
- https://xtls.github.io/en/config/dns.html
- https://learn.microsoft.com/en-us/windows/win32/fwp/filtering-condition-identifiers-
- https://learn.microsoft.com/en-us/windows/win32/fwp/filter-arbitration
- https://microsoft.github.io/CsWin32/

## Alpha.10 health confirmation
A failed periodic request is not proof that the core or every VPN route is broken. The service immediately clears ProbePassed and shows Reconnecting while retaining the owned core/TUN and existing WFP policy. After a one-second pause it makes independent fresh HTTPS requests via the same authenticated proxy to gstatic generate_204 (exact 204) and SLX robots.txt (exact 200), concurrently with the existing five-second timeout each. Redirects and TLS errors are not accepted. Either valid response, plus a still-live owned core and ready TUN, restores success without changing ConnectedAt. If neither passes, the core is replaced with the existing protected recovery procedure. Core/adapter loss does not wait for confirmation. Startup readiness remains the original strict 204/TUN/DNS path.
Optional HealthCheck IPC diagnostics retain only the last failed-primary incident and cumulative incident/retained counters until service restart. They contain enum categories, status codes, elapsed milliseconds and UTC time; no messages, credentials, URLs or app data. A caller cancellation propagates and is never accepted as health success.
