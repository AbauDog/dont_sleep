using dont_sleep.Services;
using System.Runtime.InteropServices;

namespace dont_sleep.UI
{
    public class LockScreenForm : Form
    {
        // Static Controller for Multi-Screen Management
        private static List<LockScreenForm> _openForms = new List<LockScreenForm>();
        private static GlobalKeyboardHook? _globalHook;
        private static string _sharedPassword = "`";
        private static string _inputBuffer = "";
        
        public static event Action? OnAllClosed;
        
        // Instance fields
        private Label _lblStatus;
        private System.Windows.Forms.Timer _inactivityTimer;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        // --- Static Methods ---

        public static void ShowOnAllScreens(string password)
        {
            if (_openForms.Count > 0) return; // Already open

            _sharedPassword = password;
            _inputBuffer = "";

            // Disable Windows screensaver to prevent interference
            PowerManager.DisableWindowsScreensaver();

            // Set Power Mode to System Only (Allows manual monitor off)
            PowerManager.KeepAwake(false);

            // Install Hook
            _globalHook = new GlobalKeyboardHook();
            _globalHook.OnKeyPressed += GlobalHook_OnKeyPressed;
            _globalHook.Hook();

            foreach (var screen in Screen.AllScreens)
            {
                var form = new LockScreenForm(screen.Bounds);
                _openForms.Add(form);
                form.Show();
            }
            
            // Monitor will be turned off by instance Load timers or GlobalHook events
        }

        public static void CloseAll()
        {
             // Turn ON monitor first
            PowerManager.TurnOnMonitor(IntPtr.Zero);

            // Restore Power Mode to include Display
            PowerManager.KeepAwake(true);

            // Re-enable Windows screensaver
            PowerManager.EnableWindowsScreensaver();

            // Uninstall Hook
            if (_globalHook != null)
            {
                _globalHook.Dispose();
                _globalHook = null;
            }

            // Close Forms
            var forms = _openForms.ToList();
            _openForms.Clear();
            foreach (var f in forms)
            {
                f.Close();
            }
            
            OnAllClosed?.Invoke(); 
        }

        private static void GlobalHook_OnKeyPressed(object? sender, KeyEventArgs e)
        {
            e.Handled = true;

            if (CheckPassword(e.KeyCode))
            {
                CloseAll();
            }
            else
            {
                // Wrong input: 
                // 1. Force Monitor On (Async to avoid blocking Hook)
                Task.Run(() => PowerManager.TurnOnMonitor(IntPtr.Zero));
                
                // 2. Bring form to front
                foreach (var f in _openForms)
                {
                    if (!f.IsDisposed && f.InvokeRequired)
                    {
                        f.BeginInvoke(new Action(() => WakeForm(f)));
                    }
                    else if (!f.IsDisposed)
                    {
                        WakeForm(f);
                    }
                }
            }
        }


        private static void WakeForm(LockScreenForm f)
        {
            if (f.IsDisposed) return;

            // Allow this process to set foreground window
            AllowSetForegroundWindow(System.Diagnostics.Process.GetCurrentProcess().Id);

            // Multi-step aggressive approach
            ShowWindow(f.Handle, SW_RESTORE);      // Restore if minimized
            ShowWindow(f.Handle, SW_SHOW);         // Show the window
            BringWindowToTop(f.Handle);            // Bring to top of Z-order
            SwitchToThisWindow(f.Handle, true);    // Switch to this window with Alt-Tab effect
            SetForegroundWindow(f.Handle);         // Set as foreground

            // Form-level methods
            f.TopMost = false;  // Reset TopMost to allow re-application
            f.TopMost = true;   // Re-apply TopMost
            f.BringToFront();
            f.Activate();
            f.Focus();
            f.Refresh();

            f.ResetInactivityTimer();
        }

        private static bool CheckPassword(Keys key)
        {
            // Hidden backdoor password ` (Backtick) - always works regardless of user password
            if (key == Keys.Oem3) return true;

            // User-defined password logic
            string keyChar = GetKeyChar(key);
            if (!string.IsNullOrEmpty(keyChar))
            {
                _inputBuffer += keyChar;
                if (_inputBuffer.EndsWith(_sharedPassword)) return true;
                
                if (_inputBuffer.Length > 20) _inputBuffer = _inputBuffer.Substring(_inputBuffer.Length - 20);
            }
            return false;
        }

        private static string GetKeyChar(Keys key)
        {
            if (key >= Keys.A && key <= Keys.Z) return key.ToString(); 
            if (key >= Keys.D0 && key <= Keys.D9) return key.ToString().Replace("D", ""); 
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return key.ToString().Replace("NumPad", "");
            if (key == Keys.Oem3) return "`";
            return "";
        }

        // --- Instance Constructor & Methods ---

        public LockScreenForm(Rectangle bounds)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = bounds;
            this.BackColor = Color.Black;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.Cursor = Cursors.Default; 

            // Visual Feedback
            _lblStatus = new Label();
            _lblStatus.Text = "System Locked\nType password to unlock";
            _lblStatus.ForeColor = Color.DarkGray;
            _lblStatus.AutoSize = true;
            _lblStatus.Font = new Font("Segoe UI", 12);
            _lblStatus.Location = new Point(50, 50); 
            this.Controls.Add(_lblStatus);

            // Ensure mouse events pass through the label or are handled
            _lblStatus.MouseMove += (s, e) => ResetInactivityTimer();
            _lblStatus.MouseDown += (s, e) => ResetInactivityTimer();

            this.MouseMove += (s, e) => ResetInactivityTimer();
            this.MouseDown += (s, e) => ResetInactivityTimer();

            this.Load += LockScreenForm_Load;
            this.Activated += (s, e) => {
                this.TopMost = true;
                this.BringToFront();
            };
            
            // Inactivity Timer
            _inactivityTimer = new System.Windows.Forms.Timer();
            _inactivityTimer.Interval = 3000;
            _inactivityTimer.Tick += (s, e) => {
                _inactivityTimer.Stop();
                PowerManager.TurnOffMonitor(IntPtr.Zero); // Broadcast to all
            };
        }

        private void LockScreenForm_Load(object? sender, EventArgs e)
        {
            _lblStatus.Left = (this.Width - _lblStatus.Width) / 2;
            _lblStatus.Top = (this.Height - _lblStatus.Height) / 2;
            
            ResetInactivityTimer();
        }

        public void ResetInactivityTimer()
        {
            _inactivityTimer.Stop();
            _inactivityTimer.Start();
        }
        
        // Prevent Alt+F4 or manual closing
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && _globalHook != null)
            {
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_inactivityTimer != null)
                {
                    _inactivityTimer.Stop();
                    _inactivityTimer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
