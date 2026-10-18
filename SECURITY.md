# Security and alpha limitations

Report vulnerabilities privately to https://t.me/slxvpn?direct. Do not attach live subscription URLs, keys, HWIDs or raw core configurations to public issues.

The desktop runs as the user. SLXVPN is a LocalSystem Windows service because TUN setup changes machine networking.
Only typed plans are accepted; executable paths, core location and config commands cannot be supplied over IPC.
IPC authenticates the caller SID and the client verifies the service PID through SCM before sending credentials.
The first mutation claims control for one Windows SID until service restart. Multiple interactive user sessions prevent connection.
This is not packet-level Windows-user isolation: a newly logged-in second user is detected on the next health check (up to 15 seconds).

Secrets at rest use DPAPI CurrentUser; subscription/core config is not written in plaintext. A local administrator can still read process memory.
Device binding uses this client's own installation ID, not hardware attestation. Never reuse another client's HWID to defeat a server limit.
A link already bound to Happ requires official transfer/reset in your SLX account.

**There is no kill switch in this alpha.** If the core stops or routing disappears, selected applications may fall back to the ordinary internet connection.
Do not use this alpha where preventing every direct packet is required. DNS in selected-app mode uses the shared Windows resolver directly.
LAN, loopback and the VPN endpoint bypass the tunnel. Existing app connections may need a restart/reconnect after changing rules.
Processes running under other Windows accounts/elevated brokers and application helpers need separate validation.

The service accepts only public resolved endpoints, pins a validated address and enforces certificate validation.
Only administrator-owned, administrator-writable Program Files runtime with pinned hashes is executed.
An update installer requires elevation; no silent updater or arbitrary download execution exists.
Neither a unit test nor a successful build establishes Windows system/network acceptance; see docs/VERIFICATION.md.
