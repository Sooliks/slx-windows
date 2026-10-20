# Verification — 0.1.0-alpha.11 candidate (2026-10-20)

STATUS: PARTIAL. `pwsh -NoProfile -File scripts/verify.ps1` outside the restricted sandbox exited 0:
locked restore, Release build with 0 warnings/errors, 264 unit/Windows checks and one expanded WPF scenario,
0 failures/skips, <=200 nonblank source gate. The first sandbox invocation exited 1 at locked restore because
the existing user NuGet.Config was inaccessible. The same gate was rerun with ordinary host access, not weakened.
An intermediate repeat had one success-fixture download hit its artificial 2s test deadline during real ACL/ADS disk I/O;
263 checks and the WPF scenario passed. Ordinary fixture budgets now use 15s; dedicated 100ms timeout/cancellation cases
and production 15s check / 5min download limits remain unchanged. The final complete rerun after this correction exited 0:
264 checks plus the expanded WPF scenario, zero failures/skips and zero build warnings/errors.
After the owner's denser Full HD typography request, the complete `scripts/verify.ps1` rerun also exited 0:
264 unit/Windows checks plus one WPF scenario, zero warnings/errors/failures/skips. Current 1200x820/940x660
WPF snapshots were re-rendered and inspected, including sidebar/routing text-fit assertions, status, update banner,
long app names, import, Help and settings. This is 96-DPI rendered evidence, not a physical 27-inch monitor measurement.

A temporary isolated .NET console called the real ReleaseUpdateClient against public GitHub for installed alpha.9:
exit 0, Available / 0.1.0-alpha.10 / checksum validated / no error. Only metadata/checksums were read; no installer launch.
`scripts/build.ps1 -Candidate` exited 0 and checked official Xray 26.9.9 SHA-256 and Valid vendor Wintun signature.
That intermediate candidate predates the final update-handoff review and is not a final-binary acceptance claim.

New evidence covers native existing/missing application ID behavior (read-only), stage-code preservation,
preflight rejection before policy mutation, explicit-off receipt capacity, ownership release only on confirmed off,
versioned Discord/Roblox exact-path repair, public update metadata/SemVer/HTTP bounds, staging ACL/MOTW/hash/launch locking,
and UI update states/install blocking. Updater launch fixtures do not execute an installer or UAC.

`--render-check` via a hidden, isolated desktop process exited 0. Current WPF snapshots were inspected:
1200x820 and 940x660 sidebar/connection/update offers, fault detail, long app names, narrow library/loading,
EN settings, import modal and Terminal Help. artifacts/ui contains actual current generated PNGs.
These are WPF renders/fixtures, not a real imported subscription, browser E2E or installed VPN proof.

The friend's screenshot and report establish alpha.10 TRAFFIC_PROTECTION_FAILED after reboot/Connect.
Removed version paths reproduce one failure mechanism; the exact friend's native error and ExitLag conflict remain unknown.
No installed service, live VPN, BFE/firewall/adapter, production account, server or DB mutation was performed.
Required open acceptance: installed service/UI, real TUN/egress/isolation, actual reboot/ExitLag coexistence,
installer upgrade/UAC/cancel/rollback, clean VM, Windows 10, boot/sleep/network changes, full IPv6,
multi-user packet isolation, DPI/High Contrast and live accessibility/input.

# Verification — 0.1.0-alpha.10

STATUS: PARTIAL. On 2026-10-19 scripts/verify.ps1 exited 0: locked restore, Release build with zero warnings/errors, 193 unit/Windows checks and one WPF scenario; no failures/skips, source size passes. New cases cover first success, primary/alternate confirmation permutations, cancellation, exact HTTP codes, safe error categories and retained diagnostic facts. These fixtures do not prove a working VPN.

On the installed alpha.9, read-only status recorded Recovery Count=3 / ProbeFailed at 14:18:21Z, ConnectedAt 14:18:23Z. The owned Xray process was created at the same recovery time while the SLX service had started earlier at 14:12:42Z. This confirms automatic core replacement after a failed probe, not the underlying reason for that probe failure. No rule changes or intentional kills were performed for this observation.

The candidate installer exited 0, file version 0.1.0.10; the saved installation identity hash matched before/after. The actual LocalSystem service resumed the saved Hysteria2 selected-app connection with ProbePassed=true and ProtectionActive=true, ConnectedAt 14:26:51Z. A bounded read-only three-second sampler records core PID, authoritative status and safe diagnostic observations. At 14:29:06Z the same PID and ConnectedAt remained; no failed-primary incident had occurred yet. This is a short observation, not proof of long-term stability or the Pixel sleep case. The later final installer also needs its own acceptance; candidate evidence is not a final-binary claim.

Windows 10, clean VM, full IPv6, boot/sleep/network-change and packet-level acceptance remain open. No server/Happ settings or credentials change.
# Verification — 0.1.0-alpha.9

