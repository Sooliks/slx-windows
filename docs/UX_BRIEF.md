# Windows client UX brief

## Alpha.14 live application changes

Friction: every app-list mutation restarts the whole core/TUN, including removing a disabled row. Given a live selected
connection, changed apps get new rules while unchanged apps keep the core, TUN and ConnectedAt. Disabled row removal,
metadata/order edits and toggles returning to the same effective set perform no WFP/Xray mutation. Service owns truth.
Rapid edits produce one coalesced plan. Profile/mode changes keep a separate reconnect intent; different connection
credentials/endpoint with the same name still reconnect. Whole-PC app edits are local. Final app off disconnects.
Show RU/EN copy that rules apply to new sessions; the changed app's already-open sessions may need to reconnect.
A lost API response retains protection and running core, reads its revision and never replays the mutation. A known
failed update with a live protected tunnel is scoped feedback, not a false disconnect. Matching revision clears only
that pending UI intent, never newer edits. Ordinary Connected without the target revision is insufficient.
Required evidence: pure equality/transaction/replay/failure tests, WPF action-count/state/copy checks, real pinned Xray
API contract without TUN and retained local TCP stream, and current RU/EN native images. These do not establish installed
WFP/TUN packet continuity, every preexisting flow, Windows 10 or physical-monitor rendering acceptance.

## Alpha.13 icon and update acceptance contract

Audience: Windows users returning to selected apps or seeing a new release. Friction: a failed asynchronous icon can
stay a generic fallback for the session; notification mixes availability, transfer and installation prerequisites.
Installed icons recover after a transient failure without blocking the UI. If a group's first path is missing, another
known member may supply the icon only; selected paths, enabled flags and routing remain unchanged. Late icon results
cannot replace a newer candidate or resurrect a disposed card. Keep two workers and immediate frozen fallback.
Owner explicitly requested absolute-style placement: the update card floats at the lower right within the workspace,
without moving the page, primary connection action or subscription. No focus theft or full-window hit-test blocker;
import/remove modals take precedence. Dismissing during transfer keeps the download and Settings entry available.
When an update is available, show its version and next explicit action. During transfer, show actual bytes-derived
percentage; 100% transfer is not an installation result. Ready, blocked by active/unknown service, cancellation and
failure each have a clear explanation and safe recovery. Settings retains a reachable saved offer after Later.
Keyboard focus and native accessible names remain; review RU/EN at 940x660/1200x820, both themes and long strings.
Controlled failure/retry/candidate fixtures plus read-only real Yandex/TeamSpeak icon extraction are required evidence;
the screenshot alone does not prove the first native failure, and installer fixtures do not prove UAC/upgrade success.

## Alpha.12 library acceptance contract

Audience: returning Windows users opening Applications to add a program. Current friction: sequential native discovery
and eager icon extraction hide usable results until every source completes; every revisit repeats that work.
On entry, show the saved local list promptly and refresh in the background. Search/add/manual .exe/navigation remain
available. A new source batch enriches the visible list independently of slow sources/icons. Refresh failure preserves
the previous list and its observation time, with scoped recovery copy. A successful complete scan prunes removed items.
Saved data is labelled as saved; checking is an activity, not fresh evidence. Validate existence before adding a cached
program; unavailable paths offer refresh/manual selection and never enter selected routing. Scanning alone never mutates
selected rules or reconnects VPN. Repeated entry coalesces active work and reuses a recent successful scan.
Observable acceptance: cached rows visible before a blocked scanner; fast batch visible before slow batch; search/add
works during refresh; failure retains cache/time; removed path cannot be added; cancellation/disposal cannot publish;
bounded concurrency and no UI-thread shell extraction. Inspect RU/EN wide/narrow cached/loading/partial/empty states.
Privacy: encrypted local cache only, no inventory upload or diagnostic paths. Real installed-app latency and all target
Windows/DPI combinations remain separate acceptance from controlled WPF fixtures.

Executed on 2026-10-20: scripts/verify.ps1 exit 0, 330 unit/Windows checks plus expanded WPF scenario, no skips/failures
or build warnings. Current RU940/EN1200 cached-refresh/search/partial PNGs inspected; query, caret, logical focus and
stable same-group card verified. Read-only real discovery first nonempty batch 100ms/complete1866ms on this PC only;
not a universal latency or installed startup acceptance claim. See VERIFICATION.md.

## Alpha.11 acceptance contract

