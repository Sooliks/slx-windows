# Browser connectivity — alpha.3 candidate

STATUS: PARTIAL. 2026-10-18. No production server, Happ, subscription or device changes.

Owner reports Telegram works, while YouTube closes connections and Twitch player reports #2000.
Restarting Yandex, whole-computer routing and disabling ExitLag did not resolve it.
Happ with the same SLX service works. A second browser also failed in the owner's check; its exact selected-app rule was not established.

## Executed diagnostics

Isolated EXEs, real installed LocalSystem service, installation-owned credentials only in memory.
Selected HTTPS trace exited through the expected Amsterdam address; unselected HTTPS exited in RU.
HTTP/2 image (30,320 bytes) and 1 MiB HTTPS transfer passed. Both YouTube host variants returned HTTP 200.
Selected UDP STUN had both expected-exit and unexpected-exit observations across runs; process-specific UDP isolation is not accepted.
In whole-computer mode, both probe processes exited through Amsterdam, including STUN.
HTTP/3 timed out on Google and Cloudflare through both live protocols; the unselected direct control passed.
A separate localhost UDP forwarder using the pinned Xray and no TUN also failed QUIC handshakes.
Therefore these HTTP/3 failures alone do not establish a TUN defect. No blanket UDP block was added.

A controlled TCP localhost forwarder targeted a public IPv6 address of www.youtube.com.
Without destination restoration: TLS ended with IOException for HY2 and VLESS.
With TLS SNI restoration: both received HTTP/1.1 200 OK with normal certificate validation.
The authenticated localhost health probe did not detect this difference because it supplies a hostname.

## Candidate change

Only TLS destination restoration on public IPv6 TUN destinations is enabled.
All IPv4, local/reserved IPv6 and the known cloudflare-ech.com outer name retain the original destination.
The payload remains end-to-end TLS encrypted; no certificate interception or disabled validation.
No QUIC override, global multiplexing, browser security changes, DNS hijack or system-wide IPv6 disabling.
This mitigates the demonstrated SNI/IPv6 case; it does not provide general IPv6 egress or full ECH compatibility.
Installed candidate upgraded successfully (installer exit 0, file version 0.1.0.3), restored HY2 selected mode and authoritative Connected/ProbePassed=true. The owner then confirmed that both YouTube and Twitch playback work. This acceptance covers that observed browser failure, not general IPv6/ECH/HTTP3 support.

References:
- https://github.com/XTLS/Xray-core/blob/v26.9.9/infra/conf/xray.go
- https://github.com/XTLS/Xray-core/blob/v26.9.9/proxy/tun/handler.go
- https://xtls.github.io/en/config/inbound.html
