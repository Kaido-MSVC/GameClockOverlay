# GameClock Overlay
<!-- impeccable:product-schema 1 -->

## Platform
Windows desktop (native WPF)

## Stack
User-selected C# / WPF / .NET 11 / C# 15. SDK 11.0.100, installed Windows Desktop Runtime 11.0.0. No external NuGet dependencies, UWP, or WinUI.

## Users and purpose
Personal use and a small circle of friends. Read local time while gaming or using the desktop.

## Capabilities and constraints
User-confirmed: HH:mm, optional seconds, font size, opacity, monitor selection, full-monitor bounds, dragging and four corners, click through with recovery, tray, persistent settings, Traditional Chinese / English / Japanese with system default and manual priority, MIT. No injection, game hooks, elevation, or network. Formal v0.2.0 publication is authorized after build, regression and version consistency checks. User accepted post-fix real-device functionality; second-monitor and mixed-DPI testing is unavailable. User tested borderless fullscreen successfully; exclusive fullscreen is unsupported. Supported validation platform: Windows 11 x64.

## Product principles
Lightweight implementation, safe recovery, maintainable source, proportional verification.
