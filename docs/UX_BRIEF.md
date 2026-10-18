# Windows client UX brief

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
- Language/theme changes do not disconnect or recreate the tunnel. Autostart is
  opt-in and does not imply automatic VPN connection.
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
