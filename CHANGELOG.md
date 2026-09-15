# Changelog

All notable changes to LanguageGuard are documented here.

## 1.0.6 - 2026-09-15

- New **"System state (detected)"** panel on top of the window shows the real
  languages installed/selected in Windows on this PC — including the Windows
  display language (e.g. English (United States)) — and the typing languages that
  appear next to the clock.
- Any typing language that is **not** actually installed as a Windows language
  (e.g. one Windows sneaked in as "basic typing") is flagged in orange, so you can
  see exactly what the guard is cleaning up.
- Detection reads the same sources Windows Settings uses:
  `HKCU\Control Panel\International\User Profile` (`Languages` + `WindowsOverride`
  = display language) for installed languages, and `HKCU\Keyboard Layout\Preload`
  for the typing languages next to the clock.
- Cleaner interface: real Windows language names only
  (e.g. "German (Germany)", "Arabic (Iraq)") — no more hex codes in the UI.
- `--selftest` now also dumps the detected lists (WindowsInstalled / Typing /
  TypingNotInstalled) to `%TEMP%\lg_selftest.txt` for verification without a GUI.

## 1.0.5 - 2026-09-13

- About dialog now shows a short disclaimer (use at your own risk, back up settings).
- README gained a "Disclaimer / Backup advice" section.

## 1.0.4 - 2026-09-13

- New menu bar with the main options: Protection (Enable / Release),
  Options (autostart, background vs sign-in mode, tray), and Help.
- New About dialog (Help -> About) showing program name, version, description,
  author and a GitHub link.
- EXE now carries proper file version info (1.0.4.0).

## 1.0.3 - 2026-09-12

- On any computer, opening the program now detects the languages currently selected
  in Windows and shows them already checked (working alongside the saved allow-list).

## 1.0.2 - 2026-09-12

- New: the checklist now contains the **full Windows "Add a language" catalog**
  (600+ languages, e.g. Arabic (Iraq), English (United Kingdom), ...), not just the
  layouts physically installed on the machine.
- New: search box to filter the list while typing.
- Fix: checking a language, filtering it away, then enabling no longer drops it
  from the protected set.

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