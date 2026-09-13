# LanguageGuard

[![Donate](https://img.shields.io/badge/Donate-PayPal-0070BA)](https://www.paypal.com/donate/?hosted_button_id=CFANQH892RPH2)

Keep only the input languages you type in. Windows silently adds input languages
next to the clock — LanguageGuard stops it permanently.

## What it does

- The allowed language list is enforced every 2 seconds.
- Any language Windows auto-adds to `Preload` is removed within seconds.
- If the active keyboard drifts to a non-allowed language, it snaps back instantly.
- One portable EXE, no Admin rights, no third-party libraries, works offline on
  Windows 10 and 11 (32/64-bit).

## Download

Get `LanguageGuard.exe` from the
[latest release](https://github.com/DigiSphereX/windows-language-guard/releases/latest).

## Usage

1. Run `LanguageGuard.exe`. The checklist shows **the full Windows language
   catalog** (600+ languages, e.g. Arabic (Iraq), English (United Kingdom)) by real
   name — type in the search box to filter. The languages currently selected in
   Windows on this machine are already ticked.
2. Tick the languages you are allowed to type in.
3. Press **Enable & Protect**. LanguageGuard sits in the tray and enforces your
   choice every 2 seconds.

### Startup options

Open **Settings → Startup**, then choose how it behaves at logon:

| Mode | Behaviour |
|---|---|
| **Keep watching in the background** (recommended) | A tiny tray process guards forever. No noticeable resource use. |
| **Fix once at sign-in, then exit** | Runs the app with `--boot`. Reverts anything Windows auto-added
right after logon, then closes itself after ~90 seconds. No resident process.
Best-effort — stops most cases but may miss languages added mid-session. |

Settings live under `HKCU\Software\LanguageGuard` — everything is per-user,
nothing is installed system-wide.

## How it works

- Allowed list stored in `HKCU\Software\LanguageGuard\Allowed`.
- A 2-second timer compares it with `HKCU\Keyboard Layout\Preload` and rewrites
  any mismatch so Preload contains exactly the allowed languages.
- The active keyboard layout is checked with `GetKeyboardLayout` and forced back
  via `LoadKeyboardLayout` + `WM_INPUTLANGCHANGEREQUEST` if it drifted.
- Activity is logged to `HKCU\Software\LanguageGuard\Log`.

## Why does a watcher exist?

Windows offers no built-in "never auto-add a language" switch, and it does not
fire an event when an app sneaks a language into the Preload list. The only
reliable, permanent solutions are:

1. **Always-on watcher** — instant, 100 % coverage.
2. **One-shot at logon (`--boot`)** — zero footprint; near-perfect for most users.

Removing unwanted languages manually and disabling per-app input-method tracking
helps but is not bulletproof — some installers add layouts without asking.

## Build from source

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build\build.ps1
```

No toolchain to install: the script uses the .NET Framework compiler that ships
with Windows (`csc.exe`). The icon is generated at build time.

## Requirements

- Windows 10 or 11
- .NET Framework 4.x (pre-installed on every Windows 10/11 installation)

## Disclaimer / Backup advice

Use at your own risk. Even though LanguageGuard is tested, a program that runs in
the background can behave unexpectedly on a specific machine:

- Back up your input-language settings (or create a restore point) before using it.
- Read the source before running the binary if you like — everything is plain C#.
- LanguageGuard needs no admin rights; do not grant it privileges it doesn't ask for.

The author is not responsible for any unintentional damage or data loss.

## License

MIT — see [LICENSE](LICENSE).

Copyright (c) 2026 M. Basheer (DigiSphereX)

## ☕ Support this project

Free and open source (MIT). If this project saved you time or money, consider a small thank-you:

- **GitHub Sponsors** -> https://github.com/sponsors/DigiSphereX
- **PayPal** -> https://www.paypal.com/donate/?hosted_button_id=CFANQH892RPH2