# Verification — 0.1.0-alpha.23 (2026-10-25)

STATUS: PARTIAL. Current source preparation starts at published alpha.22 commit
`f694b51c8a815ce041fe67b26c8e21627aaa5cda`. The owner reports alpha.22's successful Windows release with
538 main tests and one composite WPF test. That historical report is separate from the alpha.23 execution below.
At completion of local verification, alpha.23 had not yet been committed or published. The owner then explicitly
authorized Codex to commit/push and release through the existing Windows workflow. CI publication must be confirmed
separately. No installed-service mutation or real-profile VPN connection was performed for local testing.

## Current alpha.23 execution

Local host: Windows, repository-pinned SDK 10.0.401 and runtime 10.0.13. Tests use isolated fixtures and synthetic
profiles. Native route reads and temporary application identity fixtures are not real tunnel or packet acceptance.

- `pwsh -NoProfile -File scripts/verify.ps1`: **exit 0**, finished `2026-10-25T15:03:25Z`. Locked restore of all six
  projects succeeded. Release solution build: **0 warnings, 0 errors**. Main tests: **617 passed, 0 failed, 0 skipped**,
  reported 6 seconds. Composite WPF regression: **1 passed, 0 failed, 0 skipped**, 34 seconds. Source-size contract passed.
- The composite includes busy status polling, overlapping/late replies, known mutation followed by fresh status,
  failed read without mutation replay, explicit off superseding the read, scoped single-instance activation,
  route-failure RU/EN copy and narrow layout, plus the existing UI scenarios.
- Main fixtures cover split routes, optional route IPC serialization, ordinary-route readiness/cancellation/recovery,
  route identity/prefix validation, IPv6 source classification, comma-bearing executable paths, package refresh,
  native case normalization, equal WFP identity, reparse rejection and cancellation without partial intent adoption.
- Native casing uses an owned temporary copy of `cmd.exe`, launched hidden with AutoRun disabled and closed afterward.
  Its uppercase launch spelling produced `QueryFullProcessImageNameW` equal to the normalized path prepared for Xray.
  This is an application-path contract fixture; it neither replaces Xray nor measures VPN traffic.
- A preparatory main-test build found xUnit2010 in the new equality assertion. The assertion was corrected without
  disabling the analyzer; the next build passed. Integrated WPF preparation also corrected nullable postcondition
  and xUnit2031 diagnostics. The final full verify above ran after those corrections.
- `node --test --test-isolation=none scripts/tests/release-auth.test.mjs scripts/tests/release-base.test.mjs`:
  **exit 0, 24 passed**, 0 failed/skipped. `pwsh -NoProfile -File scripts/tests/runtime-inputs.test.ps1`:
  **exit 0, 18 checks passed**. Release policy, vendor trust, workflow and source-cleanliness guards remain enabled.
- `git diff --check`: exit 0. Four lock files differ only in **eight internal project version ranges** from .22 to .23;
  the remaining JSON entries, external dependency versions and hashes are identical.
- RU/EN resource keys are unique and identical: main **207**, library **10**, routing **4**, updates **16**.
  Four PowerShell blocks in `UPDATE_ALPHA23_RU.md` parse successfully. Release notes equal their versioned copy.
  Guide commands were syntax-checked only at that point; actual subsequent release work is recorded separately.
- Fresh WPF captures `slx-data-route-failed-ru.png` and `slx-data-route-failed-en.png`, written at `15:03:19Z`, were
  inspected at 940 × 660: route error wraps, action controls remain visible, and the page has no horizontal overflow.

Evidence in the local working tree: `artifacts/handoff/verify-alpha23.txt`, `runtime-inputs-alpha23.txt`,
`restore-alpha23.txt`, `build-main-alpha23.txt`, `test-main-alpha23.txt`; main TRX under
`artifacts/test-results/alpha23/` and full-run TRX under each test project's ignored `TestResults/`.
Preparatory route and WPF reports are under `artifacts/test-results/alpha23-routes/` and `alpha23-status/`.
These local reports and screenshots are excluded from source ZIPs.

## Alpha.23 boundaries and outstanding acceptance

The friend's alpha.22 report shows Disconnected and an earlier successful DNS route. Busy polling could expose an
old snapshot as current; this confirmed UI defect does not establish why the friend's application traffic failed.
No manual disconnect, Wi-Fi culprit, provider restriction or particular DNS problem is inferred from that report.

Split `/1` routes take precedence over ordinary `/0` defaults; more-specific or competing routes can still exist.
The new bounded checks inspect representative IPv4 halves and conditional IPv6 routing, not packets or every destination.
Existing authenticated proxy/DNS checks and native route observations together still do not prove every application's
process match, TCP/UDP delivery, public egress, or behavior under all operators, DNS configurations and network software.

Before considering the release accepted, run CI on the new clean commit, build/install the matching UI/service pair,
and repeat both modes on the owner's and friends' PCs. Cover Hysteria2/VLESS, real IPv4/IPv6 and shared DNS, Steam/CS2
outside the selection, newly opened versus existing app sessions, updated package/manual apps, startup/reboot/sleep,
network changes, owned-route cleanup, recovery and explicit off. Include Windows 10, installer upgrade/rollback and
clean-machine checks. GitHub Actions remains the publication path and builds source ZIPs from the clean commit.
The website and its separate cache fix were not changed or deployed in this iteration.

## Historical alpha.22 source handoff (not the current result)

STATUS: PARTIAL. The source handoff starts at published alpha.21 commit
`0b2979d5358b5467516dd18738379f3707d744ee`, exact tag `v0.1.0-alpha.21`.
Alpha.21's Windows workflow [40158154244](https://github.com/Sooliks/slx-windows/actions/runs/40158154244)
passed 521 main tests and one composite WPF test, built a clean installer, and published its six release assets.
Those results establish the base release, not acceptance of this alpha.22 patch.

## Observed alpha.21 evidence

The owner supplied safe diagnostics captured on 25 October 2026 at 04:09:13 UTC (07:09 Moscow):
Connected, current observation, Protection=true, ProbePassed=true, no current code; Health incidents=10,
retained=10, last incident 03:51:25 UTC. No Recovery observation is present. The ten recorded initial probe
failures all retained their existing tunnel after confirmation; no automatic recovery was recorded in this service
lifetime. This does not establish the cause of those first failures, rule out manual reconnects, or attribute them
to Steam. Counters are cumulative; the last recorded incident precedes capture by about eighteen minutes.

The DNS observation is older startup evidence, at 03:42:49 UTC. Its first Gstatic query timed out in 3006 ms;
the second Slx query succeeded in 62 ms with RCODE 0. Both selected OwnedTunnel with prefix length 32 and route
metric 0. Thus this startup completed protected DNS readiness through the expected route. It does not prove the
friend's adapter problem is solved, nor identify why the first query timed out. DNS query targets are not resolver
provider identities, and this startup observation is not a live trace of the later HTTPS health incidents.

## Alpha.22 correction and boundaries

Add a distinct Checking phase after the existing enum values. A failed first periodic HTTPS check now publishes
Checking with ProbePassed=false while retaining the running core, TUN and protection. A successful confirmation
with a still-ready core/TUN restores Connected without resetting ConnectedAt. Actual recovery remains Reconnecting.
Disconnect, controller ownership, active-update installation guards and unconfirmed-state handling stay conservative.
The UI/service must be installed together. IPC actions/version and stored-data shapes are unchanged.

The UI gives Checking its own RU/EN title and protected, unconfirmed-health detail. It keeps applicable connection
facts and explicit off available without presenting a healthy-success state. Edits during Checking wait for an
eligible service observation. Help now includes the existing primary/repeat/alternate health outcomes and bounded
timing/HTTP status metadata, allowing the next report to distinguish timeout, TLS, HTTP and other failures.
No raw exception, address, URL, profile, key, application inventory or native trace is added.

Probe targets, intervals, deadlines, recovery policy, DNS routes/resolvers, WFP rules and selected-application routing
are unchanged. In particular, toggling Telegram still affects new connections, without forcibly terminating its
existing sessions. No claim of a Steam-specific repair or measured Telegram traffic path is made.

## Alpha.22 execution evidence

Local host: Linux, SDK 10.0.401, pinned packages in `/tmp/slx-nuget`. No native Windows network mutation or real
profile was used. WPF assemblies and tests cross-compile here, but their Windows runtime scenario cannot run here.

- `dotnet restore Slx.Windows.slnx --locked-mode --packages /tmp/slx-nuget -m:1 -nr:false`: the first restricted
  invocation exited 1 with NU1900 because the sandbox could not reach NuGet's vulnerability-data endpoint. The same
  command with permitted network access exited 0 for all six projects. Vulnerability checks and locked mode remained
  enabled; no warning suppression or dependency changes were used to obtain a passing restore.
