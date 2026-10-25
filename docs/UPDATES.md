# Application updates — alpha.25

Optional startup/six-hour checks download newer complete public SLX releases in the background. Active VPN continues
working. A failed later check does not discard a verified ready package. The existing card floats above the page
without moving content or taking focus; Settings retains the same offer and progress when Later hides it for 24h.

## Integrated installation

Automatic installation while VPN is off is a separate opt-in, disabled by default and for existing settings. A visible
15-second notice can be postponed; active, unknown and pending states cancel it. Active VPN waits by default. The
explicit Disconnect and update action asks for confirmation and requires successful owned cleanup before continuing.

The service atomically rechecks confirmed off and persists a GUID maintenance session. Connection/application changes
are excluded while status and explicit off remain available. The current UI stages a bundled unprivileged WPF helper
outside the installation, verifies a live child PID/start-time handshake, commits handoff and exits. Repeated launch
shows the updater. Its branded window shows Windows permission, installation and verification without a setup wizard
or invented installation percentages. Only the verified native Inno installer is elevated, using fixed silent/no-reboot
arguments. Windows can still request administrator permission. The staged managed helper is never elevated.

After UAC, Inno invokes only the fixed previously installed service command `--claim-update GUID` before Stop. This
revalidates off and claims the same session across replacement. Exit 0 requires matching application/service native
file versions, a fresh expected service version/off observation and successful maintenance release before Succeeded.
Exit 3010 means restart required; Windows is never rebooted automatically. Download 100% and process start are not
successful installation. Unknown/failed outcomes never replay installation or claim completion automatically.

Cancelling UAC offers explicit retry or return to SLX. The retained package is revalidated before each attempt. Inno
does not implement atomic version-directory deployment: partial replacement or power loss can require repair with the
retained installer. Failed/mixed versions cannot automatically resume VPN. A consented resume stores only a plan
fingerprint, never credentials. Returning consumes the result before asynchronous work; only verified success, or
cancelled UAC with unchanged source binaries, plus fresh same-version/off service and unchanged configuration can
request one connect-if-idle. A lost reply is never replayed. Changed settings, reboot and uncertainty leave VPN off.

A known 3010 result is retained across later launches, including after days of waiting. Only a changed local Windows
LastBootUpTime observation, all target binary versions and fresh same-session/service/off evidence can release its
maintenance after reboot; this path never resumes VPN. WMI unavailable or an unverified boot observation stays pending.
If the service cannot run after a partial installation, retry cannot establish off or bypass admission. Contact support;
the existing explicit administrator repair-network.ps1 can release only confirmed stopped SLX resources, then the
retained/current installer can repair the installation. Automatic offline acceptance or global network reset is absent.

## Trust and rollout

Existing same-repository HTTPS/catalog/SemVer/checksum/redirect limits remain unchanged. SHA-256 is integrity evidence
from that repository, not an independent publisher signature. SLX remains intentionally unsigned; Wintun retains its
vendor signature. No general elevated update daemon, arbitrary update command or execution path is exposed over IPC.

Helper jobs are bounded DPAPI CurrentUser records under LocalAppData/SLX/VPN/UpdateSessions/GUID with restricted ACLs.
The protected installed manifest supplies bounded relative file names and hashes; reparse paths are rejected. Installer
bytes and Internet-zone metadata remain locked against write/delete through UAC and observed installer exit. Receipts
cannot supply executable arguments. Only a protected Program Files installation can use the integrated mechanism.
Maintenance uses separate DPAPI LocalMachine state in the existing SYSTEM/admin-only service directory. Prepare lasts
five minutes; an elevated OS-verified claim has a fifteen-minute window and repeated claim never extends it. A claimed
session that outlives that window remains blocked until verified release/repair. Expiration cannot resume VPN. Any
present, expired or corrupt marker suppresses old desired-on restore. Storage uncertainty blocks starts while keeping
status/explicit off available. No background recursive cache deletion is introduced.

Alpha.24 and earlier require one ordinary installer upgrade to alpha.25. Installed alpha.25 uses this integrated flow
for later versions. Existing automatic-check preferences retain their meaning and do not imply installation consent.
Release assets remain immutable with clean-source provenance. Actual installed upgrade, UAC, multi-user, interruption,
reboot and Windows 10/11 acceptance are separate from code/WPF checks; see VERIFICATION.md.

## Historical alpha.11–24 behavior

The following documents the older clients' updater and the retained transport constraints. Alpha.25 replaces their
interactive installation flow as described above.

The desktop checks the public `Sooliks/slx-windows` GitHub releases at startup and every six hours while running.
Settings can disable automatic checks; Check for updates remains available. Remind me tomorrow persists a 24h pause.
No subscription, HWID, executable inventory or credentials are transmitted. GitHub sees the ordinary public HTTPS request.

