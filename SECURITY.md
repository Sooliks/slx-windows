# Security and alpha boundaries

Report vulnerabilities privately to https://t.me/slxvpn?direct; never attach real subscription URLs, HWIDs, keys or raw core configurations.
The desktop is unprivileged; the LocalSystem service validates typed plans, caller SID, interactive owner and pinned runtime. The UI verifies service PID through SCM. No arbitrary command/config/binary execution over IPC.
A connected intent belongs to its Windows SID across service restart. A second interactive user prevents recovery; protection stays installed. This is not packet-level Windows-user isolation.
User subscription storage uses DPAPI CurrentUser. Resume intent uses DPAPI LocalMachine in a SYSTEM/Administrators-only directory. Raw Xray config stays in memory. A local administrator can read memory or remove firewall policy.

Alpha.5 adds persistent provider-owned WFP protection for exact enabled executable paths, or public whole-computer traffic. Core/service failure preserves blocking outside the owned TUN. LAN/loopback and the service/core recovery path remain available. Explicit Disconnect, uninstall or administrator emergency recovery releases protection. Alpha.18 also rolls back a definitively failed first connection that started from confirmed off, after its owned cleanup succeeds. This exception never applies to an established/protected session, unreadable startup state, cancellation or an unknown request result. An off-intent survives restart; corrupted/unreadable intent leaves control available without clearing existing rules.
The TUN exception is nonpersistent and tied to its next-hop LUID, rather than the interface holding the source IP. Broad per-user boot-time, existing-flow, raw-packet and IPv6 guarantees are not established by the current acceptance. See docs/NETWORK.md and docs/VERIFICATION.md.
Shared public DNS captured by TUN uses HTTPS through VPN without direct DNS fallback. Local DNS and endpoint bootstrap may remain direct; no per-app DNS anonymity is claimed.

Alpha.18 keeps status authorization independent of slow network work, while mutations stay serialized. Four bounded
pipe instances retain the original ACL and first-instance ownership. Only SID-authorized, payload-validated and
replay-checked explicit off can cancel another operation. Incomplete off cleanup is retried and cannot be replaced by
a new connect. The recovery script requires elevation, verifies the fixed installation/service path, refuses ambiguous
remaining core identities, waits for actual adapter PnP removal and invokes the installed provider-scoped release
command. It does not kill processes, delete adapters, reset the firewall, change physical DNS or clear subscriptions.

Imports use this installation's own ID; existing Happ bindings require official website transfer. No device-slot or server authorization bypass.
Runtime and Wintun provenance remain pinned; vendor signatures are not changed. Unsigned SLX installer/application are intentional. Updates require Windows elevation; no silent arbitrary downloader exists.
Alpha.25 stages an unprivileged updater and elevates only the verified native installer. Fixed typed maintenance
GUIDs, protected durable service state and the installed admin-only claim command exclude tunnel start during file
replacement. Neither IPC nor result receipts accept executable paths or commands. The same-repository HTTPS hash
is integrity evidence, not independent signing. See docs/UPDATES.md for cancellation, expiry, partial-install and
one-time resume boundaries; clean-machine/UAC/power-loss acceptance remains open.
Native Inno clears the pinned runtime's startup-hook, alternate runtime/root/GC/JIT and profiler environment before
managed preflight/service actions, and restricts PowerShell module/PATH resolution to Windows system directories.
Uninstall stops the service, clears its encrypted intent and removes only its WFP filters. If removal fails, report the failure and repair using the installer; never silently remove foreign firewall rules.
Windows 10, clean-machine, sleep/network, full IPv6/HTTP3 and multi-user acceptance remain open. A successful build alone is not system/security acceptance.
