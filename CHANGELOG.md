# Changelog

All notable changes to the Windows port of DeepSeek Status. Version numbers track
the upstream macOS project (the balance feature matches upstream 1.2) plus patch
releases for port-specific fixes.

## [1.3.1] — 2026-09-13

### Fixed

- The panel no longer grows downward / slides when its height changes while open
  (preview banner, API key editor, login-item error, balance rows). It re-anchors
  on every size change, so the bottom edge stays pinned above the taskbar and the
  top edge grows upward.

## [1.3.0] — 2026-09-13

### Added

- The countdown pill next to the notification area now shows the account balance
  once loaded (`HH:MM:SS · ¥42.00`).
- The tray tooltip appends the balance, with progressive truncation (full title →
  short title → drop countdown) so the tooltip stays under the 62-character limit.
- `DEEPSEEK_STATUS_FAKE_BALANCE=42.00` development override to preview the tray
  balance without an API key or network access.

## [1.2.0] — 2026-09-13

### Added

- **Account balance (opt-in)** in the panel, backed by
  `GET https://api.deepseek.com/user/balance`. Refreshes on startup, every
  5 minutes, and when the panel opens with stale data (also after sleep resume).
- **API key editor**: enter / change / remove, masked input, prefilled when
  replacing an existing key.
- **Windows Credential Manager** storage for the key (encrypted, per-user, never
  a plain-text file). Reading handles both UTF-8 and UTF-16 credentials.
- Balance display: total per currency, granted / topped-up split, last successful
  refresh time, and a **Refresh** button.
- Failure handling: an expired key reports *"invalid or expired"* with
  **Change API Key** offered first; HTTP/network failures offer **Retry**;
  responses arriving after a key change are discarded.
- Zero network traffic until an API key is saved.
- Self-tests for balance JSON parsing, amount formatting and a real Credential
  Manager round-trip.

## [1.0.1] — 2026-09-13

### Added

- Initial Windows port of [owenzhao/DeepSeekStatus](https://github.com/owenzhao/DeepSeekStatus):
  - Tray whale: bright and tilted during peak hours, pale and asleep off-peak.
  - Panel: current period and multiplier (`×1.0` / `×0.5`), current-rate bar,
    countdown with block progress, 7×24 weekly heat map, preview picker, options.
  - Quick menu: preview peak / off-peak / live, countdown pill, project page, quit.
  - Optional draggable countdown pill next to the notification area.
  - Launch at login via the per-user `Run` registry key.
  - All displayed times converted to the machine's local time zone; the pricing
    rule itself stays Beijing time (UTC+8), matching how DeepSeek bills.
  - English and Simplified Chinese UI.
  - Same whale vector, palettes and animations as the macOS original.
  - Self-contained local publishing (`build.ps1 -Publish`, no CI required).
  - Built-in self-test (`--selftest`).

[1.3.1]: https://github.com/tespio/DeepSeekStatus/releases/tag/v1.3.1
[1.3.0]: https://github.com/tespio/DeepSeekStatus/releases/tag/v1.3.0
[1.2.0]: https://github.com/tespio/DeepSeekStatus/releases/tag/v1.2.0
[1.0.1]: https://github.com/tespio/DeepSeekStatus/releases/tag/v1.0.1
