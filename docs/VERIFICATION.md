# Verification — 0.1.0-alpha.2

STATUS: PARTIAL. Windows 11 x64 prerelease. The checks below do not constitute full system/security acceptance.

## Executed on 2026-10-18

- `pwsh -File scripts/verify.ps1`: exit 0; locked restore, Release build with zero warnings/errors, 118 unit/Windows tests and one populated WPF UI scenario passed; zero failed/skipped. Authored source size contract passed.
- RU/EN: 145 matching resource keys; all literal XAML localization keys resolve.
- Ten native pipe contracts cover protected ACL, caller SID, service PID, invalid frames, delayed readers, thirty reconnects, bounded missing ACK and preserving a known reply when its ACK fails.
- Native PnP checks recognize the present physical adapter and reject a nonexistent GUID; invalid identities fail closed.
- Owned adapter cleanup tests cover delayed removal, cancellation, bounded timeout and preserving unrelated adapters. Background cleanup retains faults without stopping local control.
- Four synthetic production-emitter cases retain both HY2 and VLESS, including base64/CRLF and Cyrillic names; insecure HY2 fails the entire import rather than being silently filtered.
- Populated WPF checks cover profile templates, whole/selected modes, narrow layout, themed tray, unknown exit, fixed geometry for hover/focus, quick-app search/add and startup refresh success/failure/stale completion.
- Actual WPF renders under artifacts/ui were inspected. They contain synthetic data and are layout evidence only.
- The earlier testhost FailFast appeared during test teardown. The harness now owns a complete dispatcher lifecycle and joins its STA thread. An isolated rerun exited 0 with no remaining new testhost or new matching WER/.NET/ApplicationError events. Exact original native cause was not recovered.

## Installed candidate evidence

The owner had uninstalled alpha.1; this is fresh candidate installation followed by candidate replacement, not clean alpha.1 upgrade acceptance.

- Inno candidate installation/replacement exited 0; the actual SLXVPN LocalSystem service reached Running.
- Twelve sequential real status requests passed after ACK v2 was installed. Actual UI polling subsequently showed authoritative disconnected/connected states.
- Candidate with the final tunnel/presence logic: actual Hysteria2 and VLESS each connected and completed two explicit reconnects. Every result was Connected with ProbePassed=true.
- ProbePassed means a real HTTPS 204 through the authenticated local proxy and a ready dual-stack TUN owned by the pinned core. This alone does not prove selected/unselected application egress isolation.
- Both protocols then disconnected successfully; default route snapshots before/after were equal. No present SLX adapter remained after stop.
- A stale local cache contained only VLESS. A fresh response for this installation contained HY2 and VLESS. Automatic startup refresh was then observed in the installed UI with both methods available.
- Actual native UI showed correct profile names, the new background and inline quick selection. Manual .exe selection remains available.
- Native known-stop exit completed with zero UI processes, Disconnected state and no present adapter. Separately, explicit unknown-stop app-only exit terminated UI while leaving the service running, as disclosed.
- Final packaging uses a reviewed clean commit; provenance/checksums identify the exact installer and source. Published alpha.1 assets are not overwritten.

## Root causes and boundaries

The service previously disconnected a named pipe before the desktop consumed its reply. A delayed-reader OS test reproduced the loss. ACK v2 bounds reply acknowledgement before disconnect; failure to send an ACK after a complete reply does not make that known result unknown. UI and service must update together.

Windows retains detached adapter records in GetAdaptersAddresses even after the PnP device is gone. Presence now checks read-only SetupAPI DIGCF_PRESENT; a disabled but present adapter is still a conflict. Reconnect waits only for its recorded owned GUID. It never deletes another device or disables the conflict guard.

References:
- https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdigetclassdevsw
- https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream.runasclient?view=net-10.0
- https://github.com/XTLS/Xray-core/blob/v26.9.9/proxy/tun/tun_windows.go

## Historical alpha.1 scope

Alpha.1 had 93 passing unit/contracts, six valid core-parser configurations and two rejected invalid configurations.
An isolated two-EXE native IPv4 TCP/UDP classifier run used localhost synthetic sinks and restored routes/adapters.
It did not prove live VPN encryption, production DNS, IPv6 or selected/unselected public egress.
Historical sanitized evidence: docs/evidence/tun-classifier.json.

## Not verified

Independent selected/unselected application public egress; live UDP/IPv6 and DNS isolation;
sleep/resume, Wi-Fi changes, crash/cancelled-operation recovery; OS multi-user rejection;
all DPI/multiple-monitor/screen-reader cases; clean VM install, alpha.1 upgrade, repair/uninstall.
Windows 10 is not claimed. No full kill switch or packet-level Windows SID isolation is claimed.
Selected-app DNS uses the shared Windows resolver directly. Installation-ID first initialization across separate Windows sessions remains unverified.

Production servers, Happ, DB/device records and subscription keys were not changed.
