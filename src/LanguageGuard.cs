//
// LanguageGuard - portable Windows input-language keeper (v1.0.2)
//
// Lets you choose which input languages you type in. While it runs, Windows can
// never silently add another language/keyboard near the clock again: any
// auto-added language is removed and the active keyboard is forced back to an
// allowed one within seconds. Per-user, no admin rights needed, single EXE.
//
// MIT License  (c) 2026 M. Basheer (DigiSphereX)
//

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using WTimer = System.Windows.Forms.Timer;

namespace LanguageGuard
{
    internal static class Native
    {
        public const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
        public const uint WM_SETTINGCHANGE = 0x001A;
        public const uint HWND_BROADCAST = 0xFFFF;
        public const uint SPI_SETDEFAULTINPUTLANG = 0x005A;
        public const uint SPIF_SENDWININICHANGE = 0x0002;
        public const uint KLF_ACTIVATE = 0x00000001;
        public const uint KLF_SUBSTITUTE_OK = 0x00000002;

        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        public static string HklToLangId(IntPtr hkl)
        {
            long raw = hkl.ToInt64();
            uint langId = (uint)(raw & 0xFFFF);
            return string.Format("0000{0:x4}", langId);
        }
    }

    internal static class Lang
    {
        private static readonly Dictionary<string, string> _nameCache = new Dictionary<string, string>();

        private static string _display(string id8)
        {
            string cached;
            if (_nameCache.TryGetValue(id8, out cached)) return cached;
            string result;
            try
            {
                string hex = id8.Substring(4, 4);
                int lcid = Convert.ToInt32(hex, 16);
                CultureInfo ci = CultureInfo.GetCultureInfo(lcid);
                result = ci.EnglishName;
            }
            catch
            {
                result = "Language 0x" + id8.Substring(4, 4);
            }
            _nameCache[id8] = result;
            return result;
        }

        private static bool _valid(string id8)
        {
            if (id8 == null || id8.Length != 8) return false;
            foreach (char c in id8) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        // Every language Windows can use as an input language: the full NLS catalog
        // (culture-specific locales, e.g. ar-SA, ar-IQ, en-GB...) merged with the
        // keyboard layouts actually installed on this machine.
        public static List<string> Installed()
        {
            HashSet<string> set = new HashSet<string>();
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Keyboard Layouts"))
                {
                    if (k != null)
                    {
                        foreach (string sub in k.GetSubKeyNames())
                        {
                            if (sub.Length >= 8 && _valid(sub))
                            {
                                // An HKL id like "00010409" - the language is the low word.
                                string langId = "0000" + sub.Substring(sub.Length - 4, 4);
                                set.Add(langId);
                            }
                        }
                    }
                }
            }
            catch { }

            // Windows "Add a language" catalog = all specific cultures, with no keyboard
            // required at selection time (Windows downloads the layout when needed).
            CultureInfo[] cultures;
            try { cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures); }
            catch { cultures = new CultureInfo[0]; }
            foreach (CultureInfo ci in cultures)
            {
                int lcid = 0;
                try { lcid = ci.LCID; } catch { }
                if (lcid != 0 && lcid < 0x10000)
                {
                    set.Add(string.Format("0000{0:x4}", lcid));
                }
            }

            List<string> list = new List<string>(set);
            list.Sort(delegate (string a, string b) { return string.Compare(_display(a), _display(b), StringComparison.CurrentCultureIgnoreCase); });
            return list;
        }

