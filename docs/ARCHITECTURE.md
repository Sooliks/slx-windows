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

Alpha.5: Windows owns native WFP and protected resume storage; Service owns intent/lifecycle and retry. UI expands known application groups locally into ordinary validated exact-path AppRules. Persistent policy survives host/core stop; only explicit off/uninstall releases it. See NETWORK.md.

Alpha.9: desktop initialization reconciles the stored startup preference through the Windows registration owner. Service recovery observations are independent of connection success; the optional IPC field contains only a counter, enum and timestamp. It remains available after recovery, without storing core logs or changing routing/recovery behavior.

Alpha.10: LocalProbe owns authenticated proxy requests; ProbeRequest maps bounded HTTPS results to secret-free observations. HealthConfirmation owns the periodic confirmation policy, independently testable without network mutations. TunnelEngine retains lifecycle/WFP/core ownership and publishes no fresh success during an unresolved probe. Optional HealthCheck is additive to IPC v2; it does not grant any new command or pipe access.
