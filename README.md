# LanguageGuard أدوات حماية / Windows إدخال لغات

<div dir="rtl" align="right">

## ماذا يفعل؟

Windows خطير يضيف لغات/لوحات مفاتيح تلقائياً بجانب الساعة (لغات تظهر فجأة وتتغير بنفسها).
**LanguageGuard** يختار أيّ لغات يجوز لك الكتابة بها، وتشغيله يمنع أي لغة أخرى من البقاء:

- أي لغة تُضاف أو تُدرج تلقائياً تُحذف خلال ثوانٍ.
- إذا تغيّرت لغة الإدخال الحالية غير المُصرَّح بها، تعود فوراً لأولى اللغات المسموحة.
- يعمل كملف واحد محمول (Portable) — لا يحتاج تثبيت ولا حقوق مدير (Admin)، لا مكتبات خارجية.

</div>

## What is it?

Windows loves to silently add input languages next to the clock. **LanguageGuard**
keeps your choice the only choice:

- Any language Windows auto-adds is removed within seconds.
- If the active keyboard is switched to a non-allowed language, it snaps back.
- One portable EXE, no Admin rights, no third-party libraries, works offline on
  Windows 10 and 11 (32/64-bit).

## Usage / الاستخدام

1. Download `LanguageGuard.exe` from the latest release.
2. Run it. Tick the languages you are allowed to type in.
3. Press **Enable & Protect**. It sits in the tray and enforces your choice every 2 seconds.
4. Optional: *Start automatically with Windows* keeps the protection across reboots.

Settings live under `HKCU\Software\LanguageGuard` — everything is per-user,
nothing is installed.

## How it works / كيف يعمل

- The allowed list is stored in `HKCU\Software\LanguageGuard\Allowed`.
- A 2-second timer compares it with `HKCU\Keyboard Layout\Preload`. Any mismatch is
  rewritten so Preload contains exactly the allowed languages, in order.
- The active keyboard layout is checked with `GetKeyboardLayout` and forced back via
  `LoadKeyboardLayout` + `WM_INPUTLANGCHANGEREQUEST` if it drifted.
- Activity is logged to `HKCU\Software\LanguageGuard\Log` and shown in the window.

## Build from source / البناء من المصدر

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build\build.ps1
# -> LanguageGuard.exe (single file)
```

No toolchain to install: the script uses the .NET Framework compiler that ships
with Windows (`csc.exe`). The icon is generated at build time.

## Requirements / المتطلبات

- Windows 10 or 11
- .NET Framework 4.x (pre-installed on every Windows 10/11)

## License

MIT — see [LICENSE](LICENSE). © 2026 M. Basheer (DigiSphereX).