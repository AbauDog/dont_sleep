using System.ComponentModel;
using System.Diagnostics;
using dont_sleep.Services;
using dont_sleep.Models;

namespace dont_sleep.UI
{
    public class MainForm : Form
    {
        private HardwareMonitor? _hardwareMonitor;
        private System.Windows.Forms.Timer? _updateTimer;
        private System.Windows.Forms.Timer? _idleTimer;
        private NotifyIcon? _notifyIcon;
        private IContainer? _components;

        // Controls
        private Label? _lblCpu;
        private Label? _lblRam;
        private Label? _lblDisk;
        private Label? _lblTopProc;
        private GroupBox? _grpSettings;
        private RadioButton? _rbKeepAwake;
        private RadioButton? _rbScreensaver;
        private TextBox? _txtTimeout;
        private TextBox? _txtPassword;
        private Label? _lblTimeout;
        private Label? _lblPassword;
        private Label? _lblMinutes;
        private Button? _btnToggleSettings;
        private Label? _lblCountdown;
        private bool _settingsVisible = false;
        
        // Custom title bar controls
        private Panel? _titleBar;
        private Label? _lblTitle;
        private Button? _btnToggleStats;
        private Button? _btnClose;
        private Button? _btnMinimize;
        private Point _lastMousePos;
        private bool _statsVisible = true;

        // State
        private bool _isSimulationActive = false;
        
        // Defaults
        private int _idleTimeoutMin = 3;
        private string _password = "`";

        public MainForm()
        {
            InitializeComponent();
            _hardwareMonitor = new HardwareMonitor();
            
            // Apply Settings (could load from file, using defaults for now)
            UpdateSettingsFromUI();
            
            // Start KeepAwake by default? 
            // Req: "2.1.2 ... Default: Simulate Screensaver"
            // So _rbScreensaver should be checked by default.
            _rbScreensaver!.Checked = true;
            HandleModeChange();

            // Timers
            _updateTimer = new System.Windows.Forms.Timer();
            _updateTimer.Interval = 1000;
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();

            _idleTimer = new System.Windows.Forms.Timer();
            _idleTimer.Interval = 1000;
            _idleTimer.Tick += IdleTimer_Tick;
            _idleTimer.Start();
        }

        private void InitializeComponent()
        {
            _components = new Container();
            this.Text = "dont_sleep";
            this.Size = new Size(400, 185);  // 增加高度以容納標題列
            this.BackColor = Color.Black;
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.None;  // 移除系統標題列
            this.StartPosition = FormStartPosition.Manual; // 手動設定位置
            
            // 計算右上角位置 (考慮工作區域，避開工作列)
            Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.AllScreens[0].WorkingArea;
            this.Location = new Point(workingArea.Right - this.Width, workingArea.Top);
            
            // 建立自訂標題列
            _titleBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(400, 30),
                BackColor = Color.Black,
                Cursor = Cursors.SizeAll
            };
            _titleBar.MouseDown += TitleBar_MouseDown;
            _titleBar.MouseMove += TitleBar_MouseMove;
            // 繪製標題列底部分隔線
            _titleBar.Paint += (s, e) => 
            {
                using var p = new Pen(Color.DimGray, 1);
                e.Graphics.DrawLine(p, 0, _titleBar.Height - 1, _titleBar.Width, _titleBar.Height - 1);
            };
            
            _btnToggleStats = new Button
            {
                Text = "▼",
                Location = new Point(5, 5), // 移到最前面
                Size = new Size(25, 20),
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnToggleStats.FlatAppearance.BorderSize = 0;
            _btnToggleStats.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 40, 40);
            _btnToggleStats.Click += (s, e) => ToggleStats_Click();

