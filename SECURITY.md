# Security and alpha boundaries

Report vulnerabilities privately to https://t.me/slxvpn?direct; never attach real subscription URLs, HWIDs, keys or raw core configurations.
The desktop is unprivileged; the LocalSystem service validates typed plans, caller SID, interactive owner and pinned runtime. The UI verifies service PID through SCM. No arbitrary command/config/binary execution over IPC.
A connected intent belongs to its Windows SID across service restart. A second interactive user prevents recovery; protection stays installed. This is not packet-level Windows-user isolation.
User subscription storage uses DPAPI CurrentUser. Resume intent uses DPAPI LocalMachine in a SYSTEM/Administrators-only directory. Raw Xray config stays in memory. A local administrator can read memory or remove firewall policy.

Alpha.5 adds persistent provider-owned WFP protection for exact enabled executable paths, or public whole-computer traffic. Core/service failure preserves blocking outside the owned TUN. LAN/loopback and the service/core recovery path remain available. Only Disconnect or explicit uninstall releases protection. An off-intent survives restart; corrupted/unreadable intent fails service startup without clearing existing rules.
The TUN exception is nonpersistent and tied to its next-hop LUID, rather than the interface holding the source IP. Broad per-user boot-time, existing-flow, raw-packet and IPv6 guarantees are not established by the current acceptance. See docs/NETWORK.md and docs/VERIFICATION.md.
Shared public DNS captured by TUN uses HTTPS through VPN without direct DNS fallback. Local DNS and endpoint bootstrap may remain direct; no per-app DNS anonymity is claimed.

Imports use this installation's own ID; existing Happ bindings require official website transfer. No device-slot or server authorization bypass.
Runtime and Wintun provenance remain pinned; vendor signatures are not changed. Unsigned SLX installer/application are intentional. Updates require Windows elevation; no silent arbitrary downloader exists.
Uninstall stops the service, clears its encrypted intent and removes only its WFP filters. If removal fails, report the failure and repair using the installer; never silently remove foreign firewall rules.
Windows 10, clean-machine, sleep/network, full IPv6/HTTP3 and multi-user acceptance remain open. A successful build alone is not system/security acceptance.