- `dotnet build Slx.Windows.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`: exit 0,
  zero warnings/errors, 28.00 seconds. The final build after improving the compact WPF text measurement assertion
  also exited 0 with zero warnings/errors, 4.42 seconds.
- `dotnet test tests/Slx.Client.Tests/Slx.Client.Tests.csproj -c Release --no-build --no-restore -m:1 -nr:false
  --filter <classes below> --logger 'trx;LogFileName=alpha22-focused.trx'`: exit 0, **111 passed, 0 failed,
  0 skipped**, reported duration 2 seconds. The runner used permitted localhost communication. Actual native
  Windows routes, WFP and application traffic were not exercised by these controlled tests.
- Classes: ConnectionHealthCheckTests, TunnelPhaseContractTests, HealthConfirmationTests, ProbeRequestTests,
  PipeFramingTests, RecoveryObservationTests, ServiceControlTests, ServiceAutoConnectTests, ServiceResponsivenessTests,
  InitialConnectionAttemptTests, TunnelRecoveryPolicyTests, StartupReadinessTests, ApplicationUpdateTransactionTests,
  ApplicationPlanChangeTests.
- New main-test cases: 9 connection-health cases, 2 enum/strict-IPC cases, 6 admission/ownership/rollback theory cases.
  They are included in the 111, not additional to that total. A controlled cancellation inside the resource-readiness
  lookup confirms that a late successful lookup cannot publish Connected after explicit off requests cancellation.
  Actual core exit during Checking retains the recovery counter path, with the existing child identity/lifecycle guard.
- `node --test --test-isolation=none scripts/tests/release-auth.test.mjs scripts/tests/release-base.test.mjs`:
  exit 0, **24 passed**, 0 failed/skipped.
- `git diff --check`: exit 0. All source C#/XAML files remain within 200 nonblank lines; RU/EN resources contain
  204 identical unique keys. Release notes match their versioned copy; relative changed-document links resolve.
  The four lock-file changes are exactly eight internal project version ranges; external dependency objects/hashes
  are unchanged. The four PowerShell blocks in UPDATE_ALPHA22_RU.md parse successfully with PowerShell 7.6.8;
  release commands from that guide were not executed.
- The Linux WPF cross-build left one untracked generated `*_wpftmp.csproj`. Its generated intermediate-project
  identity was checked and only that temporary file was removed after the build. It is not source and is excluded
  from the handoff. The Windows workflow's source-cleanliness gate is unchanged; this is not Windows clean-build proof.
- HEAD, `v0.1.0-alpha.21` and the fetched `origin/main` all identify the published base commit above. This is an
  uncommitted source handoff; the owner must create a new descendant commit before pushing. The release policy and
  workflow were not weakened, tags/assets were not changed, and no alpha.22 installer/publication is claimed.

Evidence logs: `/tmp/slx-alpha22-restore.log`, `/tmp/slx-alpha22-restore-permitted.log`,
`/tmp/slx-alpha22-build.log`, `/tmp/slx-alpha22-build-final.log`, `/tmp/slx-alpha22-tests.log`,
`/tmp/slx-alpha22-results/alpha22-focused.trx`, `/tmp/slx-alpha22-release-tests.log`.

## Alpha.22 outstanding installed acceptance

- Run the existing Windows release workflow on the newly committed source: all tests, WPF scenarios, installer and
  clean-input/source packaging gates must pass before publication can be considered successful.
- With a real installed matching UI/service pair, observe a transient failed first HTTPS check and a retained tunnel:
  Checking must not claim a successful probe, the connection start time must persist, and explicit off must work.
- Verify an actual core exit, adapter loss and fully failed confirmation still enter Reconnecting with protection.
- Verify application edits queued during Checking are applied once after recovery of a confirmed service state,
  and that explicit off does not trigger a later automatic reconnect or install while protected.
- Confirm compact RU/EN Olive/Terminal layouts at Windows runtime. Cross-compilation is not rendered WPF acceptance.
- Repeat owner/friend Wi-Fi, Hysteria2/VLESS, excluded Steam/CS2 and normal disconnect checks as appropriate.
  Clean-machine, Windows 10, driver/WFP/TUN and packet-level isolation acceptance remain separate open items.

## Historical alpha.21 source-handoff evidence (before its later successful publication)

STATUS: PARTIAL. Source patch based on published alpha.20, commit
`9cd73831292c8e8ba34327ead0039969c70cea7d`, exact tag `v0.1.0-alpha.20`.
The alpha.21 installer, Windows workflow and installed friend-machine result are not claimed by this handoff.

## Observed problem

The owner supplied an alpha.20 screenshot and safe diagnostics captured on 25 October 2026 at 04:55 Moscow
(01:55 UTC). The selected path is Hysteria2, Applications mode, Discord enabled; the reported Windows kernel version
is 10.0.26300.0. All three attempts are RouteMismatch, 0 ms, route=OtherInterface. The service reports current
Disconnected / Protection=false. This establishes a pre-send route rejection and reported completed rollback;
it does not establish which competing interface/prefix was chosen or prove a Wi-Fi, ExitLag or DoH-server fault.

## Confirmed weaknesses and correction

- Alpha.20 relied on the two default routes, with no host routes for its advertised TUN DNS addresses. Pinned Xray
  26.9.9 installs exactly the configured prefixes and uses zero route/interface metrics. A more-specific ordinary
  route can still beat a default route. Alpha.21 adds only 1.1.1.1/32 and 2606:4700:4700::1111/128 on the owned TUN,
  alongside the unchanged defaults. Equal-prefix foreign routes can still conflict and must not be silently accepted.
- Alpha.20 sampled three failed routes immediately without awaiting any work, exhausting its attempt count before
  the route had time to settle. Each attempt now allows up to six ordinary route observations with 500 ms pauses,
  sharing one three-second route-plus-exchange deadline and the original nine-second total budget. Caller cancellation
  propagates. Awaited pause/exchange work is bounded even when a test adapter returns late. A late native observation
  cannot overwrite the last timely route evidence or confirm readiness.
- The UDP socket and native route lookup remain unbound/unrestricted. No packet is sent until the owned TUN wins;
  the complete DNS answer and the ordinary route are checked before success. A final route failure now returns
  VPN_DNS_ROUTE_FAILED; normal DNS transport/response failures retain VPN_DNS_FAILED. Initial-failure rollback,
  established-session protection and explicit off are unchanged.
- Optional PrefixLength and RouteMetric add only numeric evidence. Successful IPv4 prefixes above 32, lookup errors
  and missing identities cannot pass. The route metric is an offset, not the combined interface-plus-route cost.
  Safe copy uses prefix_length/route_metric; no address, adapter name/GUID, profile or raw exception is added.
  UI/service are upgraded together; new readers accept old observations lacking these optional fields.

## Commands and actual results

Local host: Linux. SDK 10.0.401 and cached pinned NuGet packages; cross-compilation targets net10.0-windows.
Windows native networking and WPF execution are not available on this host.

- `dotnet restore Slx.Windows.slnx --locked-mode --packages /tmp/slx-nuget -m:1 -nr:false`: exit 0, all six projects.
- `dotnet build Slx.Windows.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`: exit 0,
  zero warnings/errors. Initial integrated build 26.14 seconds; final build after shortening RU/EN copy 5.05 seconds.
  Actual Desktop/WPF, Service, Windows/Core and both test assemblies compile.
- `dotnet test tests/Slx.Client.Tests/Slx.Client.Tests.csproj -c Release --no-build --no-restore -m:1 -nr:false
  --filter <focused classes below> --logger 'trx;LogFileName=alpha21-focused-permitted.trx'`: exit 0,
  **129 passed, 0 failed, 0 skipped**, reported duration 3 seconds.
- Focused classes: DnsRouteReadinessTests, TunnelDnsRouteLayoutTests, TunnelDnsRouteTests, TunnelDnsProbeTests,
  DnsProbePacketTests, CoreConfigTests, InitialConnectionAttemptTests, TunnelRecoveryPolicyTests, StartupReadinessTests,
  OutboundInterfaceTests, PipeFramingTests, RecoveryObservationTests, and
  RecoveryAndDiscoveryTests.PublicDnsRacesOnlyConfiguredHttpsResolversThroughVpn.
- The first test-runner launch exited 1 before executing tests because the sandbox prohibited its local socket.
  Re-running with permitted local runner communication passed the same selected tests. No VPN, TUN, WFP policy,
  physical route/DNS setting or live production profile was created by these tests.