            _lblTitle = new Label
            {
                Text = "dont_sleep",
                Location = new Point(28, 7), // 緊接在按鈕後方
                AutoSize = true,
                ForeColor = Color.White,  // 白色文字
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            
            _btnMinimize = new Button
            {
                Text = "", // 移除文字，改用 Paint 繪製
                Location = new Point(320, 3),
                Size = new Size(35, 24),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnMinimize.FlatAppearance.BorderSize = 0;
            _btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            // 自訂繪製最小化符號 (水平線)
            _btnMinimize.Paint += (s, e) => 
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var pen = new Pen(Color.White, 2);
                int y = _btnMinimize.Height / 2 + 3;
                int w = 12;
                int x = (_btnMinimize.Width - w) / 2;
                e.Graphics.DrawLine(pen, x, y, x + w, y);
            };
            
            _btnClose = new Button
            {
                Text = "", // 移除文字，改用 Paint 繪製
                Location = new Point(360, 3), 
                Size = new Size(32, 24), 
                BackColor = Color.FromArgb(150, 0, 0), // 暗紅色
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.Click += (s, e) => this.Close();
            // 自訂繪製關閉符號 (大叉叉)
            _btnClose.Paint += (s, e) => 
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var pen = new Pen(Color.White, 2);
                int offset = 8; // 邊距，越小 X 越大
                int w = _btnClose.Width;
                int h = _btnClose.Height;
                e.Graphics.DrawLine(pen, offset, offset, w - offset, h - offset);
                e.Graphics.DrawLine(pen, w - offset, offset, offset, h - offset);
            };
            
            _titleBar.Controls.Add(_lblTitle);
            _titleBar.Controls.Add(_btnToggleStats);
            _titleBar.Controls.Add(_btnMinimize);
            _titleBar.Controls.Add(_btnClose);
            this.Controls.Add(_titleBar);

            // Notify Icon
            _notifyIcon = new NotifyIcon(_components);
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Text = "dont_sleep";
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += (s, e) => {
                this.Show();
                this.WindowState = FormWindowState.Normal;
            };

            // Fonts
            Font fontLarge = new Font("Segoe UI", 14, FontStyle.Bold);
            Font fontNormal = new Font("Segoe UI", 10);
            
            // CPU, RAM, DISK in one row - increased width to show percentages
            int labelWidth = 120;
            int topOffset = 35;  // 增加偏移以避開標題列
            _lblCpu = new Label { Location = new Point(20, topOffset), Size = new Size(labelWidth, 25), Font = fontLarge, Text = "CPU: --%" };
            _lblRam = new Label { Location = new Point(135, topOffset), Size = new Size(labelWidth, 25), Font = fontLarge, Text = "RAM: --%" };
            _lblDisk = new Label { Location = new Point(250, topOffset), Size = new Size(labelWidth, 25), Font = fontLarge, Text = "DISK: --%" };
            
            // Top Processes - reduced spacing
            _lblTopProc = new Label { Location = new Point(20, topOffset + 25), Size = new Size(350, 80), Font = fontNormal, Text = "Top 3 記憶體 (各獨立進程):\n...", AutoSize = false };

            // Toggle Settings Button - transparent background, text only
            _btnToggleSettings = new Button { 
                Location = new Point(20, 145),  // 調整位置確保可見
                Size = new Size(90, 25), // 縮小寬度，避免遮擋右側 Label
                Text = "▼ 設定", 
                BackColor = Color.Transparent, 
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand
            };
            _btnToggleSettings.FlatAppearance.BorderSize = 0;
            _btnToggleSettings.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 40, 40);
            _btnToggleSettings.Click += ToggleSettings_Click;

            // Settings Group - initially hidden, reduced spacing
            _grpSettings = new GroupBox { 
                Location = new Point(20, 175),  // 調整到設定按鈕下方
                Size = new Size(350, 150), 
                Text = "", 
                ForeColor = Color.White,
                Visible = false  // Hidden by default
            };
            
            _rbKeepAwake = new RadioButton { Location = new Point(20, 15), Text = "醒者模式 (Keep Awake)", AutoSize = true };
            _rbScreensaver = new RadioButton { Location = new Point(20, 38), Text = "模擬螢幕保護", AutoSize = true };
            _rbScreensaver.CheckedChanged += (s, e) => HandleModeChange();

            _lblTimeout = new Label { Location = new Point(40, 65), Text = "閒置時間:", AutoSize = true };
            _txtTimeout = new TextBox { Location = new Point(110, 63), Size = new Size(50, 23), Text = "3", BackColor = Color.FromArgb(64,64,64), ForeColor = Color.White };
            _lblMinutes = new Label { Location = new Point(165, 66), AutoSize = true, ForeColor = Color.White, Text = "分" };

            _lblPassword = new Label { Location = new Point(30, 96), AutoSize = true, ForeColor = Color.White, Text = "解鎖密碼:" };
            _txtPassword = new TextBox { Location = new Point(110, 93), Size = new Size(100, 23), Text = "z", BackColor = Color.FromArgb(64,64,64), ForeColor = Color.White };
            
