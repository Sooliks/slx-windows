# SLX Windows — development contract
Read PROJECT_SPEC.md and docs/ARCHITECTURE.md before changes. This is a separate desktop repository, not the web/server repository.
C#/.NET 10, WPF/MVVM, Xray, Windows Service and Inno Setup. Preserve SLX brand.
No production server/env/DB changes; no Happ edits; no server credentials in source.
UI is unprivileged; service validates every IPC message and caller SID. No arbitrary command/config execution.
Subscription import is explicit and sends this installation's own X-Hwid. Never spoof Happ or bypass server binding/slots.
No fake connected state, metrics or unsupported toggles. Source files <=200 nonblank lines; split by owner.
Pin dependencies, use locked restore; never weaken guards/tests to obtain a green build.
Record actual commands/exits. Unit/parser/build are not real TUN, clean-machine or security acceptance.
Keep requested scope and unrelated work. No mass formatting. No direct production mutations.
Unsigned client/installer are intended; vendor driver signatures must remain valid. Never disable Windows security.
Final: STATUS, Changed, Verified, Not verified, Adjacent fixes, Assumptions, Deviations.
