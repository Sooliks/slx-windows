# 0.1.0-alpha.3 candidate

STATUS: PARTIAL; installed/browser candidate acceptance passed; full system acceptance remains open.

- Public IPv6 TLS destinations can recover the requested hostname before tunneling; IPv4 and local destinations remain unchanged.
- Application library uses local executable icons, search and one-click addition; manual .exe selection remains.
- Add/remove/toggle/mode changes apply automatically after a short debounce while connected. The current engine briefly reconnects; this is not a seamless hot routing update.
- Rapid edits coalesce into one operation; explicit stop/dispose cancels pending application. Unknown replies are not retried automatically.
- Shared DNS, no kill switch, existing socket migration and full IPv6/ECH support remain limitations.

Local verify: 120 unit/Windows contracts plus one expanded WPF scenario, zero skipped/failed.
Installed candidate exit 0; restored HY2 connected; owner confirmed YouTube and Twitch work. Full system acceptance remains open.