An alpha installation can discover newer prereleases or stable releases; a stable installation ignores prereleases.
SemVer precedence is numeric (`alpha.12` follows `alpha.9`), independent of publication ordering.
Only complete, non-draft releases with exact versioned x64 installer names and `SHA256SUMS.txt` qualify.
The feed covers the newest 20 public releases. Missing/incomplete assets are not update candidates.

The fixed catalog request advertises gzip. Encoded and decoded catalog bodies are bounded to 2 MiB; unsupported or
invalid compressed content fails validation. Plain responses remain supported. Installer/checksum requests keep their
existing byte-exact asset transfer policy. One 30-second deadline covers both catalog and checksum, including bodies
and redirects. A deadline failure becomes UPDATE_TIMEOUT; caller cancellation is not converted to a failed check.
The UI distinguishes timeout and invalid update data through fixed RU/EN copy. Unknown/unavailable errors stay generic;
no response bodies, exception messages or signed asset URLs enter the UI. No automatic retry loop is introduced.

## User flow

1. A nonmodal card floats at the lower right inside the workspace, above page content and below modal flows. It does not
   reserve a layout row, move primary controls, steal focus or change the VPN. Width is constrained to the workspace.
2. Download update fetches the installer with bounded size, duration and HTTPS redirects. Download progress is actual bytes.
3. The file remains `.part` until its complete size and SHA-256 match the release metadata. Windows Mark-of-Web is retained.
4. Install update is a separate action. It is unavailable until the service confirms disconnected with protection released.
5. The desktop rechecks the service and holds its mutation gate during launch verification/handoff. It does not issue Disconnect.
6. Windows UAC and the existing interactive installer own installation. Cancelling UAC remains retryable. After confirmed
   installer launch, SLX closes its UI to prevent reconnecting during the installer's welcome screen. If the wizard is
   cancelled, reopen SLX normally; no completed upgrade is claimed. UI/service upgrade together.

The installer preserves saved settings, installation ID and subscription. An unknown/offline service cannot authorize install.
An interrupted or failed check does not become Current. A previously observed offer may stay visible with failed-check copy;
its installer still needs all normal download/hash checks. Failed hashes cannot launch.

The card distinguishes installed/available version, actual transfer percentage, verified readiness and why installation
is blocked (active VPN, unknown service observation or current operation). Transfer 100% is not an installation claim.
Old progress callbacks cannot update a new transfer or a completed phase. Same-offer checks preserve verified readiness.
Later dismisses the offer for 24h and remains available while downloading without cancelling transfer; Settings retains
the offer, current transfer and explicit action even while the floating card is hidden. It stays disabled during installer
handoff. No operating-system notification or reminder while the app is closed is promised.

## Ownership and trust

Core owns strict release parsing/source policy; Windows owns bounded HTTP, protected staging and UAC launch.
Desktop owns timing, consent, local preference storage and connection/install coordination. No privileged auto-update service.
The public same-repository HTTPS metadata and checksum are the trust source. SHA-256 detects mismatched bytes; it is not
an independent publisher signature and does not protect against compromise of the repository itself. The app/installer
remain unsigned by owner decision; vendor Wintun signature is unchanged. Windows security is not bypassed.

Staging uses a GUID directory beneath LocalAppData/SLX/VPN/Updates, with access restricted to the current user,
SYSTEM and administrators. Reparse ancestry is rejected. The installer is checked again while a deny-write/delete handle
is held through launch. Cancellation never auto-retries the installer, and no silent command line is used.
Incomplete files are removed; completed stages are retained for explicit use. No background recursive cache deletion.

## Rollout and open acceptance

Alpha.10 and earlier have no updater: install this version once manually from the SLX download page or its GitHub release.
An alpha.17/18 client whose 15-second catalog check fails may also need one manual alpha.19 installer update: the new
compression/deadline behavior is in the installed application, not a server-side change to the old executable.
Future releases must preserve the versioned asset/checksum contract. Never overwrite a released version.
The release tool still requires a reviewed clean build, matching provenance and checksums; candidate builds cannot publish.
To roll back, explicitly disconnect, then run the previous version's installer. Server/DB migrations are unnecessary.

Offline contracts cover SemVer, allowlists, malformed metadata, redirects, bounds, integrity, staging permissions,
launch locking and simulated UAC cancellation. WPF fixtures cover snooze, optional checks, failed checks/hash,
connected/unknown install blocking, and connection command exclusion during the simulated handoff.
Actual UAC, installer upgrade, interruption/rollback, installed multi-instance behavior, Windows 10 and clean VM remain open.

Staged installer reuse (unreleased, after alpha.15): before downloading, and right after a check finds an offer,
the client looks for this exact asset in its own GUID staging folders (at most 64). A candidate is reused only when
size and SHA-256 match the published checksum and no reparse point is involved; its Zone.Identifier is restored if
missing. The offer then shows Install directly. Damaged or foreign files are ignored, and LaunchInstallerAsync keeps
its full path, hash and MOTW checks. This is an integrity reuse, not a publisher-signature check.
