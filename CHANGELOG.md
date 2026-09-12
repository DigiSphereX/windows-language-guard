# Changelog

All notable changes to LanguageGuard are documented here.

## 1.0.1 - 2026-09-12

- Fix: "Balloon tip text must have a non-empty value" crash when protection resumed at startup.
- Languages now show their real Windows names in the checklist (e.g. "Arabic (Iraq)"),
  not hex codes; logs show names too.
- Two protection levels: keep watching in the background, or fix languages once at
  sign-in and exit (start the app with `--boot`; used by the Run key in that mode).

## 1.0.0 - 2026-09-12

- First public release. Single portable EXE (no admin, no install).
- Choose allowed input languages; enforce every 2 seconds.
- Auto-removes languages Windows adds to `HKCU\Keyboard Layout\Preload`.
- Forces the active keyboard back to an allowed language if it drifts.
- Optional start-with-Windows (per-user, no admin).
- Tray icon with close-to-tray and per-user settings + activity log.
- Buildable from source using only the built-in .NET Framework compiler.