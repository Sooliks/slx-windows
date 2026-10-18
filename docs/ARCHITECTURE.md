# Boundaries
Desktop owns presentation and localization; Core owns typed contracts, subscription parsing and pure configuration.
Windows owns protected user storage, discovery and pipe transport. Service owns privileged core/network lifecycle.
Desktop cannot select a binary, command, shell, arbitrary core config, system file or service account.
The service validates plans and caller SID and serializes mutations. Server entitlement and HWID remain authoritative.
A second Windows user must not read/control another user's profile. Machine-wide TUN has explicit multi-session limitations.
SLX does not change production servers or publish private web repository content.
Credentials go through authenticated local IPC to memory-only Xray stdin. No plaintext config or raw core logs.
Xray and driver must have pinned provenance. Authenticated localhost proxy is only an internal readiness probe.
Connected means owned core + tunnel readiness + real proxy probe, not process start alone.