STATUS: PARTIAL. On 2026-10-19 scripts/verify.ps1 exited 0: locked restore, Release build with zero warnings/errors, 175 unit/Windows checks and one expanded WPF scenario; no failures/skips, source size passes. Five new isolated HKCU fixture cases cover missing/obsolete startup repair, opt-out, unrelated-value preservation and unsafe host/command rejection. Two recovery observation checks cover old IPC compatibility and diagnostic facts that do not manufacture connection success. RU/EN resources include a dedicated startup-registration error. Candidate installer exited 0 and installed file version 0.1.0.9. With the saved opt-in true and real Run entry absent, launching the installed EXE with --tray restored exactly the quoted installed path plus --tray. Shell_NotifyIconGetRect found one registered icon with its owning window hidden. The Xray PID stayed unchanged across this UI start; real service status remained Connected/ProbePassed=true and the installation ID hash matched before/after. This is startup-command acceptance, not an actual Windows sign-out/reboot. New recovery observations are unit/IPC checked; a natural failure on alpha.9 is not yet observed.

# Verification — 0.1.0-alpha.8

STATUS: PARTIAL. RU/EN help-copy correction only; network/IPC/storage unchanged from alpha.7.
168 unit/Windows checks and one expanded WPF scenario passed via scripts/verify.ps1; source size and locked restore passed.
Owner confirmed CS2/Rust with icons in installed alpha.7, and Claude grouping/right-edge scrollbar in alpha.5.
Historical native evidence and open system acceptance remain below.

# Verification — 0.1.0-alpha.7

STATUS: PARTIAL. Windows 11. scripts/verify.ps1: locked restore/Release build, 168 unit/Windows tests and one expanded WPF scenario; no skips/failures, source size passes.
Local Steam discovery returned Counter-Strike 2 (cs2.exe) and Rust (RustClient.exe, Rust.exe), including a second drive.
Twelve new cases cover URL allowlisting, ambiguous/argument rejection, VDF unescaping, traversal rejection and two-library exact EXE resolution.
No game, Steam protocol handler or anti-cheat was launched; gameplay/network behavior is not accepted by discovery tests.
The shared remote catalog stays at 18 entries. Steam discovery is local and does not upload app lists.
Network/service/WFP source unchanged from alpha.5; its native evidence remains below. Alpha.6 loading indicator and tests are retained.

# Verification — 0.1.0-alpha.6

STATUS: PARTIAL. Windows 11; Windows 10 and complete system acceptance remain open.
UI-only follow-up to published alpha.5. scripts/verify.ps1 exit 0: locked restore, Release build,
156 unit/Windows tests and one expanded WPF scenario, no skips/failures, zero warnings/errors, source size passes.
The blocked-first-discovery WPF scenario renders a visible indeterminate progress indicator before completion,
hides empty-state copy, disables refresh, then verifies success and retained results/message after failure.
No real network is used by this UI fixture. Icons are warmed on the worker task as frozen cached sources.
Network/service/WFP/IPC/storage source is unchanged from alpha.5; native evidence below belongs to alpha.5.

# Verification — 0.1.0-alpha.5

STATUS: PARTIAL. Actual Windows 11 execution; Windows 10 target remains unaccepted.

- scripts/verify.ps1 exit 0: locked restore, Release build, zero warnings/errors; 156 unit/Windows checks and one WPF scenario; no skips/failures; source size contract passes.
- Real pinned Xray isolated DNS A/B: plain DNS returns RCODE 3 / zero answers for youtube.com and www.youtube.com; DoH via HY2 and VLESS returns RCODE 0 / positive answers. No second TUN or server edits; configuration/credentials remain in memory.
- Local discovery observed one registered OpenAI desktop group, three exact executable paths and 77 readable shortcut targets. Counts only; inventory is not published or uploaded.
- Isolated real WFP two-EXE IPv4 TCP/UDP: before both pass; selected block fails both while control passes; owned TUN next-hop permit restores selected; persistent block survives engine close; explicit Remove restores both. Local-interface condition was insufficient; final code uses IP_NEXTHOP_INTERFACE.
- Installed candidate: setup exit 0, file version 0.1.0.5. Connected/ProbePassed/ProtectionActive true; YouTube resolves. Owner confirmed YouTube works and Codex / ChatGPT appears.
- Installed service stop: selected fresh IPv4 TCP and UDP fail, unselected control passes. Service start restores encrypted plan and protection. Three forced owned-core exits recover independently with ProtectionActive/ProbePassed true; VLESS also connects. Explicit off releases protection; original HY2/app plan restored.
- Final installed rerun after DNS readiness: first selected TCP/UDP probes pass after resume; three more forced core exits recover with protection, HY2/VLESS pass. Already-open selected TCP/TLS and UDP sockets pass before stop and fail while service/TUN is stopped. In whole-computer mode both test EXEs pass connected and both fail while stopped. Explicit off restores both; original HY2 plan restored. Public DNS is also blocked for the unselected control during recovery, as required by the shared resolver protection.
- Matching-name WPF scenario verifies one Claude card, two retained paths, both switched together and one card after settings reconstruction. Owner confirmed YouTube and OpenAI group in the earlier candidate.
- SCM restart policy confirmed 3/10/30 seconds. Persistent filters and machine-DPAPI resume state are owned locally; explicit uninstall releases only SLX policy.

