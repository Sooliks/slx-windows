# Application library — alpha.12

Desktop owns library presentation, saved/read activity and explicit selection. Windows owns local discovery and the
optional cache through UserSettingsStore. No inventory is sent to the catalogue endpoint, update server or diagnostics.

## Return and refresh

Initialization warms the previous discovery snapshot off the UI thread. Applications entry shows it and starts one
background scan. A complete successful scan is reused for five minutes during this process; explicit Refresh bypasses
that interval. Search and explicit Add remain usable while discovery is active. The query and stable card instances
survive progress; adding another exact path to a known group updates that card instead of replacing it.

Six independent sources cover running windows, installed apps, local shortcuts, Steam, the known application group
and catalogue paths. Three workers publish sources as they complete; the public catalogue refresh runs independently.
Expected source failure does not cancel other sources. Existing scanner filters, exact paths, no shortcut Resolve or
shell execution, local inventory boundaries and selection semantics remain. Shortcut traversal additionally caps total
scheduled directories at 2048, depth five and 200 children per directory.

A complete successful scan replaces/prunes the local results and saves at most 300 paths with its actual UTC observation
time. During incomplete refresh, the prior list is retained alongside up to 300 new paths, with at most 600 transient
rows. Fresh metadata for an existing exact path takes precedence. Failed/incomplete refresh never advances the last
complete observation or replaces the durable snapshot. Queued progress is rejected before the complete result commits.

The managed refresh budget is 12 seconds. Cancellation is checked between native/registry/file items; already started
native calls must return before the operation is released. Late callbacks are rejected. Refresh stays single-flight,
so repeated clicks cannot stack scans behind a slow native call. The interface explains a delayed completion while
keeping available content usable; this is not a promise to forcibly abort a Windows API call at exactly 12 seconds.

## Local cache and explicit addition

The optional discovery.dat uses a schema-1 envelope and separate-purpose DPAPI CurrentUser protection. Maximum cipher
and plain sizes are 1 MiB; at most 300 unique IDs and exact local EXE paths are accepted. Canonical path, timestamp,
Unicode/name and structural checks happen without File.Exists, native discovery or icon extraction during loading.
Missing/corrupt/inaccessible cache falls back to discovery without changing preferences, subscription or installation ID.
Writes use the settings owner's atomic temporary-file replacement. Cache failure does not discard the usable list.

Saved rows are previous observations, not proof that a program is still installed. Explicit Add validates the group's
files on a worker and adds only existing exact paths. A moved/removed file offers refresh/manual selection. Manual
file selection adds the chosen executable without importing unverified cached siblings. Discovery by itself never
selects a new path, changes selected rules or reconnects VPN. Service plan validation remains authoritative.

## Presentation and icons

The first 12 matching cards are shown; Show more adds 12 and search resets the page. This limits native WPF realization
without nesting another scroll area or preventing access to the full list. Query, selection and available content remain
while checking. Status distinguishes saved, checking, delayed completion, partial failure, complete and cache-write
failure. The observation label always belongs to the last complete scan. No fabricated progress percentage is used.

Cards show a frozen fallback immediately. Shared icon work is coalesced by exact path, capped at two native extractions,
and retained in an LRU of 512 images. Extraction runs outside the UI thread and shared cache lock. Weak completion
targets and dispatcher delivery prevent abandoned cards from being retained or updated. Selected cards use the same
asynchronous mechanism. The main resource formatter also reads a feature-owned RU/EN LibraryStrings catalogue.

## Migration and acceptance

No dependency, service/IPC, network policy, server, DB or account migration. Older clients ignore the optional cache.
Unit/controlled WPF checks do not establish installed Windows 10 latency, physical-monitor rendering or every shell
provider's behavior. Current executed evidence and open acceptance are in VERIFICATION.md.
