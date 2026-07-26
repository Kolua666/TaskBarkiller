using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TaskBarkiller
{
    static class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        static bool isHidden = false;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            SetStartup();

            uint modifiers;
            Keys key;
            LoadConfig(out modifiers, out key);

            Application.Run(new HiddenContext(modifiers, key));
        }

        public static void ToggleTaskbar()
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            IntPtr startButton = FindWindow("Button", "Start");
            IntPtr secondaryTaskbar = FindWindow("Shell_SecondaryTrayWnd", null);

            isHidden = !isHidden;
            int command = isHidden ? SW_HIDE : SW_SHOW;

            if (taskbar != IntPtr.Zero) ShowWindow(taskbar, command);
            if (startButton != IntPtr.Zero) ShowWindow(startButton, command);
            if (secondaryTaskbar != IntPtr.Zero) ShowWindow(secondaryTaskbar, command);
        }

        public static void ShowTaskbar()
        {
            if (isHidden)
            {
                ToggleTaskbar();
            }
        }

        static void SetStartup()
        {
            try
            {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                rk.SetValue("TaskBarkiller", Application.ExecutablePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not add to startup: " + ex.Message, "TaskBarkiller Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static void LoadConfig(out uint modifiers, out Keys key)
        {
            string configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            
            // Defaults: Alt (1) + Shift (4) = 5, F12
            // MOD_ALT = 0x0001
            // MOD_CONTROL = 0x0002
            // MOD_SHIFT = 0x0004
            // MOD_WIN = 0x0008
            
            modifiers = 5;
            key = Keys.F12;

            if (!File.Exists(configFile))
            {
                string defaultConfig = @"[Hotkey]
# Modifiers: Alt=1, Ctrl=2, Shift=4, Win=8. Add them together for combinations.
# Example: Alt + Shift = 1 + 4 = 5
Modifiers=5
# Key: F12, A, B, C etc.
Key=F12
";
                File.WriteAllText(configFile, defaultConfig);
            }
            else
            {
                try
                {
                    string[] lines = File.ReadAllLines(configFile);
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("Modifiers="))
                        {
                            uint.TryParse(line.Substring(10).Trim(), out modifiers);
                        }
                        else if (line.StartsWith("Key="))
                        {
                            Enum.TryParse(line.Substring(4).Trim(), true, out key);
                        }
                    }
                }
                catch { }
            }
        }

        class HiddenContext : ApplicationContext
        {
            HotKeyForm form;
            NotifyIcon trayIcon;

            public HiddenContext(uint modifiers, Keys key)
            {
                form = new HotKeyForm(modifiers, key);
                
                trayIcon = new NotifyIcon();
                trayIcon.Text = "TaskBarkiller";
                trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                
                ContextMenu trayMenu = new ContextMenu();
                trayMenu.MenuItems.Add("Exit", OnExit);
                trayIcon.ContextMenu = trayMenu;
                trayIcon.Visible = true;
            }

            void OnExit(object sender, EventArgs e)
            {
                Program.ShowTaskbar();
                trayIcon.Visible = false;
                Application.Exit();
            }
            
            protected override void Dispose(bool disposing)
            {
                if (form != null)
                {
                    form.Dispose();
                }
                if (trayIcon != null)
                {
                    trayIcon.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        class HotKeyForm : Form
        {
            [DllImport("user32.dll")]
            public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

            [DllImport("user32.dll")]
            public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

            const int MY_HOTKEY_ID = 1;

            public HotKeyForm(uint modifiers, Keys key)
            {
                this.CreateHandle(); // Ensure handle is created
                if (!RegisterHotKey(this.Handle, MY_HOTKEY_ID, modifiers, (uint)key))
                {
                    MessageBox.Show("Could not register hotkey. It might be in use by another program. Please change it in config.ini and restart.", "TaskBarkiller Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Application.Exit();
                }
            }

            protected override void WndProc(ref Message m)
            {
                const int WM_HOTKEY = 0x0312;
                if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == MY_HOTKEY_ID)
                {
                    Program.ToggleTaskbar();
                }
                base.WndProc(ref m);
            }

            protected override void SetVisibleCore(bool value)
            {
                base.SetVisibleCore(false); // Keep form hidden
            }

            protected override void OnClosed(EventArgs e)
            {
                UnregisterHotKey(this.Handle, MY_HOTKEY_ID);
                base.OnClosed(e);
            }
        }
    }
}