Open: Windows 10/clean VM, full IPv6/ECH/HTTP3, existing flows, sleep/Wi-Fi/interface changes, reboot-before-BFE, multi-user packet isolation and all DPI/a11y combinations. Current native new-IPv4 socket evidence is not full packet-level/no-leak acceptance. Historical releases below retain their original limitations.

# Verification — 0.1.0-alpha.4

STATUS: PARTIAL. Windows 11 x64 prerelease. Historical alpha.3 and alpha.2 evidence remains below.

## Alpha.4 executed on 2026-10-18

- scripts/verify.ps1: exit 0; locked restore, Release build with zero warnings/errors, 128 unit/Windows checks and one expanded WPF scenario; zero failed/skipped.
- 152 matching RU/EN keys. WPF fixture renders of dashboard facts and catalog inspected at 940x660: switches remain present when disabled, long names wrap, facts fit, library uses outer page scrolling.
- Catalog parser rejects paths/commands and malformed schemas. Embedded fallback contains 18 entries. Discovery does not upload installed/running programs.
- Temporary IPv6 source-address experiment restored its change automatically. Owner confirmed ChatGPT/Claude opened during the experiment.
- Installed alpha.4 candidate: installer exit 0, file version 0.1.0.4, authoritative Connected/ProbePassed=true. Owned adapter address reports SkipAsSource=true automatically after connection.
- Owner confirmed both ChatGPT and Claude open after ordinary candidate connection, after the temporary test had restored. This is end-user evidence, not packet-level acceptance.
- Service change touches only its owned TUN address via native IP Helper. The UI and service remain unprivileged/privileged respectively; no physical-interface or global IPv6 changes.
- Website catalog deployment can follow later; embedded catalog remains available on 404/network/invalid response.

Open acceptance: process-specific UDP isolation, HTTP3, general IPv6/ECH, DNS isolation, sleep/network transitions, clean VM, multi-user and all DPI/accessibility combinations.
No kill switch. No server, Happ, secret, device-binding or schema changes in this client release.

# Verification — 0.1.0-alpha.3

STATUS: PARTIAL. Windows 11 x64 prerelease. Historical alpha.2 checks remain below.

## Alpha.3 executed on 2026-10-18

- scripts/verify.ps1: exit 0; locked restore, Release build, zero warnings/errors, 120 unit/Windows checks plus one expanded WPF scenario; zero skipped/failed.
- 146 matching RU/EN resource keys. Source <=200 nonblank lines and git diff --check passed.
- New WPF checks cover icon library wrapping/fallback/cache/manual picker, automatic coalesced app updates, last-app disconnect, explicit-stop cancellation and no retry after an unknown reply.
- Real local forwarder A/B: IPv6 YouTube TLS failed without SNI restoration and received HTTP/1.1 200 OK with the exact candidate exclusions on both HY2 and VLESS. Credentials/configuration stayed in memory; no server edits.
- Candidate installer exit 0, file version 0.1.0.3. The installed service restored HY2 selected mode with Connected/ProbePassed=true. Owner confirmed YouTube and Twitch both work afterward.
- Selected fresh-EXE TCP exit matched Amsterdam; unselected control was RU. Whole-mode TCP and STUN matched Amsterdam. Selected UDP had inconsistent exit observations and is not accepted as isolated.
- Forced HTTP3 timed out through both protocols and a separate localhost UDP forwarder; direct control passed. No blanket UDP block or unsupported HTTP3 success claim.
- IPv6 TLS restoration is a compatibility mitigation, not general IPv6 egress. IPv4/LAN destinations remain unchanged; known cloudflare-ech.com is excluded. General ECH acceptance remains open.
- Automatic rule application uses one bounded reconnect after debounce. Existing sockets can need application reconnection. It is not a seamless routing API update.
- Browser automation could not be used because native browser policy support was unavailable. Browser outcome is explicitly owner-reported, not automated E2E evidence.

Details: [network diagnosis](NETWORK_DIAGNOSIS.md). Sleep/network changes, clean VM, multi-user, full IPv6 and UDP isolation remain unverified. Kill switch is not implemented.

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

Alpha.5 UI follow-up: the current WPF run verifies a 12px scrollbar hit area at the right frame edge (within 3px), successful scroll-to-bottom, and no horizontal overflow at 940x660. Current wide/narrow renders were inspected; thin thumb geometry remains fixed on hover.