- `node --test --test-isolation=none scripts/tests/release-auth.test.mjs scripts/tests/release-base.test.mjs`:
  exit 0, **24 passed**, 0 failed/skipped. Existing workflow and publication guards are unchanged.
- `node scripts/release-base.mjs` on this uncommitted handoff: exit 1, RELEASE_BASE_MISMATCH, as required because
  HEAD is still the alpha.20 base itself. The guide creates a new alpha.21 commit before the workflow runs this gate;
  the base tag and its exact commit are present. The publication guard was not bypassed or changed.
- Source-size check: all .cs/.xaml files at most 200 nonblank lines; largest 176. New route-readiness tests 165 lines
  were independently reviewed. RU/EN resources: 201 matching unique keys. `git diff --check`: exit 0.
- Four parsed lockfiles differ only in eight internal alpha.20-to-alpha.21 project ranges; external versions and
  content hashes are unchanged. Current and versioned release notes match. Seven guide PowerShell blocks parse
  successfully under PowerShell 7.6.8; rollout commands were not executed. No tracked source deletions.

The 129 tests include controlled route convergence, permanent foreign/unavailable routes with zero sends, cancellation
in cooperative/late-returning pauses, the real per-attempt deadline, late response rejection, post-response route loss,
final-failure classification, metadata privacy/backward reads, exact native-layout bytes, both routing modes and
rollback waiting before returning the new fault. The ten new settling cases are included, not additional to 129.
WPF fixtures were extended for the distinct RU/EN error, retained off-state/action, and copied route numbers, but only
compiled locally. Screenshots produced by those tests must be reviewed after the real Windows workflow.

Run evidence is outside the source archives: /tmp/slx-alpha21-restore.log, /tmp/slx-alpha21-build-final.log,
/tmp/slx-alpha21-tests-permitted.log, /tmp/slx-alpha21-results/alpha21-focused-permitted.trx,
/tmp/slx-alpha21-release-tests.log. Test sources and the rollout guide make the checks reproducible.

## Native acceptance still required

The existing Windows workflow must run all main/WPF tests, build the clean installer, and package matching source,
provenance and checksums before publication. These source ZIPs are review/apply handoffs, not clean-build publication
artifacts. The normal main version bump still triggers that workflow; no guard or test was disabled.

On the owner and friend's PCs: verify both modes and live protocols, ordinary DNS and selected apps, unselected CS2,
the two owned DNS host routes while connected, successful Disconnect, and preservation of pre-existing foreign routes
and physical DNS. Include a persistent equal-prefix conflict and explicit cancellation; false Connected is forbidden.
Also verify owned-route removal after core termination. The service kills/waits for its core and waits for the owned
adapter to disappear; the Go Close method is not assumed to execute on every termination. Unit/layout tests do not
establish native Windows route creation, removal, packet isolation, Wi-Fi compatibility or clean-machine acceptance.

The exact earlier competing route is not recoverable from the supplied bounded alpha.20 diagnostic. Its historical
cause remains unproven; the new numeric fields support a more precise report if the failure persists. No server,
subscription, account/HWID, dependency, driver, physical-DNS or website migration is introduced.

## Primary implementation evidence