Owner explicitly requested startup VPN connection, superseding older startup-only statements in historical sections.
With Windows autostart enabled, the separate Connect VPN on startup preference defaults on. A --tray login waits for
network/service/saved-profile readiness and performs at most one guarded connect-if-idle. Normal manual launch does not
connect. An existing active/protected/recovering service owns its intent; manual off or setup/update action cancels the
pending startup attempt. Uncertain replies require normal status reconciliation and never create another auto mutation.
Old preferences keep AutoStart=false by default; existing AutoStart=true installations get the newly requested behavior
unless the new preference is switched off. This is a documented owner decision, not an implicit reinterpretation.
While Windows startup cleanup is pending, show preparation copy, disable Connect and retain the startup intent until
a fresh Disconnected observation. Other fault/active/protected states do not acquire this exception.

Given a removed Discord/Roblox version path, Connect refreshes verified exact sibling paths before sending the plan;
unresolved or inaccessible selected executables stop protection changes with a specific safe explanation.
Given a restore failure after restart, the service exposes status/explicit off and preserves recovery intent.
The friend's exact reboot/ExitLag failure still needs installed evidence; the screenshot is not a native error trace.

Given a new complete public release, automatic optional checking offers a nonmodal banner; Download verifies bytes,
and Install requires a fresh confirmed disconnected/unprotected status. Connected, stale and unknown observations block
install. During the explicit install handoff Connect/routing cannot race the installer. Cancellation remains retryable;
starting the installer never claims the upgrade is complete. Language changes preserve the offer/download state.

Given a 1200x820 window, navigation has icons/labels; at 940x660 the same buttons become a compact 76px sidebar.
Names, tooltips, focus and current-section UIA status remain available; resizing preserves profile/app selection.
Static vector graphics remain decorative, without timers or status meaning. High contrast maps semantic brushes to Windows.
For the owner's 27-inch Full HD screen, text uses denser native weights and readable secondary labels; the opaque window
uses WPF Ideal/Grayscale after the owner rejected the earlier Display/ClearType pixel-snapped preview. Secondary text
is at least 13px, mode labels are 14px «Приложения» / «Весь ПК» with simple RU/EN explanations and unchanged routing.
Heading/body hierarchy remains, with no redistributed fonts or decorative font swap.
The statistics block and its last-check timestamp are absent while disconnected, unknown, connecting or recovering,
and on first use. Only confirmed connection with a known ConnectedAt shows actual duration/selected-app facts;
an unresolved protocol is omitted rather than shown as a dash or invented name. Setup and the primary action remain.

Current evidence: scripts/verify.ps1 passed 285 unit/Windows tests and one expanded WPF scenario on 2026-10-20;
actual WPF RenderTargetBitmap snapshots inspected at 1200x820/940x660 in artifacts/ui. Covered connection, long names,
wide/compact menus, update offer and simulated handoff, settings EN, protected-fault copy, library/loading, import and
Terminal Help. These are synthetic isolated WPF fixtures, not installed service/TUN or full keyboard/screen-reader acceptance.
Actual UAC/upgrade, Windows 10, multi-instance update, reboot, ExitLag, DPI 125/150/200 and system High Contrast remain open.

## Audience and primary outcome
A Windows user copies their own SLX connection link, imports it explicitly,
chooses selected apps or the entire computer, and connects. They do not name a
device, enter server details, install an SDK or configure a system proxy.

## Composition and brand
Native WPF, not a web wrapper. SLX cut-X, olive/lime and Terminal themes, Segoe UI
for reading and Consolas for short labels. Compact sidebar: Connection,
Applications, Settings, Help. The account opens the existing /account website.
Open source is visible in the sidebar and Help and links to Sooliks/slx-windows.
No copied ExitLag composition, fake latency, app counters or simulated activity.

## Main scenarios
- First use: unavailable service is explained; explicit link import is primary.
- Import: no background preview GET, no clipboard reading, no Happ identity copying.
  A 403 does not guess its exact cause; guidance covers access and official transfer.
- Connection: HY2 is preferred; the visible native method selector also permits
  VLESS. Native radio controls choose selected apps or the whole computer.
- Applications: visible local list, search, running-process discovery, .exe picker,
  per-app switches and removal. Full paths are local and available in tooltips.
  A blank selected-app list cannot silently turn into whole-computer routing.
- Connected rule/profile changes need explicit reconnect. Edits are disabled during
  an outstanding mutation so the selected plan cannot drift behind an acknowledgement.
- Language/theme changes do not disconnect or recreate the tunnel. Autostart is opt-in;
  its separate VPN connection option waits for readiness and can be switched off.
- Close may use the real native tray. If tray creation fails the app stays reachable
  in the taskbar. Tray Exit requests disconnect; an unknown stop is reconciled before exit.

## Async and truthful state
One service owns tunnel state. UI never sets Connected on process start. Successful
status plus ProbePassed is required. Mutation generations reject late results;
requests are single-flight, with explicit disconnect cancellation. An unavailable,
failed or timed-out reply is unknown. New connect/reconnect is disabled until an
authoritative status reconciles it; no automatic new-ID replay is issued.
Refresh failures retain the previous snapshot and its original fetched timestamp.
Status polls are bounded and never treat synthetic rejected replies as Disconnected.
Subscription secrets remain out of status text, diagnostics and render evidence.

