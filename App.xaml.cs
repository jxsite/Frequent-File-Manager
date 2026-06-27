using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace SidebarApp
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. Single Instance Check
            const string appGuid = "SidebarApp-Unique-Guid-2026";
            _mutex = new Mutex(true, appGuid, out bool isNewInstance);
            if (!isNewInstance)
            {
                MessageBox.Show("侧边栏管理工具已在后台运行！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // 2. Initialize Configuration
            ConfigHelper.Load();
            AutoStartHelper.SetAutoStart(ConfigHelper.Current.AutoStart);

            // Apply Theme & Language on Startup
            ThemeHelper.ApplyTheme(ConfigHelper.Current.Theme);
            LanguageHelper.ApplyLanguage(ConfigHelper.Current.Language);

            // 3. Setup System Tray Icon
            InitializeTrayIcon();

            // 4. Show MainWindow
            _mainWindow = new MainWindow();
            _mainWindow.Show();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon();
            // Use standard system folder/application icon
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Text = "悬浮侧边栏文件夹管理";
            _notifyIcon.Visible = true;

            // Context Menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("显示/隐藏侧边栏", null, (s, e) => ToggleMainWindow());
            contextMenu.Items.Add("设置", null, (s, e) => ShowSettingsWindow());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("退出", null, (s, e) => ExitApplication());
            _notifyIcon.ContextMenuStrip = contextMenu;

            // Double Click
            _notifyIcon.DoubleClick += (s, e) => ToggleMainWindow();
        }

        public void ToggleMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
                _mainWindow.Show();
            }
            else
            {
                if (!_mainWindow.IsVisible)
                {
                    _mainWindow.ApplyWindowPosition(true); // Force position on screen containing mouse cursor
                    _mainWindow.Show();
                    _mainWindow.Activate();
                    _mainWindow.TriggerSlideIn();
                }
                else
                {
                    if (_mainWindow.IsCollapsed)
                    {
                        _mainWindow.ApplyWindowPosition(true); // Move to active monitor & slide out
                        _mainWindow.Activate();
                    }
                    else
                    {
                        _mainWindow.Hide();
                    }
                }
            }
        }

        public void ShowSettingsWindow()
        {
            var settingsWin = new SettingsWindow();
            settingsWin.Owner = _mainWindow;
            settingsWin.ShowDialog();
        }

        private void ExitApplication()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.OnExit(e);
        }
    }
}