        public static string Display(string id8)
        {
            return _display(id8);
        }
    }

    internal sealed class LangItem
    {
        public readonly string Id;
        public LangItem(string id) { Id = id; }
        public override string ToString() { return Lang.Display(Id); }
    }

    internal static class Preload
    {
        private const string KeyPath = @"Keyboard Layout\Preload";

        public static List<string> Get()
        {
            List<string> result = new List<string>();
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    if (k == null) return result;
                    List<int> names = new List<int>();
                    foreach (string n in k.GetValueNames())
                    {
                        int v;
                        if (int.TryParse(n, out v)) names.Add(v);
                    }
                    names.Sort();
                    foreach (int n in names)
                    {
                        object o = k.GetValue(n.ToString());
                        if (o != null) result.Add(o.ToString().ToLowerInvariant());
                    }
                }
            }
            catch { }
            return result;
        }

        // Rewrites the input-language list so it contains exactly [ids], in order.
        public static void Set(List<string> ids)
        {
            try
            {
                RegistryKey k = Registry.CurrentUser.CreateSubKey(KeyPath);
                HashSet<string> allowed = new HashSet<string>(ids);
                List<int> existing = new List<int>();
                foreach (string n in k.GetValueNames())
                {
                    int v;
                    if (int.TryParse(n, out v)) existing.Add(v);
                }
                existing.Sort();

                // First pass: fix (or create) values 1..N; delete any leftover numbers above N.
                for (int i = 0; i < ids.Count; i++)
                {
                    string name = (i + 1).ToString();
                    k.SetValue(name, ids[i], RegistryValueKind.String);
                }
                foreach (int n in existing)
                {
                    if (n > ids.Count) k.DeleteValue(n.ToString(), false);
                }
                // Second pass: drop any extra entries that slipped under a small number
                // (for example after the list shrank) without breaking the order.
                foreach (string n in k.GetValueNames())
                {
                    int v;
                    if (!int.TryParse(n, out v)) continue;
                    if (v > ids.Count) k.DeleteValue(n.ToString(), false);
                }
                k.Flush();
                k.Close();
            }
            catch { }
        }
    }

    internal static class Settings
    {
        private const string Root = @"Software\LanguageGuard";

        public static List<string> GetAllowed()
        {
            List<string> list = new List<string>();
            try
            {
                string raw = (string)Registry.GetValue(Registry.CurrentUser.ToString() + @"\" + Root, "Allowed", "");
                if (!string.IsNullOrEmpty(raw))
                {
                    foreach (string part in raw.Split(','))
                    {
                        string p = part.Trim().ToLowerInvariant();
                        if (p.Length == 8) list.Add(p);
                    }
                }
            }
            catch { }
            return list;
        }

        public static void SetAllowed(List<string> ids)
        {
            string raw = string.Join(",", ids.ToArray());
            try { Registry.SetValue(Registry.CurrentUser.ToString() + @"\" + Root, "Allowed", raw, RegistryValueKind.String); }
            catch { }
        }

        public static bool GetEnabled()
        {
            try { return "1" == (string)Registry.GetValue(Registry.CurrentUser.ToString() + @"\" + Root, "Enabled", "0"); }
            catch { return false; }
        }

        public static void SetEnabled(bool on)
        {
            Registry.SetValue(Registry.CurrentUser.ToString() + @"\" + Root, "Enabled", on ? "1" : "0", RegistryValueKind.String);
        }

        public static bool GetAutoStart()
        {
            try { object o = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", "LanguageGuard", null); return o != null; }
            catch { return false; }
        }

        public static void SetAutoStart(bool on, bool bootMode)
        {
            const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(runKey))
                {
                    if (on)
                    {
                        string arg = bootMode ? "\" --boot" : "\"";
                        k.SetValue("LanguageGuard", "\"" + Application.ExecutablePath + arg);
                    }
                    else k.DeleteValue("LanguageGuard", false);
                }
            }
            catch { }
        }

        public static void Log(string line)
        {
            try
            {
                string stamp = DateTime.Now.ToString("HH:mm:ss");
                string[] rows = {
                    stamp + "  " + line,
                    (string)Registry.GetValue(Registry.CurrentUser.ToString() + @"\" + Root, "Log", "")
                };
                Registry.SetValue(Registry.CurrentUser.ToString() + @"\" + Root, "Log", string.Join("\n", rows), RegistryValueKind.String);
            }
            catch { }
        }

        public static string ReadLog()
        {
            try { return (string)Registry.GetValue(Registry.CurrentUser.ToString() + @"\" + Root, "Log", ""); }
            catch { return ""; }
        }
    }

    internal class Guardian
    {
        private readonly List<string> _allowed = new List<string>();
        private string _lastNotice = "";
        public event Action<string> OnEvent;

        public List<string> AllowedList { get { return _allowed; } }

        public void SetAllowed(IEnumerable<string> ids)
        {
            _allowed.Clear();
            foreach (string id in ids)
            {
                if (id != null && id.Length == 8 && !_allowed.Contains(id))
                    _allowed.Add(id);
            }
        }

        // One enforcement pass: keep Preload equal to Allowed, then force the active
        // keyboard back to an allowed language if Windows drifted.
        public void Enforce()
        {
            if (_allowed.Count == 0) return;

            List<string> preload = Preload.Get();
            HashSet<string> allowedSet = new HashSet<string>(_allowed);

            bool changedPreload = false;
            if (preload.Count != _allowed.Count) changedPreload = true;
            else
            {
                for (int i = 0; i < preload.Count; i++)
                {
                    if (preload[i] != _allowed[i]) { changedPreload = true; break; }
                }
            }

            if (changedPreload)
            {
                _notice("Enforced languages to: " + NamesOf(_allowed));
                Preload.Set(_allowed);
            }

            uint pid;
            IntPtr fg = Native.GetForegroundWindow();
            IntPtr hkl = (fg != IntPtr.Zero)
                ? Native.GetKeyboardLayout(Native.GetWindowThreadProcessId(fg, out pid))
                : Native.GetKeyboardLayout(0);

            string activeId = Native.HklToLangId(hkl);
            if (!allowedSet.Contains(activeId))
            {
                string first = _allowed[0];
                IntPtr h = Native.LoadKeyboardLayout(first, Native.KLF_ACTIVATE | Native.KLF_SUBSTITUTE_OK);
                if (h != IntPtr.Zero)
                {
                    if (fg != IntPtr.Zero) Native.PostMessage(fg, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, h);
                    _notice("Active keyboard " + Lang.Display(activeId) + " reset to " + Lang.Display(first));
                }
            }
        }

        private string NamesOf(List<string> ids)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Lang.Display(ids[i]));
            }
            return sb.ToString();
        }

        private void _notice(string msg)
        {
            if (_lastNotice == msg) return;
            _lastNotice = msg;
            Settings.Log(msg);
            if (OnEvent != null) OnEvent(msg);
        }

        // Inform the shell that the default input language list changed.
        public static void BroadcastDefaultInput()
        {
            try
            {
                IntPtr dflt = (IntPtr)0x04090409;
                Native.SystemParametersInfo(Native.SPI_SETDEFAULTINPUTLANG, 0, dflt, Native.SPIF_SENDWININICHANGE);
            }
            catch { }
        }
    }

    public class MainForm : Form
    {
        private readonly Guardian _guardian = new Guardian();
        private CheckedListBox _lst;
        private TextBox _txtFilter;
        private readonly HashSet<string> _checkedSet = new HashSet<string>();
        private List<string> _catalog = new List<string>();
        private Button _btnEnable;
        private Button _btnDisable;
        private CheckBox _chkAuto;
        private RadioButton _radAlways;
        private RadioButton _radBoot;
        private CheckBox _chkTray;
        private Label _lblStatus;
        private ListBox _lblLog;
        private WTimer _guardTimer;
        private NotifyIcon _tray;
        private ContextMenuStrip _trayMenu;
        private bool _reallyQuit;

        public MainForm()
        {
            Text = "LanguageGuard - Keep only the languages you type in";
            ClientSize = new Size(430, 520);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _guardian.OnEvent += delegate (string m) { AddLog(m); };

            Label cap = new Label();
            cap.Text = "Allowed input languages (checked):";
            cap.Location = new Point(12, 10);
            cap.AutoSize = true;
            Controls.Add(cap);

            _txtFilter = new TextBox();
            _txtFilter.Location = new Point(12, 30);
            _txtFilter.Size = new Size(406, 22);
            _txtFilter.TextChanged += delegate { RebuildList(); };
            Controls.Add(_txtFilter);

            _lst = new CheckedListBox();
            _lst.Location = new Point(12, 56);
            _lst.Size = new Size(406, 196);
            _lst.CheckOnClick = true;
            _lst.ItemCheck += delegate (object s, ItemCheckEventArgs e)
            {
                LangItem it = (LangItem)_lst.Items[e.Index];
                if (e.NewValue == CheckState.Checked) _checkedSet.Add(it.Id); else _checkedSet.Remove(it.Id);
            };
            Controls.Add(_lst);

            _btnEnable = new Button();
            _btnEnable.Text = "Enable & Protect";
            _btnEnable.Location = new Point(12, 260);
            _btnEnable.Size = new Size(195, 34);
            _btnEnable.Click += delegate { EnableGuard(); };
            Controls.Add(_btnEnable);

            _btnDisable = new Button();
            _btnDisable.Text = "Release all";
            _btnDisable.Location = new Point(223, 252);
            _btnDisable.Size = new Size(195, 34);
            _btnDisable.Enabled = false;
            _btnDisable.Click += delegate { DisableGuard(); };
            Controls.Add(_btnDisable);

            _chkAuto = new CheckBox();
            _chkAuto.Text = "Start automatically with Windows";
            _chkAuto.Location = new Point(12, 302);
            _chkAuto.AutoSize = true;
            _chkAuto.Checked = Settings.GetAutoStart();
            _chkAuto.CheckedChanged += delegate { Settings.SetAutoStart(_chkAuto.Checked, _radBoot.Checked); };
            Controls.Add(_chkAuto);

            _radAlways = new RadioButton();
            _radAlways.Text = "Keep watching in the background (recommended)";
            _radAlways.Location = new Point(12, 330);
            _radAlways.AutoSize = true;
            _radAlways.Checked = true;
            _radAlways.CheckedChanged += delegate { if (_chkAuto.Checked) Settings.SetAutoStart(true, _radBoot.Checked); };
            Controls.Add(_radAlways);

            _radBoot = new RadioButton();
            _radBoot.Text = "Or: fix languages once at sign-in, then exit";
            _radBoot.Location = new Point(12, 354);
            _radBoot.AutoSize = true;
            _radBoot.CheckedChanged += delegate { if (_chkAuto.Checked) Settings.SetAutoStart(true, _radBoot.Checked); };
            Controls.Add(_radBoot);

            _chkTray = new CheckBox();
            _chkTray.Text = "Minimize to tray (guard keeps running)";
            _chkTray.Location = new Point(12, 380);
            _chkTray.AutoSize = true;
            _chkTray.Checked = true;
            Controls.Add(_chkTray);

            _lblStatus = new Label();
            _lblStatus.Location = new Point(12, 408);
            _lblStatus.AutoSize = true;
            _lblStatus.ForeColor = Color.DimGray;
            Controls.Add(_lblStatus);

            Label logCap = new Label();
            logCap.Text = "Activity:";
            logCap.Location = new Point(12, 432);
            logCap.AutoSize = true;
            Controls.Add(logCap);

            _lblLog = new ListBox();
            _lblLog.Location = new Point(12, 452);
            _lblLog.Size = new Size(406, 56);
            _lblLog.HorizontalScrollbar = true;
            Controls.Add(_lblLog);

            _guardTimer = new WTimer();
            _guardTimer.Interval = 2000; // 2 s
            _guardTimer.Tick += delegate { _guardian.Enforce(); };

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Show LanguageGuard", null, delegate { Show(); WindowState = FormWindowState.Normal; });
            _trayMenu.Items.Add("Exit", null, delegate { _reallyQuit = true; Close(); });
            _tray = new NotifyIcon();
            try { _tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            _tray.Text = "LanguageGuard - protecting input languages";
            _tray.ContextMenuStrip = _trayMenu;
            _tray.Visible = true;
            _tray.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; };

            FormClosing += delegate (object s, FormClosingEventArgs e)
            {
                if (!_reallyQuit && _chkTray.Checked)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
        }

        private void AddLog(string m)
        {
            if (_lblLog.Items.Count > 300) _lblLog.Items.Clear();
            _lblLog.Items.Insert(0, m);
        }

        private void RebuildList()
        {
            string f = _txtFilter != null ? _txtFilter.Text.Trim() : "";
            _lst.BeginUpdate();
            _lst.Items.Clear();
            foreach (string id in _catalog)
            {
                if (f.Length == 0 || Lang.Display(id).IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    _lst.Items.Add(new LangItem(id), _checkedSet.Contains(id));
                }
            }
            _lst.EndUpdate();
        }

        private void EnableGuard()
        {
            List<string> chosen = new List<string>();
            foreach (string id in _checkedSet)
            {
                chosen.Add(id);
            }
            if (chosen.Count == 0)
            {
                MessageBox.Show("Select at least one language to protect.", "LanguageGuard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _guardian.SetAllowed(chosen);
            Settings.SetAllowed(chosen);
            Settings.SetEnabled(true);
            _guardian.Enforce();
            Guardian.BroadcastDefaultInput();
            _guardTimer.Start();
            _btnEnable.Enabled = false;
            _btnDisable.Enabled = true;
            SetStatus(true);
            string names = string.Join(", ", Lang.Display(chosen[0]) + (chosen.Count > 1 ? ", ..." : ""));
            _tray.BalloonTipTitle = "LanguageGuard";
            _tray.BalloonTipText = "Protecting " + chosen.Count + " language(s): " + names;
            try { _tray.ShowBalloonTip(3000); } catch { }
            AddLog("Protection ON for: " + names);
            if (_chkTray.Checked && !ShowInTaskbar)
            {
                Show();
                Hide();
            }
        }

        private void DisableGuard()
        {
            _guardTimer.Stop();
            Settings.SetEnabled(false);
            _btnEnable.Enabled = true;
            _btnDisable.Enabled = false;
            SetStatus(false);
            _tray.BalloonTipTitle = "LanguageGuard";
            _tray.BalloonTipText = "Protection OFF - Windows is back in charge of languages.";
            _tray.ShowBalloonTip(2000);
            AddLog("Protection OFF.");
        }

        private void SetStatus(bool on)
        {
            _lblStatus.Text = on
                ? "State: PROTECTED (" + _guardian.AllowedList.Count + " languages, checked every 2 s)"
                : "State: idle - choose languages and press Enable & Protect.";
        }

        public void EnsureLoaded()
        {
            List<string> allowed = Settings.GetAllowed();
            bool enabled = Settings.GetEnabled();

            if (allowed.Count == 0 && !enabled)
            {
                // Cold start, nothing configured yet: show the current languages
                // as pre-selected suggestions but do NOT start protecting anything.
                allowed = Preload.Get();
            }

            _catalog = Lang.Installed();
            _checkedSet.Clear();
            foreach (string id in allowed) _checkedSet.Add(id);
            RebuildList();

            if (enabled)
            {
                if (allowed.Count == 0)
                {
                    // Corrupt/missing allow-list while protection was supposed to be on:
                    // stop protection instead of silently guarding whatever is in Preload
                    // (that could include a language Windows auto-added).
                    Settings.SetEnabled(false);
                    allowed = Preload.Get();
                    _btnEnable.Enabled = true;
                    _btnDisable.Enabled = false;
                    SetStatus(false);
                    AddLog("Saved language list was unreadable - protection OFF. Re-select and press Enable & Protect.");
                }
                else
                {
                    _guardian.SetAllowed(allowed);
                    _guardTimer.Start();
                    _btnEnable.Enabled = false;
                    _btnDisable.Enabled = true;
                    SetStatus(true);
                    AddLog("Resumed from saved settings. Protection active.");
                }
            }
            else
            {
                SetStatus(false);
            }

            string log = Settings.ReadLog();
            if (!string.IsNullOrEmpty(log))
            {
                foreach (string line in log.Split('\n'))
                {
                    if (!string.IsNullOrEmpty(line)) _lblLog.Items.Add(line);
                }
            }
        }

        public void RunAfterLoad()
        {
            // If protection was previously on, start quietly in the tray.
            if (Settings.GetEnabled() && _guardian.AllowedList.Count > 0 && _chkTray.Checked)
            {
                _tray.BalloonTipTitle = "LanguageGuard";
                _tray.BalloonTipText = "Protecting " + _guardian.AllowedList.Count + " language(s).";
                try { _tray.ShowBalloonTip(2000); } catch { }
                Hide();
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool boot = Array.IndexOf(args, "--boot") >= 0;
            bool created;
            using (Mutex m = new Mutex(true, "Local\\LanguageGuardSingleInstance", out created))
            {
                if (!created)
                {
                    // A full instance is already running - nothing to do.
                    return;
                }
                if (boot)
                {
                    RunBootCheck();
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                MainForm f = new MainForm();
                f.Shown += delegate { f.EnsureLoaded(); f.RunAfterLoad(); };
                Application.Run(f);
            }
        }

        // Silent mode started from the Run key: enforce the saved list for ~90 seconds
        // (long enough to revert anything Windows re-added at logon), then exit quietly.
        private static void RunBootCheck()
        {
            if (!Settings.GetEnabled()) return;
            List<string> allowed = Settings.GetAllowed();
            if (allowed.Count == 0) return;

            Guardian g = new Guardian();
            g.SetAllowed(allowed);
            Application.EnableVisualStyles();
            ApplicationContext ctx = new ApplicationContext();
            WTimer t = new WTimer();
            t.Interval = 2000;
            int ticks = 0;
            t.Tick += delegate
            {
                try { g.Enforce(); } catch { }
                ticks++;
                if (ticks >= 45) { t.Stop(); ctx.ExitThread(); }
            };
            t.Start();
            try { g.Enforce(); } catch { }
            Application.Run(ctx);
        }
    }
}