## Accessibility and checks
Native automation peers, labeled inputs/buttons, Tab/Enter/Escape, focus indicators,
modal background disabling and scoped tab navigation. All UI copy and file-picker
labels have matching RU/EN .resx resources. Native controls use themed dropdowns and
scrollbars. High contrast maps theme colors to system resources. No decorative
animation runs, so minimized/reduced-motion users have no background motion cost.

`--render-check` is isolated from stored settings/subscriptions, autostart, tray,
service and network requests. It creates current actual WPF RenderTargetBitmap
PNGs for empty RU olive, 940x660 applications, RU settings, EN Terminal help and
import. These visual checks prove layout only, not real per-app traffic routing.

Still required: actual imported connection UI flow, live tray/keyboard UI Automation,
DPI 100/125/150/200, multiple monitors, screen-reader verification and real network,
sleep, service-crash and clean-machine acceptance. No independently verified per-app
kill switch is claimed; Help states that limitation.

## Populated UI regression — 0.1.0 alpha follow-up
The original empty-only render missed a collapsed ComboBox failure. A separate
`Slx.Client.UiTests` STA WPF project now creates the actual MainWindow with typed,
synthetic subscription/preferences injected in memory. Fixtures live only in the
test project; no saved user data, subscription GET, service call or TUN is used.
The new test reproduced the reported redacted `VpnProfile` display before the fix.
Binding the missing ItemTemplateSelector fixed both DisplayMemberPath and explicit
ItemTemplate rendering. Core model ToString redaction remains intact.

The populated regression verifies HY2/VLESS selection, both profiles in the opened
popup, Settings language changes, actual selected/whole layouts at 1200x820,
subscription and connection action above the fold, no collapsed-section divider gap,
940x660 without horizontal overflow, app-name ellipsis, and the themed tray menu.
Current actual WPF screenshots are `artifacts/ui/slx-populated-*.png`,
`slx-tray-menu.png` and `slx-exit-unknown.png`; the data is visibly synthetic.
The regression passed after the fix. This is UI evidence, not real tunnel evidence.

An IPC failure now retains an allowlisted safe code and specific guidance; it does
not universally claim the service needs installation or duplicate a banner on the
connection page. An unknown stop no longer traps the user indefinitely: a dialog
explains the VPN may still be running and offers explicit app-only exit. Cancel is
the default and Escape action; exiting the UI does not claim the VPN was stopped.
The dialog default and explicit choice are exercised by the populated STA test.
The app version label is derived from assembly metadata instead of a hardcoded tag.

Final UI checks: Release desktop build passed without warnings/errors; populated
STA regression passed; 145 RU/EN keys match and all XAML keys resolve. Authored
Desktop/UiTests sources and catalogs remain below 200 nonblank lines per file.
Installed service/IPC, actual profile traffic and upgrade checks remain separate
acceptance owned by the root workflow and are not implied by these UI checks.

## Installed app and focus follow-up
Root's current installed-client check verified the unknown-stop dialog's explicit
app-only exit: the UI process exited while the running service remained unchanged,
as disclosed. This does not claim VPN traffic stopped or end-to-end tray acceptance.
Button focus now uses WPF keyboard-only FocusVisualStyle adorners instead of changing
the measured button border on every mouse/programmatic focus. Navigation and title-bar
buttons retain stable geometry; keyboard users keep a visible outline, including a
contrasting inner outline on the primary action. The root candidate build owns the
subsequent installed screenshot and input verification.

## Alpha.2 final follow-up
Main content has a static cut-X background; it has no timers or animation and is omitted in high contrast.
Hover/focus feedback uses opacity, color or a reserved outline. It does not change padding, margins or border geometry.
The applications page discovers programs with open windows asynchronously, with inline search and one-click Add.
The manual executable picker remains visible. Background/helper processes can be selected manually.
An existing saved subscription gets one bounded refresh at startup. Cached profiles remain available during fetch or failure.
Refresh activity is separate from tunnel mutations; explicit import cancels the background fetch and late results cannot replace it.
A running tunnel is never silently replaced. Startup auto-connect and periodic subscription retries are not introduced.
Current installed HY2/VLESS and reconnect evidence belongs to docs/VERIFICATION.md; full network isolation acceptance remains incomplete.

## Alpha.9 startup follow-up
Saved startup opt-in repairs a missing/obsolete per-user Run entry on normal launch. Off remains off; render-only scenarios skip reconciliation. SLX starts in the tray with --tray; this does not issue a VPN-connect mutation. Registration failure has RU/EN actionable copy. Windows-disabled StartupApproved entries are not forcibly enabled.
