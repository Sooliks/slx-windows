# Network model and limitations

The UI creates a typed ConnectPlan. The Windows service validates it again, validates all current DNS answers for the remote endpoint, and supplies a generated configuration to the pinned Xray core in memory. No server keys, production profiles or raw configurations are part of this repository.

## Supported subscription profiles

UTF-8 URI lists and base64-encoded UTF-8 URI lists are accepted. Supported profiles are Hysteria2 over TLS with h3, VLESS/XHTTP over TLS with h2 and stream-one, and legacy VLESS/REALITY over RAW/TCP. Unsupported transports, insecure certificate flags, duplicate/unknown query parameters and private literal endpoints fail closed. XHTTP fp=unsafe means native TLS; certificate and hostname validation remain enabled. Profile IDs are SHA-256 of the URI; they do not contain the original secret.

TLS certificate checks are never disabled. REALITY requires its public key. Entitlement, expiry, active device slots and HWID binding remain enforced by the subscription server; importing a Happ-bound URL is not a device transfer.

## Traffic selection

A dual-stack TUN receives IPv4 and IPv6 traffic. Xray automatically binds its outbound sockets to the physical interface. Local/private destinations and the core process bypass the proxy. The authenticated localhost readiness probe routes through the VPN before process rules. Selected application paths are exact full executable paths, not a process-name wildcard; disabled entries do not match. Xray's process matcher is case-sensitive, so paths should be obtained from the running process or native file picker.

Selected-app mode routes selected processes to VPN and unmatched or unattributed traffic directly. This is not a fail-closed kill switch. Child processes with a different executable, browser helpers, service-hosted traffic and existing connections are not automatically attributed to the parent. Select the relevant helper executable and restart existing application connections when changing the list.

Whole-computer mode routes public destinations through VPN; local/private network access remains direct. New connections only are classified. There is no per-app bandwidth/latency claim.

## DNS

Windows DNS Client may resolve for multiple applications from the same shared service. Selected-app mode routes port 53 directly, including DNS requests from selected applications. Consequently the alpha does not promise per-app DNS isolation or anonymity. Applications using their own HTTPS DNS follow their own process rule. Whole-computer mode routes public DNS over VPN, but physical-interface/bootstrap resolution of the VPN hostname is necessarily outside the tunnel. Local DNS/mDNS and LAN destinations remain direct. DNS and UDP behavior require real acceptance tests; configuration syntax is not proof of absence of leaks.

## Recovery and isolation

Only one machine-wide tunnel can be owned at a time. Its user SID is the control owner; a different interactive user must not take over or read credentials. No network change should outlive its owned core/service lifecycle. Adapter cleanup after kill, sleep, network change and uninstall must be verified on a Windows VM before calling the release stable. Native IPv4 TCP/UDP process classification passed an isolated two-EXE test. Whole-computer routing, real VPN egress, IPv6 and lifecycle behavior remain unverified until system acceptance passes.

## Upstream references

- [Pinned Xray TUN parser](https://github.com/XTLS/Xray-core/blob/v26.9.9/infra/conf/tun.go)
- [Pinned Hysteria parser](https://github.com/XTLS/Xray-core/blob/v26.9.9/infra/conf/hysteria.go)
- [TUN interface routing](https://xtls.github.io/en/config/inbounds/tun.html)
- [Process routing syntax](https://xtls.github.io/en/config/routing.html#ruleobject)
- [XHTTP transport](https://xtls.github.io/en/config/transports/splithttp.html)

No production website, Moscow/Amsterdam server, Happ installation, HWID record or existing subscription was changed by developing this client.
