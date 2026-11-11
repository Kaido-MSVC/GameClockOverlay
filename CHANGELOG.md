# Changelog

## 0.2.0 — 2026-11-11

Formal release: assembly / file version 0.2.0.0; informational version and Git tag 0.2.0 / v0.2.0.

- Preserve corner and monitor on single/double click; commit only a completed substantial drag, restore cancelled moves.
- Replace title-based second-launch recovery with a named AutoReset event plus the existing mutex; no waiting on a foreign UI thread.
- Synchronize only external position / monitor / click-through controls; reuse settings content and scroll on recovery.
- Route tray and WinForms errors to centralized diagnostics and localized messages.
- Reuse one owned tray menu; defer exit until its Click stack unwinds, then explicitly dispose it.
- Add .gitattributes, focused regression / real-process IPC checks and reproducible versioned packaging.
- The user reports normal post-fix real-device functionality, including borderless games, Alt+Tab and resolution/display switching. A second physical monitor and mixed DPI remain untested due to unavailable equipment.

- Upgrade to stable .NET 11 / C# 15, SDK 11.0.100; preserve WPF and framework-dependent x64 deployment.
- Add adjustable font size (16–96 DIP), monitor selection, and optional full-monitor corner placement.
- Coalesce placement after display/DPI changes; update at second/minute boundaries and on time/power messages.
- Preserve old settings, click-through recovery, tray lifecycle, and Traditional Chinese / English / Japanese.
- Clarify fullscreen limitations and add technical assessment / game acceptance instructions.
- Use versioned publish directories and checksum files, preserving earlier packages.
- True exclusive fullscreen is NOT implemented. Borderless visibility is not evidence of exclusive support.

## 0.1.0 — 2026-11-04

Initial shared Windows x64 WPF clock: seconds, opacity, dragging / corners, click-through recovery,
tray, persistent settings, three languages and offline license viewer.
