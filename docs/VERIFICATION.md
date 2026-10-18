# Verification — 0.1.0-alpha.1

STATUS: PARTIAL. Windows 11 x64 alpha; build/test checks do not establish completed system acceptance.

## Recorded checks, 2026-10-18

- dotnet restore Slx.Windows.slnx --locked-mode: exit 0 after explicit dependency/lock update.
- dotnet build Slx.Windows.slnx -c Release --no-restore: exit 0, zero warnings/errors.
- dotnet test Slx.Windows.slnx -c Release --no-build --no-restore: 93 passed, zero failed/skipped.
- Tests cover URI parsing, strict transport/TLS policy, generated configs, local DPAPI, concurrent ID creation, HTTP budgets/redirects, IPC framing and dispatcher replay/ownership contracts.
- Official Xray 26.9.9 configuration parser: six positive configurations accepted (HY2/XHTTP/REALITY, selected/whole); two malformed/unsupported configurations rejected. No network adapter was started by parser checks.
- Desktop --render-check: five current WPF PNGs rendered and visually inspected (RU olive, narrow applications, settings, EN Terminal help, import).
- RU/EN: 125 matching resource keys; source size checks pass.
- Official Xray archive pinned SHA256; exact core version checked; Wintun Authenticode Valid.
- Inno Setup 6.7.3: unsigned self-contained setup compiled successfully.
- High-confidence source secret-pattern scan: no matches. New repository contains no web/server history or real subscription data.

## System acceptance

scripts/acceptance-tun.ps1 defaults to review-only. After review, an administrator can run -Run.
It creates a temporary named adapter, one benchmark host route plus its connected private subnet, two probe EXEs and localhost sinks.
It does not install SLXVPN, use live keys, alter production or add default routes.
This only proves IPv4 TCP/UDP process classification if actually executed and passed; parser/build tests do not substitute for it.
Actual administrator run: PASS. Two distinct executable paths sent real IPv4 TCP and UDP packets through the owned Wintun adapter; selected and other paths reached their respective synthetic sinks. Both probes exited 0. Default routes were unchanged; cleanup reported no remaining adapter, address or route. Sanitized evidence: docs/evidence/tun-classifier.json. The localhost-only fixture disables outbound physical-interface binding; production retains auto. Background broadcasts are excluded from the test sinks.

Still required before stable release:
actual SLXVPN installation/control and UI-driven import; live HY2 and XHTTP traffic; selected/unselected application egress;
TCP/UDP/IPv6 and DNS behavior; sleep/resume, Wi-Fi changes, crash and cancelled mutation recovery;
multi-user OS-level rejection; high-DPI/multiple monitors and keyboard/UI Automation;
clean VM install, upgrade, repair, uninstall and route/adapter cleanup.

No full kill switch exists. No packet-level Windows SID isolation is claimed.
The first release remains a prerelease regardless of unit-test count.
