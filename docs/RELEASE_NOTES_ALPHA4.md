# Alpha.4: catalog and browser source-address policy

STATUS: PARTIAL. The installed candidate was accepted by the owner for ChatGPT and Claude.
A temporary guarded test changed only SkipAsSource on the owned fd73:6c78::1/126 address, then restored it.
The installed service now applies this policy through IP Helper after verifying its owned adapter GUID and address.
A failed native update fails connection setup; it is not reported as Connected. No physical NIC or global IPv6 setting is changed.

The application catalog is public static data, schemaVersion 1, served at https://slxvpn.com/api/desktop/catalog.
Only bounded display names, category IDs and executable basenames are accepted. No executable downloads, commands or paths from the server.
Offline fallback is embedded. Running programs, registry App Paths, uninstall DisplayIcon and known local locations are discovered locally.
Missing programs retain a manual executable picker. Store applications and unusual installation locations may need manual selection.
No app inventory or popularity telemetry is sent to SLX.

The dashboard retains disabled application switches. Counts describe selected rules, not traffic or proof of per-process isolation.
Duration is shown only for an authoritative Connected state and protocol comes from the active service profile.
Unknown/stale status shows no connected duration or protocol. The last service observation time is distinct from tunnel readiness.
The library has no nested scrolling container around its item grid.

No server configuration, subscription binding or database migration is required for the client upgrade.
The catalog endpoint may be deployed later; fallback works before deployment.
Roll back with an older installer; preserve the installation ID and user data. Alpha.3 lacks the source-address mitigation.
