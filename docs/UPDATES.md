# Application updates — alpha.13 (transport introduced in alpha.11)

The desktop checks the public `Sooliks/slx-windows` GitHub releases at startup and every six hours while running.
Settings can disable automatic checks; Check for updates remains available. Remind me tomorrow persists a 24h pause.
No subscription, HWID, executable inventory or credentials are transmitted. GitHub sees the ordinary public HTTPS request.

An alpha installation can discover newer prereleases or stable releases; a stable installation ignores prereleases.
SemVer precedence is numeric (`alpha.12` follows `alpha.9`), independent of publication ordering.
Only complete, non-draft releases with exact versioned x64 installer names and `SHA256SUMS.txt` qualify.
The feed covers the newest 20 public releases. Missing/incomplete assets are not update candidates.

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
Future releases must preserve the versioned asset/checksum contract. Never overwrite a released version.
The release tool still requires a reviewed clean build, matching provenance and checksums; candidate builds cannot publish.
To roll back, explicitly disconnect, then run the previous version's installer. Server/DB migrations are unnecessary.

Offline contracts cover SemVer, allowlists, malformed metadata, redirects, bounds, integrity, staging permissions,
launch locking and simulated UAC cancellation. WPF fixtures cover snooze, optional checks, failed checks/hash,
connected/unknown install blocking, and connection command exclusion during the simulated handoff.
Actual UAC, installer upgrade, interruption/rollback, installed multi-instance behavior, Windows 10 and clean VM remain open.