            // 倒數計時 Label - 位於「設定」按鈕右側，靠右對齊
            _lblCountdown = new Label
            {
                AutoSize = false, // 關閉自動大小以支援靠右對齊
                Size = new Size(160, 25),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(230, 148),  // 初始位置 (400 - 160 - 10)
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                Text = "",
                Visible = false
            };
            
            _grpSettings.Controls.Add(_rbKeepAwake);
            _grpSettings.Controls.Add(_rbScreensaver);
            _grpSettings.Controls.Add(_lblTimeout);
            _grpSettings.Controls.Add(_lblPassword);
            _grpSettings.Controls.Add(_txtTimeout);
            _grpSettings.Controls.Add(_lblMinutes);
            _grpSettings.Controls.Add(_txtPassword);

            // Apply settings on text changed
            _txtTimeout.TextChanged += (s, e) => UpdateSettingsFromUI();
            _txtPassword.TextChanged += (s, e) => UpdateSettingsFromUI();

            this.Controls.Add(_lblCpu);
            this.Controls.Add(_lblRam);
            this.Controls.Add(_lblDisk);
            this.Controls.Add(_lblTopProc);
            this.Controls.Add(_btnToggleSettings);
            this.Controls.Add(_lblCountdown);  // 倒數計時加入主視窗
            this.Controls.Add(_grpSettings);

            this.Resize += MainForm_Resize;

            // 初始化佈局
            UpdateWindowLayout();
        }

        private void ToggleSettings_Click(object? sender, EventArgs e)
        {
            _settingsVisible = !_settingsVisible;
            UpdateWindowLayout();
        }

        private void ToggleStats_Click()
        {
            _statsVisible = !_statsVisible;
            UpdateWindowLayout();
        }