- [Pinned Xray Windows TUN](https://github.com/XTLS/Xray-core/blob/v26.9.9/proxy/tun/tun_windows.go): exact prefixes,
  owned LUID, zero metrics and normal close path.
- [Pinned TUN handler](https://github.com/XTLS/Xray-core/blob/v26.9.9/proxy/tun/handler.go): external interface controller
  is selected/registered before TUN route setup.
- [Pinned WireGuard native-layout checks](https://github.com/WireGuard/wireguard-windows/blob/v1.0.1/tunnel/winipcfg/types_test.go):
  row size 104, destination prefix offset 12 plus nested prefix-length offset 28, metric offset 84.
- [Microsoft route preference](https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/unattend/microsoft-windows-tcpip-interfaces-interface-routes-route-metric),
  [GetBestRoute2](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/nf-netioapi-getbestroute2), and
  [MIB_IPFORWARD_ROW2](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/ns-netioapi-mib_ipforward_row2).

## Alpha.20 publication evidence added after its source handoff

Alpha.20 subsequently passed Windows workflow run
[40136133276](https://github.com/Sooliks/slx-windows/actions/runs/40136133276): 499 main tests and the WPF scenario,
24 release-policy tests and 18 runtime-input checks; the public release is
[v0.1.0-alpha.20](https://github.com/Sooliks/slx-windows/releases/tag/v0.1.0-alpha.20).
Publication was 25 October 2026 at 00:56 Moscow (24 October 21:56 UTC). Source assets and installer were checked
against their published hashes. That CI evidence did not establish the friend's installed routing behavior.
The older alpha.20 source report below is preserved as historical evidence, not the later publication status.

## History — 0.1.0-alpha.20 source handoff (2026-10-24)

STATUS: PARTIAL. Source patch based on published alpha.19, commit `522cb9ec9c4f13ef2cd71d20c4918c76fbdfc37c`.
No alpha.20 installer or successful alpha.20 native Windows workflow is claimed by this source handoff.

## Observed problem and scope

The friend's screenshot shows alpha.19, selected-app Hysteria2, Discord enabled, VPN off and VPN_DNS_FAILED.
That code is emitted after the authenticated HTTPS readiness and adapter checks, during DNS readiness. It does not
identify a resolver outage, a socket refusal, a foreign route, Wi-Fi, ICS or any particular security product.
The owner explicitly confirmed alpha.19 works on his own PC. The reproduced defect reduces resilience when the first
resolver fails; it does not imply that every alpha.19 connection fails. No production profile or server was accessed.

## Confirmed defect and correction

Exact Xray 26.9.9 `app/dns/dns.go` excludes fallback candidates when disableFallback=true; with the two current bare
server entries and no matching domain rules, the empty selection uses clients[0] only. Enable fallback among the same
two HTTPS-only peers and enable parallel queries. The normal four-second first-resolver wait otherwise exceeds a
three-second individual DNS readiness attempt. No localhost/system/plaintext/+local resolver is added. Both peers
retain the resolver tag routed to VPN before self bypass; production WFP and the pinned core/driver are unchanged.

The unbound DNS socket keeps ordinary Windows routing. Read-only route checks before sending and before accepting a
positive answer require the owned TUN identity; missing/foreign routes cannot send or pass. This prevents whole-PC
service exemptions from turning a physical DNS response into proof of TUN DNS. Route observations are snapshots,
not a packet-capture guarantee. The existing three attempts, three-second/overall nine-second deadlines and caller
cancellation remain. The complete bounded response must contain the correct question and a real question-owner IN/A
answer, as emitted by the pinned core. Header-only replies and late responses cannot falsely confirm readiness.

Optional bounded DnsCheck status retains three typed attempts at most, survives rollback, and resets at the next core
launch. Strict IPC framing, old statuses without DnsCheck and safe diagnostics are covered. UI/service must still be
updated together: strict old clients are not promised to accept new status fields. RU/EN copy identifies DNS failure.
No raw packets, exception text, addresses, adapter identities, profile names, app paths or credentials enter diagnostics.

## Commands and actual results

All local .NET commands used SDK 10.0.401, single-node MSBuild, the existing package cache and the unchanged locked
external dependency versions/hashes. The host is Linux; Windows-targeted compilation is not native Windows execution.

- `dotnet restore Slx.Windows.slnx --locked-mode --packages /tmp/slx-nuget -m:1 -nr:false`: exit 0, all six projects.
- `dotnet build Slx.Windows.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`:
  exit 0, zero warnings, zero errors, 25.88 seconds. WPF and its expanded regression scenario compiled.
- `dotnet test tests/Slx.Client.Tests/Slx.Client.Tests.csproj -c Release --no-restore -m:1 -nr:false
  -p:UseSharedCompilation=false --filter <focused classes below> --logger 'console;verbosity=minimal'`:
  final exit 0, 107 passed, 0 failed, 0 skipped, 3 seconds. This final run includes strict IPC round trips.
- Focused classes: DnsProbePacketTests, TunnelDnsProbeTests, TunnelDnsRouteTests, CoreConfigTests,
  RecoveryAndDiscoveryTests.PublicDnsRacesOnlyConfiguredHttpsResolversThroughVpn, RecoveryObservationTests,
  InitialConnectionAttemptTests, TunnelRecoveryPolicyTests, StartupReadinessTests, OutboundInterfaceTests,
  PipeFramingTests. The preceding integrated run without the framing class passed 101 tests.
- Separate linked-source checks passed 46 probe/packet cases and 17 route-policy cases. These overlap the 107 above and
  must not be added to that count. They inject native route reads rather than performing Windows route calls.
- `node --test --test-isolation=none scripts/tests/release-auth.test.mjs scripts/tests/release-base.test.mjs`:
  exit 0, 24 passed, 0 failed/skipped. Existing release and clean-source guards remain unchanged.
- Source size limit, RU/EN key parity/duplicates, exact release-note match, no external package/hash changes,
  unchanged WFP/release scripts and exact alpha.19 base checks passed. No tracked source deletions.

The first source-archive byte check compared `.ps1` files to raw LF Git blobs and exited1 on the first PowerShell file.
The archive correctly uses the repository's explicit `*.ps1 text eol=crlf` attribute. The corrected audit applies only
the declared per-file Git EOL conversion before comparing complete bytes; arbitrary whitespace is not normalized.
No PowerShell source was changed to accommodate this archive check.

The configuration agent's earlier broader Linux filter ran 20 cases successfully and failed 3 existing
PackageCacheIsNotAnApplicationLibrary cases because those assume Windows CommonApplicationData/C:\ProgramData.
Those unrelated tests were left intact. The narrowed configuration filter passed 8 cases. An initial isolated-route
harness command used unsupported `dotnet test --packages` and exited 1 before compilation; the corrected
`-p:RestorePackagesPath=/tmp/slx-nuget` run passed 17 cases. Neither failed command is represented as successful.

## Actual pinned-core reproduction and working-path controls

Official Linux Xray 26.9.9, build 52a412d, was downloaded and checked before execution. Release archive SHA256:
`1eb9175d0f0a8f8149c9230a7fc5ae66ce332ed20a53155ce61fe62e3f58b7df`.
The isolated harness uses the real resolver-selection code with loopback UDP resolver stand-ins and a loopback inbound.
It uses no TUN, WFP, physical DNS changes, remote VPN credentials or live production DoH server. It proves core policy,
not installed transport availability. `python check.py` and `python control_cases.py` each exited0.

| Scenario | First queries | Second queries | Observed result |
| --- | ---: | ---: | --- |
| Old configuration, first unavailable | 1 | 0 | No answer within 6 seconds |
| Fallback enabled but serial, first unavailable | 1 | 1 | Valid answer after 4005 ms, beyond one 3-second probe |
| New parallel configuration, first unavailable | 1 | 1 | Valid answer in 3 ms |
| New parallel configuration, second unavailable | 1 | 1 | Valid answer in 2 ms |
| New configuration, both unavailable | 1 | 1 | No successful answer |
| Old configuration, both healthy | 1 | 0 | Valid answer in 2 ms |
| New configuration, both healthy | 1 | 1 | Valid answer in 2 ms |
| First immediately SERVFAIL, second positive 200 ms later | 1 | 1 | Positive answer in 202 ms |
| First immediately NXDOMAIN, second positive 200 ms later | 1 | 1 | Positive answer in 202 ms |

Local timing is a controlled observation, not a latency promise on the owner's/friend's network. Both negative-first
cases verified that the negative reply was sent before the delayed positive reply; racing did not accept early failure.

An independent parser harness consumed a response emitted by the actual pinned core: valid compressed/flattened A
accepted; every proper truncation rejected; unrelated-owner A rejected; 20,000 seeded malformed/mutated packets produced
no exception. `dotnet run --project Review.csproj -p:UseSharedCompilation=false -m:1 -nr:false` exited0.
Independent review also caught and corrected answer-owner and expired-own-deadline acceptance before the final 107-test run.

## Native checks still required

Full `scripts/verify.ps1` on Windows, expanded WPF DNS-copy/diagnostics/screenshots, real GetBestRoute2/LUID/GUID,
installed service/TUN/WFP and real DoH through HY2/VLESS remain pending for alpha.20. Native assertions and screenshots
are committed for the existing release workflow; they are not silently skipped or treated as passed locally.
The friend's exact cause, success after installation, owner's working PC, Wi-Fi/ExitLag/ICS coexistence, CS2 traffic,
Windows 10, clean-machine/upgrade/rollback and system/network transitions need installed acceptance.
Old successful alpha.19 Windows checks are history, not alpha.20 acceptance. No core upgrade or physical-network tweak
was made for the adjacent upstream ICS report because the screenshot does not establish that cause.

## Primary implementation references

- https://github.com/XTLS/Xray-core/blob/v26.9.9/app/dns/dns.go
- https://github.com/XTLS/Xray-core/blob/v26.9.9/proxy/dns/dns.go
- https://github.com/XTLS/Xray-core/blob/v26.9.9/proxy/tun/tun_windows.go
- https://xtls.github.io/en/config/dns.html
- https://learn.microsoft.com/en-us/windows/win32/api/netioapi/nf-netioapi-getbestroute2
- https://learn.microsoft.com/en-us/windows/win32/api/netioapi/nf-netioapi-convertinterfaceluidtoguid
- https://learn.microsoft.com/en-us/windows/win32/fwp/filter-arbitration
- https://learn.microsoft.com/en-us/windows/win32/winsock/windows-sockets-error-codes-2

---

# Verification — 0.1.0-alpha.19 (2026-10-24)

## Published alpha.19 verification

[Run 40107793607](https://github.com/Sooliks/slx-windows/actions/runs/40107793607) completed successfully for
`522cb9ec9c4f13ef2cd71d20c4918c76fbdfc37c`: locked restore, full build with 0 warnings/errors, 434 main Windows tests and one
expanded WPF scenario, 24 release-policy and 18 runtime-input checks, installer, clean-source packaging and publication.
[Alpha.19](https://github.com/Sooliks/slx-windows/releases/tag/v0.1.0-alpha.19) was published 24 October 2026 at 15:40:43 UTC.
All six assets were independently downloaded: hashes matched SHA256SUMS and GitHub digests; full 361 / changed 25 source
inventories and every file matched the reviewed commit and previously delivered source. Provenance had sourceClean=true,
candidate=false, publicationAllowed=true and the correct alpha.18 base. Installer was not executed in this environment.
Installer SHA256: `5219ae35917e249e17a5ae5700f72ad2f0321b42a3da78e4ed371926ae0d5326`.
A final code-linked catalog check returned Available/alpha.19 from current alpha.17 and Current from current alpha.19,
with the container-required proxy enabled only in that harness. On 24 October at 15:46:20 UTC, fresh site page/download
requests still served alpha.18; immediate site propagation was not confirmed. The owner subsequently confirmed his own
alpha.19 works, while the friend's screenshot exposed the DNS stage discussed in alpha.20 above.

The following alpha.19 sections retain the earlier preparation evidence and its then-pending checks.

STATUS: PARTIAL. This patch starts from published alpha.18, commit `ccb1b71fb0a5fd6842741298933003380db408c2`.
The owner supplied direct Windows curl results using the updater's public URLs and headers. The release feed returned
HTTP 200, quota remaining 56/60, then timed out after 15004 ms with 126432 of 149771 bytes read. The checksum asset
returned 302 then 200. This establishes a slow/stalled catalog-body failure at the old 15-second deadline; the screenshot
alone did not identify its cause. No conclusion about the ISP or all connectivity failures is drawn.

Before changing code, exact alpha.17 Core sources accepted the actual published alpha.18 feed and checksum. A linked
production updater harness also returned Available/alpha.18 through the execution environment's required proxy.
Default direct transport could not resolve DNS in this container; that result concerns the container, not the owner's PC.
The update protocol/version/asset contract was not broken by publishing alpha.18.

## Change and measured transfer reduction

Only the fixed catalog request advertises gzip. Both transferred and decoded catalog bytes remain bounded to 2 MiB;
ordinary identity responses remain supported. Installer/checksum requests keep their existing exact byte/length/hash
rules and allowed HTTPS destinations. The single check budget is now 30 seconds, including catalog/checksum bodies and
redirects; cancellation from the caller remains cancellation. Expiry of the check's own budget returns UPDATE_TIMEOUT.
UI feedback uses fixed RU/EN mappings for timeout and invalid data; unknown fault codes remain generic. No retry loop,
proxy change, server mutation, service/IPC/routing change or external dependency upgrade is included.

The real unauthenticated GitHub feed was fetched in identity and gzip forms with the same updater headers:
149771 bytes versus 23874 transferred bytes, with identical 149771-byte decoded content and SHA-256. This is 84.06%
less transfer (6.27 times smaller). Measured latency in this environment is not a prediction for the owner's connection.

## Commands and results for alpha.19

- `dotnet restore Slx.Windows.slnx --locked-mode --packages /tmp/slx-nuget -m:1 -nr:false`: exit 0 with NuGet audit
  available. The four existing lock files change only internal project-version ranges from alpha.18 to alpha.19;
  every external package version/content hash is unchanged.
- `dotnet build Slx.Windows.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`: exit 0,
  zero warnings/errors. All six projects, including WPF/Desktop and UI tests, cross-compiled. A generated local
  `*_wpftmp.csproj` was moved outside the source tree; it is not a reviewed source file or part of either archive.
- Focused tests in the actual compiled main test assembly: 61 passed, zero failures/skips, exit 0. Filter classes:
  UpdateCheckTests, UpdateCompressionTests, UpdateCatalogTests and ReleaseVersionTests. Tests retain identity fallback,
  source/asset/redirect guards and caller cancellation, cover the observed 126432-byte transfer ceiling, wire/decoded
  oversize rejection, bad/unsupported/stacked encodings, gzip header/CRC/size corruption and truncated endings.
- A linked final-source live HTTP check returned Available/alpha.18 through the container's required proxy: catalog
  HTTP 200/gzip, checksum 302 then 200 with exactly 487 bytes. Asset requests do not advertise compression. This does
  not establish timing or direct DNS behavior on the owner's PC; production `UseProxy=false` remains unchanged.
- Independent actual-response decoding accepted 23874 gzip bytes as the exact 149771-byte identity JSON and rejected
  endings truncated by 1/8/9 bytes. Standard GZipStream alone tolerated those endings, so the helper additionally
  validates final CRC32/ISIZE against the full bounded decoded body. No new codec package or custom DEFLATE is used.
- A portable UI behavior proof using the real view model/resources passed 21 assertions. Only WPF timer/markup
  boundaries were stubbed. New native WPF checks cover RU/EN text, whitelisted errors, unknown-code privacy, retained
  offer/prepared installer, no automatic download/launch and recovery to Current. Their rendering still requires Windows.
- `node --test --test-isolation=none scripts/tests/release-auth.test.mjs scripts/tests/release-base.test.mjs`:
  24 passed, zero failures/skips, exit 0. Existing release guards and workflow are unchanged.
- Source-size <=200 nonblank lines, localization-key parity/duplicates, release-note consistency and diff whitespace
  checks passed. Archives are built from the reviewed Git inventory and checked for CRC and exact patch/full contents.

## Acceptance boundaries

Alpha.19 source changes must pass a new native Windows workflow after the owner pushes them. This workspace cannot
run the installed Windows application, WPF renderer or interactive upgrade. The owner's slow connection, the new
timeout message on that PC, actual installer handoff and the website download-cache update remain unverified.
Alpha.17/18 binaries retain their old updater; the owner may need one manual alpha.19 installer update.

---

# Verification — 0.1.0-alpha.18 (2026-10-24)

## Published release verification

[Run 40101079488](https://github.com/Sooliks/slx-windows/actions/runs/40101079488) completed successfully for
`ccb1b71fb0a5fd6842741298933003380db408c2`. It passed 24 release-policy and 18 runtime-input checks, locked restore,
the full solution build with zero warnings/errors, 416 main Windows tests and one expanded WPF scenario with zero
failures/skips. Runtime provenance/signature checks, the native installer build, clean-source guard (`sourceClean=true`),
packaging and publication all passed. Alpha.18 was published as a prerelease at 2026-10-24 14:11:24 UTC.

All six public release assets were downloaded and hashed against both SHA256SUMS and GitHub asset digests. Full source
contained 356 files and changed source 74 files from alpha.17; both inventories, ZIP CRCs and every source byte matched
the reviewed Git commit. No source deletions. Provenance confirmed the exact commit/base, installer hash, candidate=false
and publicationAllowed=true. Installer SHA-256: `90d4c874228999b232fd83d61d6008032031b7702315c2fbfca41090457915ac`.
The downloaded installer was not executed here. The last successful site-download check at 14:13 UTC still redirected
to alpha.17; subsequent site requests timed out. Website cache refresh and installed VPN/game acceptance were not proven.

The following sections retain the preceding alpha.18 preparation and failed-run history.

## Windows tests passed; immutable build-input correction

The owner pushed `dc83ebcd546c5badd27ed752d590ad48b50c49e9`.
[Run 40099322143](https://github.com/Sooliks/slx-windows/actions/runs/40099322143) passed all 24 release-policy tests,
the Inno Setup 6 preflight, locked restore and the full solution build with zero warnings/errors. The native Windows
test assembly passed 416/416 and the expanded WPF scenario passed 1/1, with zero failures/skips. The source-size gate
passed. TRX reports and synthetic UI screenshots were saved in `windows-verification-40099322143-1`.
Both self-contained application publishes and Inno compilation completed. The installer existed, but build-info
reported `sourceClean=false`; the workflow correctly refused packaging/publication. No alpha.18 release was created.

The failing step did not print its dirty-file list, so the exact changed file on that hosted machine is not established.
Inspection found release build calls that overwrite reviewed source: the icon, runtime manifest and upstream licenses.
TRX, UI images and normal build outputs are ignored and untracked. The pinned Xray archive and both extracted binaries
match the reviewed SHA-256 values. LICENSE is byte-identical; LICENSE-Wintun differs only by CRLF, which normalizes
cleanly under Git attributes. Reserializing the manifest with PowerShell 7.6.8 also produced no semantic/text difference
locally. These checks do not prove an icon or signer-display difference occurred on the hosted machine.

Release builds now consume the committed icon directly; `create-icon.ps1` remains an explicit source-authoring tool.
Runtime preparation validates the reviewed manifest and license content against the pinned upstream runtime and writes
only to `.runtime`. The pinned archive digest, actual Xray version and valid Wintun vendor-signature checks remain.
Installer build evidence now records exact worktree paths, a binary-capable source diff, build-info and the bundled
runtime manifest in a separate seven-day Actions artifact. Diagnostic steps cannot override a failed build or release
gate. No source reset, source ignore rule, version change or runtime/application test removal was introduced.

Correction verification completed locally:

- `pwsh -NoLogo -NoProfile -File scripts/tests/runtime-inputs.test.ps1`: exit 0, 18 checks passed, including rejected
  mismatched hashes, inventory/version/source/archive/signature metadata and missing/changed licenses.
- Actual official pinned ZIP: archive and extracted binary hashes match; the validator accepts both license files and
  preserves reviewed input bytes. A three-case fetch integration fixture passes valid input and refuses a changed
  reviewed hash or license before staging the manifest. Only Windows executable-version and Authenticode calls were
  replaced by explicit test doubles; archive extraction, digest/hash checks, validation and copying ran normally.
- The exact workflow evidence step ran in an isolated Git fixture: tracked and untracked paths, text/binary changes,
  build-info and bundled runtime manifest were recorded without cleaning the modified source.
- An isolated clean Git packaging fixture produced the exact six expected assets with matching ZIP inventories,
  CRCs, checksums and commit/base provenance. Tracked changes, untracked changes and `sourceClean=false` were rejected.
  Its installer was an explicitly marked non-executable fixture; this is packaging validation, not a native build.
- PowerShell AST/workflow YAML parsing, blocking-step/token-scope review, source-size and diff whitespace checks passed.

The updated build scripts still require a new native Windows run. Real connections on the friends' PCs, CS2/Steam,
Wi-Fi/Ethernet route behavior, installed upgrade/recovery and final GitHub/site distribution remain unverified.

## Windows verification follow-up after the compiler fix

The owner pushed `0903212692104fc2cbe67b580c60a712de0cf751`.
[Run 40097872743](https://github.com/Sooliks/slx-windows/actions/runs/40097872743) passed the corrected Inno check,
locked restore and the full solution build with zero warnings/errors. The main Windows test assembly ran 416 tests:
415 passed, one failed before reaching its dispatcher assertion at `entered.Wait(2s)`. The separate expanded WPF
scenario failed at `AdaptiveNavigationChecks`: the expected wide sidebar was 204, but the observed compact sidebar
was 76. Installer build, packaging and publication did not run.

The two synchronous-validation fixtures now use a dedicated thread and an asynchronously awaited entry signal,
with release in finally. Their original 2/5-second limits and replay/cancellation assertions are preserved. An isolated
process with one ThreadPool worker reproduced the original failure at line 97; both corrected scenarios pass under
that same constraint. Focused portable `ServiceResponsivenessTests` passed 5/5, with no skips or failures.

The WPF fixture now explicitly sets its requested test viewport through `Sta.Resize` and checks ActualWidth/Height
before applying the original layout assertions. This addresses WPF's desktop-dependent native track constraints;
setting Width alone did not establish the required actual viewport. All 26 shared-window resize transitions use the
helper. Production minimum 940x660 is asserted before test sizing; production UI, breakpoint, window constraints and
the sidebar 204/76 assertions are unchanged. Exact runner desktop dimensions were not present in the failed log.
At that handoff the correction still required a new native Windows run. Run 40099322143 above subsequently passed it;
the local cross-compilation result alone was not WPF execution.

Final correction validation: locked restore with the existing package locks passed, and
`dotnet build Slx.Windows.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` passed with
zero warnings/errors, including all updated WPF fixtures. A first restricted restore could not reach NuGet audit
data (NU1900); the unchanged locked command succeeded with network access. Audit/lock checks were not disabled.
PowerShell AST parsing, workflow YAML checks, the <=200-nonblank-line source gate and diff whitespace checks passed.

`verify.ps1` additionally records TRX test results. Actions saves those and synthetic UI screenshots as a seven-day
artifact even on verification failure. Only this diagnostic upload is nonblocking; failed tests still block installer
build and publication. The workflow modification starts a new run on push without changing alpha.18.

## GitHub run and compiler preflight correction

The owner successfully pushed `58e3c92541641c453199ba6237b98f59f20f58d3`; its tree matches the prepared source tree.
[Run 40097168143](https://github.com/Sooliks/slx-windows/actions/runs/40097168143) used Windows image
`windows-2025-vs2026` version `20261021.278.1`. Checkout, .NET 10.0.401, Node 24.10.0, all 24 release-policy tests and
the exact alpha.17 predecessor check passed. The next step failed with `Expected Inno Setup major version 6`.
The executable existed, but its PE FileMajorPart was not 6. Windows application tests, installer build, packaging and
publication were skipped. The hosted image's software inventory lists Inno Setup 6.7.1.

The corrected preflight uses the same absolute compiler path, requires the exact Inno Setup 6 help banner and the
expected help exit code 1, then clears only that handled native exit code for the Actions PowerShell wrapper.
Official `jrsoftware/issrc` tag `is-6_7_1`, `Projects/ISCC.dpr`, confirms ShowBanner and the `/?` → Halt(1) path.
No native application test, build, provenance or release check was removed. A workflow-only push now also triggers a
new run; re-running the old failed SHA would still use the old broken preflight. Version remains alpha.18.

Local correction checks: workflow YAML parsing and `git diff --check` passed. The exact PowerShell step body was
executed with five controlled native-command fixtures: Inno 6 help/exit 1 succeeds with final wrapper exit 0;
Inno 7, an unexpected exit 2, a prefixed fake banner and an absent banner are rejected. All five expectations passed.
Only the Windows compiler path was replaced by each fixture path. This checks exit handling and recognition;
it does not count as running the real Windows compiler or passing the next hosted Windows build.

The source-preparation evidence below predates the owner's push; it is retained as a record of that stage.

STATUS: PARTIAL. This work ran in a Linux workspace, not on the owner's or friends' Windows machines.
The baseline is the supplied alpha.17 source archive. Screenshots show SERVICE_UNAVAILABLE and protected
selected-app operation. The owner also reports that the affected PC has a Wi-Fi adapter; active Wi-Fi/default-route
selection on that PC has not been measured. No production service, site, repository branch or release was mutated.

The owner subsequently explicitly requested GitHub publication. The uploaded source baseline was compared with
remote main `491d9255d1a0cf32fd9eb679cb8887ca8df91d2f`: all shared source contents matched after line-ending
normalization. The remote-only `.github/workflows/verify.yml` was preserved; an archive-only generated TRX was excluded.
The connected repository reports owner push permission, but GitHub rejected the actual create-tree mutation with
HTTP 403 `Resource not accessible by integration`. No Git object, branch, tag or release was successfully written.
Local GitHub CLI/credential authentication is unavailable. Prepared release automation must be pushed using the
owner's Git access; its native Windows job and automatic publication have not run in this session.

## Commands and results

- SDK: official .NET 10.0.401, installed in a temporary directory for verification.
- Initial `dotnet restore Slx.Windows.slnx --locked-mode`: exit 1, NU1004 in Desktop and UiTests. Their uploaded lock
  files lacked the existing Windows project's ProtectedData dependency. Regenerated the locks against existing pinned
  references, confirmed every existing external package version/content hash stayed unchanged, then repeated strict
  restore. Only the existing ProtectedData 10.0.13 transitive entry and internal project versions were synchronized.
- Final `dotnet restore Slx.Windows.slnx --locked-mode --packages /tmp/slx-nuget -m:1 -nr:false`: exit 0.
- `dotnet build Slx.Windows.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`: exit 0,
  zero warnings/errors. This cross-compiles Core, Windows, Desktop/WPF, Service and both test projects.
- Focused managed regression run: 203 executed, 203 passed, zero failures/skips, exit 0. Filtered to network,
  configuration, pipe framing, dispatcher/replay/ownership, startup/health, rollback/cleanup, route updates and outbound
  interface policy. Native Windows tests were deliberately not selected, not silently counted as passed.
- The test runner requires a localhost socket; the first restricted invocation could not create it. The same tests
  completed with reviewed local-runner access. No VPN/WFP/TUN or production endpoint was invoked by this test selection.
- PowerShell 7.6.8 parser: all 12 source `.ps1` scripts parse, including emergency recovery. The administrative recovery
  commands were not executed. The downloaded official PowerShell archive matched its GitHub release SHA-256.
- Source size <=200 nonblank lines for authored `.cs`/`.xaml`, XML/resource-key consistency, and archive contents/hash
  comparison are checked during handoff. Source archives omit generated binaries/caches and have no source deletions.
- Release automation: `node --test --test-isolation=none scripts/tests/release-auth.test.mjs scripts/tests/release-base.test.mjs`
  executed 24 checks, all passed, zero failures/skips, exit 0. Wrong repository/ref/event/workflow/commit, missing job
  token, moved main, a missing/unrelated/mislabeled previous tag and local-token owner bypass are rejected.
  `node --check` succeeds for the changed release script and both new modules. Workflow YAML parses; action references
  are pinned to primary-source commit SHAs, and the release token is passed only to the final publication step.
  These checks do not run GitHub Actions or create an installer. CI retains all original build/provenance/hash guards.

The 203-test filter is reproducible on a built tree:

```powershell
$classes = @('Core','OutboundInterfaceTests','Service','TunnelRecoveryPolicyTests','InitialConnectionAttemptTests',
 'StartupReadinessTests','StartupOffCleanupTests','HealthConfirmationTests','ProbeRequestTests','OwnedAdapterCleanupTests',
 'BackgroundCleanupTests','RecoveryObservationTests','TrafficApplicationPolicyTests','ApplicationPlanChangeTests',
 'ApplicationUpdateTransactionTests','XrayRoute','PipeFramingTests')
$filter = ($classes | ForEach-Object { 'FullyQualifiedName~Slx.Client.Tests.' + $_ }) -join '|'
dotnet test tests/Slx.Client.Tests/Slx.Client.Tests.csproj -c Release --no-build --no-restore --filter $filter
```

## New scenarios covered

Managed tests reproduce status and explicit-off responsiveness while connect waits; reject unauthorized/malformed
off; preserve old-off replay without cancelling a new connection; admit fresh off during validation; reject changed
same-ID off during that validation window; interrupt readiness without concurrent lifecycle mutation; retain and pace
incomplete owned-off cleanup; allow first-connect rollback only from confirmed off; preserve protection on established
failure/cancellation; surface failed cleanup; accept either strict startup HTTPS observation; select the Windows route
identity regardless of Wi-Fi enumeration order; reject down/missing/own-TUN interfaces, including a renamed own GUID.

Added Windows-only fixtures cover overlapping native pipe clients and first-instance ownership. Added WPF scenarios
cover unknown-first-connect Disconnect, missing-subscription recovery, cancellation of busy connect, Stopping action,
and retained original/early failure details. These fixtures compile but were not executed in this Linux environment.

## Not verified

Full Windows `scripts/verify.ps1`; native ACL/pipes, GetBestRoute2, WFP, TUN/PnP cleanup, DNS flush, emergency recovery,
WPF runtime/layout, Windows 10, clean VM, real VPN protocols and CS2/Steam traffic, ExitLag coexistence, dual-stack
uplinks, reboot/sleep/network changes, Inno installer, UAC/upgrade/uninstall/rollback and update publication.
The explicit interface still uses the pinned core's single-interface model. Shared public Windows DNS stays protected
and can affect unselected apps. Source fixes and managed tests do not prove the friends' individual root causes or
complete game/UDP isolation. No production binaries or prior release assets were overwritten.

Build/install/release instructions: [UPDATE_ALPHA18_RU.md](UPDATE_ALPHA18_RU.md).

---

# Verification — 0.1.0-alpha.17 (2026-10-23)

STATUS: PARTIAL. Codex ran scripts/verify.ps1 on the Windows host: exit 0, completed 14:50:42 UTC. Locked restore and Release solution build passed with zero warnings/errors; 382 unit/Windows checks and one expanded WPF scenario passed, zero failed/skipped. Source size gate passed. Log: artifacts/handoff/verify-run5.txt. LayoutStabilityChecks covers stable connection/subscription positions, floating notice dismissal and suppression of short activity flashes. Current slx-floating-notice-ru-narrow.png was viewed. Synthetic WPF evidence does not establish installed product, physical DPI, real network or upgrade/rollback acceptance. No service/core/IPC/data/dependency change. Update download progress still appears when a transfer starts.

# Verification — 0.1.0-alpha.16 (2026-10-23)

STATUS: PARTIAL. Owner explicitly requested build/publication without a full verification run. No verify.ps1, unit or WPF tests executed for this release; newly added tests are unexecuted. Release build and packaging are performed by the standard scripts; exact identity is recorded in provenance. Installed upgrade, receipt expiry behavior, staged-installer reuse and the reported long-uptime failure remain unverified. Historical alpha.15 results below do not validate alpha.16.

# Verification — 0.1.0-alpha.15 (2026-10-23)

STATUS: PARTIAL. Codex ran `pwsh -NoProfile -File scripts/verify.ps1` on the owner's Windows host via an evidence
wrapper: final run 03:05:15–03:05:46 UTC, exit 0. Locked restore, Release solution build with zero warnings/errors,
379 unit/Windows checks and one expanded WPF scenario passed, zero failed/skipped, source <=200 nonblank lines.
SHA-256 inventories of all non-ignored source files before/after the gate match. Local evidence:
artifacts/handoff/verify-alpha15.txt and source-alpha15-before/after.json. Only release documentation was finalized
after this run; runtime/test files were unchanged. Earlier integration runs also passed. Claude authored the initial
UI pass; Codex ran the host commands and performed release review. The original runs 1–2 caught a new scroll-memory
defect, corrected before the successful run 3 and this final gate; no gate was weakened.

Expanded WPF evidence covers page scroll memory, service-confirmed status tone, blocked subscription removal,
non-persistent removal, and deferred app edits submitted once after a fresh observation without replaying unknown
mutations. Saved-choice labels remain explicit at no observation/off/unconfirmed/unknown states in both languages;
13px labels fit the narrow/wide layouts. High Contrast fixtures switch one owned resource layer on/off, preserve
foreign resources and restore the selected theme without accumulating layers. These fixtures inject preferences;
they never change the owner's actual Windows accessibility settings.

Separate unsuppressed native-window fixtures start real WPF animation clocks. Mid-flight hide, minimize, unload,
policy-off and render suppression apply final values and remove clocks/subscriptions; finite fade/route completion
does the same. Route base Progress stays 1 and losing confirmation cancels the ring. This is native lifecycle
evidence, not a PresentMon/ETW performance study or a physical-monitor motion review.

Caption checks cover equal 46x42 hit targets, 12x12 glyph areas and numerical centering, keyboard-focus/chrome flags,
UIA maximize/restore invocation and RU/EN names/tooltips. Maximize happens only while the isolated fixture is hidden.
Caption normal/restored and hover/press PNGs were viewed; hover/press previews force final template layers and are
not evidence of real mouse input. Subtree capture now normalizes its origin, avoiding empty offset screenshots.
Current populated/update PNGs were viewed at RU 940x660 and EN 1200x820, Olive/Terminal. The synthetic updater offer
is alpha.16 for installed alpha.15; it is not a public alpha.16 release. 224 matching RU/EN keys across four owners,
no duplicates. git diff --check passed. Olive uses native Segoe UI if site fonts are absent; no font is installed.

The isolated `dotnet run --project src/Slx.Client.Desktop/Slx.Client.Desktop.csproj -c Release --no-build --no-restore
-- --render-check` refreshes empty/import/help/settings PNGs without reading stored user data, contacting the service
or starting update checks. Final packaging identity belongs to clean-build provenance and SHA256SUMS.txt.

No installed UI/service, saved subscription/preferences, startup registration, WFP, adapter, account, website,
server or DB was changed for acceptance. No network/Core/Windows/Service, IPC, dependency or stored-data format change.
Real Windows animation/High Contrast toggles, physical DPI 125/150/200, Narrator/keyboard in the installed product,
live performance, Windows 10/clean VM, multi-user, reboot/BFE/ExitLag, sleep/network transitions and installed
upgrade/UAC/cancel/rollback remain open. The friend's reboot failure is not declared reproduced or fully fixed.
The real-core API contract from alpha.14 remains historical evidence; this UI-only release does not rerun or extend
packet/TUN/DNS isolation or connection-continuity acceptance.

# Verification — 0.1.0-alpha.14 (2026-10-21)

STATUS: PARTIAL. `pwsh -NoProfile -File scripts/verify.ps1` on the Windows host exited 0: locked restore,
Release solution build with zero warnings/errors, 379 unit/Windows checks and one expanded WPF scenario, zero
failed/skipped, <=200 nonblank source gate. The first build found CS8631 in three new nullable JSON assertions;
explicit non-null string assertions corrected them without weakening tests or production guards. After adding the
final native UI evidence scenario, targeted `dotnet test tests/Slx.Client.UiTests/Slx.Client.UiTests.csproj -c Release
--no-restore` also exited 0, one expanded scenario and no failures/skips/warnings.

New isolated evidence covers effective app-set equality, strict profile/mode comparison, separate dispatch path,
SID/session/receipt-capacity guards, idle update rejection without ownership capture, strict replay after stop and
app updates never invoking Start/Stop. WFP planner checks preserve existing filter IDs, no-op zero transactions,
full preflight, union/rollback snapshots and baseline invalidation after native failure. They never call real WFP.
Coordinator checks cover union → durable desired intent → one core replacement → final policy, initial/final storage
failure, cancelled caller, lost acknowledgement reporting old/new/unrelated revisions and read-only reconciliation
without replay. A failed final native transaction retains protection and unconfirmed state, not a false success.

WPF checks cover coalescing, disabled removal/toggle-back using app-only IPC, final app off, separate mode/profile
reconnect including changed same-name profile, Whole-PC library changes, explicit reconnect, unknown outcome without
automatic replay, stale response/revision versus newer edit and live protected failure feedback. Current native RU
940x660 and EN 1200x820 live/unconfirmed PNGs were inspected: hints and persistent scoped feedback wrap; connection
action stays available, no horizontal overflow. These are synthetic profiles and controlled status, not real VPN proof.
218 paired RU/EN keys across four owners, no duplicates; source size and git diff --check pass.

Separate real-core command: `dotnet run --project tests/Slx.XrayRouteContract/Slx.XrayRouteContract.csproj -c Release
--no-build -- C:/Users/xls/Desktop/slx-windows/.runtime/xray.exe` exited 0 after a zero-warning targeted build. The
fixture verifies the pinned executable SHA, starts its own Xray with config in stdin, authenticated loopback HTTP and
local echo only. Two actual full routing replacements and ListRule revision readbacks passed; the same existing TCP
stream continued. Unauthenticated CONNECT returned 407 and wrong-password AddRule was rejected. A first fixture launch
rejected a documentation IP as INVALID_PROFILE; its synthetic hostname was corrected, without changing runtime policy.
This is an actual pinned API contract, not TUN, installed service, WFP or Internet traffic acceptance.

No installed application/service, user preferences/subscription, firewall, adapter, server, account or DB was mutated.
Existing sessions in the changed app can retain their old route until that app reconnects. Windows ALE can reauthorize
flows after policy changes, so retained core/TUN/filter identities do not establish every flow's packet continuity.
Installed app-rule changes and packet isolation/continuity, unusual Shell providers, Windows 10, multi-user loopback
port races, boot/sleep/network changes, upgrade/UAC/rollback, DPI/High Contrast/live accessibility remain open.
Final build/installer identity belongs to release provenance; publication is not installed-system acceptance.

# Verification — 0.1.0-alpha.13 (2026-10-20)

STATUS: PARTIAL. `pwsh -NoProfile -File scripts/verify.ps1` on the Windows host exited 0:
locked restore, Release solution build with zero warnings/errors, 330 unit/Windows checks and one expanded
WPF scenario; zero failed/skipped, <=200 nonblank authored source gate. An initial build found a nullable
test assertion (CS8602); it was corrected without weakening gates. Final targeted
`dotnet test tests/Slx.Client.UiTests/Slx.Client.UiTests.csproj -c Release --no-restore` exited 0 after
the Settings spacing and update copy changes: one expanded WPF scenario, zero failed/skipped or build warnings.

Icon fixtures cover monotonic negative-cache expiry, one bounded cancellable retry, weak-target lifetime,
group-member fallback without changing routing rules, explicit same-path refresh and frozen native images.
An isolated Unicode temporary path starts missing, receives a copy of the built apphost and recovers its actual
resource icon on retry. The apphost is never executed. Real Yandex and TeamSpeak executables on this PC also
returned native images through the actual alpha.13 loader in a read-only probe; alpha.12 decoded them in the
same observation. The user's original transient failure is not reproduced. Permanent negative caching and
first-member-only image lookup are confirmed code defects; missing explicit Shell COM setup is an API-contract
hardening change, not proof of the original cause. No preferences, subscription data or service state were changed.

The controlled WPF scenario checks the floating card at RU 940x660 and EN 1200x820. Connection and subscription
rectangles remain identical before/after the offer and dismissal; the card does not intersect Connect or steal
focus, and modal flows suppress it. Dismissal during download keeps the transfer running and progress visible
in Settings. Tests cover fresh service-state installation guards, mutation-lease races, UAC cancellation,
checksum failure, prepared-file retention and rejection of late progress from an older download. Fixtures never
launch an installer or UAC. Current native PNGs for offers, download, deferred Settings and failure/wait states
were visually inspected without clipping or horizontal overflow. The synthetic offer is alpha.14 for installed
alpha.13; it is not a published-release or installed-upgrade claim. 215 matching RU/EN resource keys across
the main, library and updates owners; no duplicate keys. `git diff --check` passed.

No installed client, startup registration, VPN service, firewall, adapter, account, server or DB changes were made
for acceptance. Installed upgrade/cancel/rollback, clean Windows 10/11, unusual Shell providers, physical-monitor
rendering at 125/150/200% DPI, High Contrast and live accessibility remain open. Historical reboot/network acceptance
gaps remain open. Final clean source/installer identity is recorded in release provenance; publication does not
establish installed-system acceptance.

# Verification — 0.1.0-alpha.12 (2026-10-20)

STATUS: PARTIAL. Final `pwsh -NoProfile -File scripts/verify.ps1` on the Windows host exited 0:
locked restore, Release solution build with zero warnings/errors, 330 unit/Windows checks and one expanded
WPF scenario; zero failed/skipped, <=200 nonblank authored source gate. An earlier targeted cache/scanner
run hit sandbox DPAPI restrictions (6 failures of 42); the same targeted host run passed all 42. No gate was weakened.

New unit evidence covers schema/count/size/Unicode/path/UTC cache validation, CurrentUser DPAPI round trip,
corrupt/foreign-purpose/missing cache, atomic replacement and independent preferences/vault behavior in isolated
fixture directories. Discovery checks cover fast-source publication before a blocked source, concurrency three,
source failure isolation, cancellation quiescence/no late publication and total shortcut-directory queue bounds.

The controlled WPF scenario covers saved rows before a blocked scan, fast batches, single refresh, search and
explicit addition during refresh, 300 previous plus 300 new transient rows retained on partial failure, unchanged
observation time and no cache write after incomplete scan. Complete scans prune old rows and save once; queued
progress cannot restore an old row while the final cache save is blocked. A recent automatic revisit reuses the
complete result; manual refresh bypasses that interval. Missing cached files cannot become selected routes.
Discovery does not mutate selected rules or call the service. Pagination and search across the available list pass.

Async icon fixtures check two workers, coalescing, cache access during blocked extraction, LRU eviction, frozen
fallback, caller cancellation, origin Dispatcher, weak target lifetime and disposal. UIA/search fixtures preserve
query/caret/logical focus and the same card when a later source adds a second executable path. RU 940x660 and
EN 1200x820 WPF PNGs were inspected for cached refresh/search/partial failure, without clipping or horizontal
overflow. These are synthetic fixtures with fallback icons, not actual third-party icons or a physical-monitor study.
201 matching RU/EN resource keys across the main and library owners; no duplicate keys. git diff --check passed.

A temporary read-only console called the actual DiscoveryScanner on this Windows PC, without writing a cache,
executing a shortcut, printing app names/paths or changing any network/service state. Exit 0: first nonempty batch
100ms, all six sources succeeded, total 1866ms. This is one observation with this PC's OS/file caches, not a benchmark
across users or a measurement of the complete WPF frame time. Optional persisted-cache behavior is fixture tested.

No installed client, startup registration, VPN service, firewall, adapter, server, account or DB changes were made
for acceptance. Required open evidence includes installed upgrade/cancel/rollback and first/returning startup latency
on clean Windows 10/11, unusual shell providers, DPI/High Contrast and live screen-reader behavior. Historical network
acceptance gaps remain open; the service and network policy are unchanged in alpha.12. Final clean build/installer
identity is recorded in release provenance; publication does not establish installed-system acceptance.

# Verification — 0.1.0-alpha.11 (2026-10-20)

STATUS: PARTIAL. Final `pwsh -NoProfile -File scripts/verify.ps1` outside the restricted sandbox exited 0:
locked restore, Release build with 0 warnings/errors, 285 unit/Windows checks and one expanded WPF scenario,
0 failures/skips, <=200 nonblank source gate. The initial sandbox restore could not access user NuGet.Config;
the host rerun used the same gate without weakening it. An earlier updater success fixture exceeded its artificial
2s disk-I/O budget; ordinary fixtures now use 15s, with dedicated 100ms timeout tests and production limits unchanged.

New isolated evidence covers complete native application-ID preflight, missing EXE/directory results 2/3,
safe stage-code preservation, scoped owned-filter cleanup, explicit-off receipt capacity and ownership release,
Discord/Roblox exact-path repair before connect and encrypted service resume, and atomic connect-if-idle no-op/replay.
Startup checks cover normal/manual launch guards, delayed network/service/profile readiness, cached refresh failure,
unknown acknowledgement without replay, manual/setup/update cancellation and existing protected intent.
Known-off startup cleanup pacing/cancellation is checked without WFP mutation; unreadable intent never authorizes cleanup.
The UI waits only for the distinct unprotected startup-cleanup code and cannot connect until confirmed ready.

Updater checks cover SemVer, complete same-repository release assets, bounded HTTP and redirects, staging ACL/MOTW,
size/SHA-256 verification, launch locking, cancellation and download/install state handling. Launcher fixtures never run
an installer or UAC. A temporary isolated console called the real public ReleaseUpdateClient for installed alpha.9:
exit 0, Available / 0.1.0-alpha.10 / checksum validated. It read metadata/checksums only.

Current real WPF RenderTargetBitmap snapshots were inspected at 1200x820 and 940x660: wide/compact navigation,
connection/fault/update states, import, long app names, library/loading, Terminal Help and RU/EN settings.
The disconnected/unknown/recovery/first-use facts block is absent; confirmed synthetic connection shows actual fixture
values and unknown protocol is omitted. Routing labels are «Приложения» / «Весь ПК» at 14px with Ideal/Grayscale,
13px secondary text and stronger native weights. Settings UIA toggles the new auto-connect preference; disabled startup
preserves its saved choice. These fixtures do not read real subscriptions or change Windows startup registration.
The hidden `--render-check` process exited 0. artifacts/ui contains generated images; 96-DPI evidence is not a physical
27-inch monitor, live screen-reader or 125/150/200% DPI acceptance.

An earlier unpublished `scripts/build.ps1 -Candidate` exited 0 and checked official Xray 26.9.9 SHA-256 and Valid vendor
Wintun signature. Intermediate typography binaries are preserved outside the final release directory; they are not
final installed-binary evidence. Final clean source/installer identity is recorded separately in artifact provenance.

The friend's screenshot/report establish alpha.10 TRAFFIC_PROTECTION_FAILED after reboot/Connect. Removed version
paths reproduce one mechanism; the exact friend's native error and ExitLag conflict remain unknown.
No installed service, live VPN, BFE/firewall/adapter, production account, server or DB mutation was performed.
Open acceptance: installed service/UI, real TUN/egress/isolation, actual reboot/ExitLag coexistence, installer
upgrade/UAC/cancel/rollback, clean VM, Windows 10, sleep/network changes, full IPv6, multi-user packet isolation,
physical-monitor rendering, system High Contrast and live accessibility/input. GitHub publication is not this acceptance.

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