        private void UpdateWindowLayout()
        {
            // 基礎高度 (標題列 30 + 狀態列含間距 35)
            int currentY = 60;

            if (_statsVisible)
            {
                _lblTopProc!.Visible = true;
                _btnToggleSettings!.Visible = true;
                
                _lblTopProc.Location = new Point(20, 60);
                currentY += 85; // 列表高度 80 + 間距 5

                _btnToggleStats!.Text = _statsVisible ? "▲" : "▼";

            // 設定按鈕位置
            _btnToggleSettings!.Location = new Point(20, currentY);
            _btnToggleSettings.Text = _settingsVisible ? "▲ 設定" : "▼ 設定";
            
            // 倒數計時 Label 跟隨按鈕並靠右對齊
            if (_lblCountdown != null)
            {
                int rightMargin = 15;
                int x = this.Width - _lblCountdown.Width - rightMargin;
                _lblCountdown.Location = new Point(x, currentY); 
            }
            
            currentY += 30; // 按鈕高度 25 + 間距 5

            if (_settingsVisible)
                {
                    _grpSettings!.Visible = true;
                    _grpSettings.Location = new Point(20, currentY);
                    currentY += 155; // GroupBox 高度 150 + 底部留白 5
                }
                else
                {
                    _grpSettings!.Visible = false;
                    currentY += 10; // 底部留白
                }
            }
            else
            {
                // 列表摺疊時，隱藏列表、隱藏設定按鈕、隱藏設定群組
                _lblTopProc!.Visible = false;
                _btnToggleSettings!.Visible = false;
                _grpSettings!.Visible = false;
                
                currentY = 65; // 極簡高度，僅顯示 CPU/RAM/DISK
            }

            _btnToggleStats!.Text = _statsVisible ? "▲" : "▼";
            this.Height = currentY;
        }

        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _lastMousePos = e.Location;
            }
        }

        private void TitleBar_MouseMove(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Left += e.X - _lastMousePos.X;
                this.Top += e.Y - _lastMousePos.Y;
            }
        }

        private void MainForm_Resize(object? sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
            }
        }

        private void UpdateSettingsFromUI()
        {
            if (int.TryParse(_txtTimeout!.Text, out int min))
            {
                _idleTimeoutMin = min;
            }
            _password = _txtPassword!.Text;
        }

        private void HandleModeChange()
        {
            if (_rbKeepAwake!.Checked)
            {
                // Keep Awake + Keep Display On
                PowerManager.KeepAwake(true);
            }
            else
            {
                // Screensaver Mode: 
                // We MUST use KeepAwake(true) (Display Required) to preventing Windows from starting its own Screensaver or Locking the workstation.
                // We will manually manage the Monitor Off state.
                PowerManager.KeepAwake(true);
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Get Stats
            var (cpu, ram, disk, isSwapping) = _hardwareMonitor!.GetUsage();
            var topProcs = _hardwareMonitor.GetTop3MemoryProcesses();

            // Update UI Labels with Colors
            UpdateLabel(_lblCpu!, "CPU", cpu);
            UpdateLabel(_lblRam!, "RAM", ram, isSwapping); // 傳遞 SWAP 狀態
            UpdateLabel(_lblDisk!, "DISK", disk);

            // Update Top Proc
            string procText = "";
            foreach (var p in topProcs)
            {
                // Format: <RAM Usage> <Name+(Threads)>
                // Convert bytes to MB
                double mb = p.MemoryBytes / 1024.0 / 1024.0;
                procText += $"{mb,5:F0} MB  {p.Name}\n";
            }
            _lblTopProc!.Text = procText;

            // Update Tray Tooltip
            string tip = $"C{cpu:F0}% R{ram:F0}% D{disk:F0}%";
            if (tip.Length >= 64) tip = tip.Substring(0, 63); // Limit 64 chars
            _notifyIcon!.Text = tip;
        }

        private void UpdateLabel(Label lbl, string name, float val, bool forceRed = false)
        {
            // 當數值 >= 100 時不顯示小數點，避免文字過長
            string format = val >= 100 ? "F0" : "F1";
            lbl.Text = $"{name}: {val.ToString(format)}%";
            
            // Color Logic
            Color c = Color.Lime; // Bright Green
            
            // 如果 forceRed = true (正在使用 SWAP)，強制顯示紅色
            if (forceRed)
            {
                c = Color.Red;
            }
            else
            {
                // 正常顏色判斷
                // Green (<70), Orange (70-85), Red (>85)
                if (val > 85) c = Color.Red;
                else if (val > 70) c = Color.Orange;
            }
            
            lbl.ForeColor = c;
        }

        private void IdleTimer_Tick(object? sender, EventArgs e)
        {
            if (_rbScreensaver!.Checked)
            {
                // Check Idle
                TimeSpan idle = InputListener.GetIdleTime();
                
                // 計算剩餘時間
                double remainingMinutes = _idleTimeoutMin - idle.TotalMinutes;
                
                if (remainingMinutes > 0)
                {
                    // 顯示倒數計時
                    int remainingSeconds = (int)(remainingMinutes * 60);
                    int mins = remainingSeconds / 60;
                    int secs = remainingSeconds % 60;
                    _lblCountdown!.Text = $"進入鎖定: {mins:D2}:{secs:D2}";
                    _lblCountdown.Visible = true;
                }
                else
                {
                    _lblCountdown!.Visible = false;
                }
                
                // If idle enough AND not already active
                if (idle.TotalMinutes >= _idleTimeoutMin && !_isSimulationActive)
                {
                    _isSimulationActive = true;
                    StartScreensaverSimulation();
                }
                // Reset flag if user is active (idle < timeout)
                // This logic is tricky: if we are in simulation, simulation handles input and closes itself.
                // When simulation closes, we need to reset _isSimulationActive.
                // But wait, simulation is a modal-like state.
                // Better approach: Let LockScreenForm manage the state, or use a callback.
            }
            else
            {
                // 非模擬螢幕保護模式，隱藏倒數計時
                _lblCountdown!.Visible = false;
            }
        }

        private void StartScreensaverSimulation()
        {
            // LockScreenForm handles multi-instance internal state
            LockScreenForm.ShowOnAllScreens(_password);
            
            // Wait for close? No, ShowOnAllScreens returns immediately (Show()).
            // We need a mechanism to know when it closes.
            // Let's create an event or pass a callback to LockScreenForm.
            // Simplified: Poll valid state or just assume it stays active until IdleTime < Timeout? 
            // No, user unlocks it manually.
            
            // Re-implementing LockScreenForm.OnClosed event is cleaner. 
            // Or add a public event to LockScreenForm static class.
            LockScreenForm.OnAllClosed += () => _isSimulationActive = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _components?.Dispose();
                _hardwareMonitor?.Dispose();
                _notifyIcon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
