using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;

namespace SidebarApp
{
    public partial class MainWindow : Window
    {
        private List<FolderItem> _allFolders = new();
        private List<FolderItem> _displayFolders = new();
        private List<FileItem> _allFiles = new();
        private List<FileItem> _displayFiles = new();
        private List<RecentItem> _allRecentItems = new();
        private List<RecentItem> _displayRecentItems = new();
        private List<CustomItem> _allCustomItems = new();
        private List<CustomItem> _displayCustomItems = new();
        
        private string _activeTab = "Folders"; // "Folders", "Files", "Recent", "Custom"
        private string _activeSort = "Time"; // "Name", "Time", "Freq"
        private bool _sortAscending = false; // Default is descending for Time/Freq
        
        private DispatcherTimer? _hideTimer;
        private bool _isCollapsed = false;
        private double _collapsedOffset = 15.0;
        private bool _isPinMode = false;         // true = always visible, no auto-collapse
        private bool _searchBoxFocused = false;  // prevent collapse while typing
        
        public bool IsCollapsed => _isCollapsed;
        public bool IsMouseOverNow => IsMouseOver;

        private Screen GetCurrentScreen()
        {
            try
            {
                if (!this.IsVisible || this.Left < -10000 || this.Left > 100000)
                {
                    return Screen.FromPoint(System.Windows.Forms.Cursor.Position) ?? Screen.PrimaryScreen ?? Screen.AllScreens[0];
                }
                
                var windowCenter = new System.Drawing.Point(
                    (int)(this.Left + this.Width / 2),
                    (int)(this.Top + this.Height / 2)
                );
                return Screen.FromPoint(windowCenter) ?? Screen.PrimaryScreen ?? Screen.AllScreens[0];
            }
            catch
            {
                return Screen.PrimaryScreen ?? Screen.AllScreens[0];
            }
        }
        
        public MainWindow()
        {
            InitializeComponent();

            _hideTimer = new DispatcherTimer();
            _hideTimer.Interval = TimeSpan.FromMilliseconds(500);
            _hideTimer.Tick += HideTimer_Tick;

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Restore active tab from configuration
            var config = ConfigHelper.Current;
            string savedTab = config.ActiveTab;
            if (string.IsNullOrEmpty(savedTab)) savedTab = "Folders";
            SelectTab(savedTab);

            // Restore pin mode
            _isPinMode = config.PinMode;
            UpdatePinModeVisual();

            ApplyWindowPosition(false);
            ApplySettingsAndRefresh();
            
            // Start collapsed after 2 seconds (only in auto mode)
            var initialCollapseTimer = new DispatcherTimer();
            initialCollapseTimer.Interval = TimeSpan.FromSeconds(2);
            initialCollapseTimer.Tick += (s, ev) =>
            {
                initialCollapseTimer.Stop();
                if (!IsMouseOver && !_isPinMode)
                {
                    CollapseSidebar();
                }
            };
            initialCollapseTimer.Start();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwndSource = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            hwndSource?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_SETTINGCHANGE = 0x001A;
            if (msg == WM_SETTINGCHANGE)
            {
                var config = ConfigHelper.Current;
                if (config.Theme.Equals("System", StringComparison.OrdinalIgnoreCase))
                {
                    ThemeHelper.ApplyTheme("System");
                    ApplyWindowPosition(IsMouseOver);
                }
            }
            return IntPtr.Zero;
        }

        public void ApplyWindowPosition(bool isMouseOverNow)
        {
            var config = ConfigHelper.Current;
            var screen = GetCurrentScreen();
            if (screen == null) return;

            var workArea = screen.WorkingArea;
            
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            double scaleX = dpi.DpiScaleX;
            double scaleY = dpi.DpiScaleY;

            double workAreaLeft = workArea.Left / scaleX;
            double workAreaTop = workArea.Top / scaleY;
            double workAreaRight = workArea.Right / scaleX;
            double workAreaHeight = workArea.Height / scaleY;

            this.BeginAnimation(Window.LeftProperty, null);

            this.Height = workAreaHeight;
            this.Top = workAreaTop;
            this.Width = config.Width;

            // Resolve background color dynamically from the theme resource
            System.Windows.Media.Color baseColor = System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x2E);
            if (Application.Current.Resources["BgDarkBrush"] is System.Windows.Media.SolidColorBrush brush)
            {
                baseColor = brush.Color;
            }

            byte alpha = (byte)(config.Opacity * 255);
            MainBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));

            if (config.Position.Equals("Left", StringComparison.OrdinalIgnoreCase))
            {
                MainBorder.CornerRadius = new CornerRadius(0, 12, 12, 0);
                MainBorder.Margin = new Thickness(0, 0, 12, 0);
            }
            else
            {
                MainBorder.CornerRadius = new CornerRadius(12, 0, 0, 12);
                MainBorder.Margin = new Thickness(12, 0, 0, 0);
            }

            double expandedLeft;
            double collapsedLeft;

            if (config.Position.Equals("Left", StringComparison.OrdinalIgnoreCase))
            {
                expandedLeft = workAreaLeft;
                collapsedLeft = workAreaLeft - this.Width + _collapsedOffset;
            }
            else
            {
                expandedLeft = workAreaRight - this.Width;
                collapsedLeft = workAreaRight - _collapsedOffset;
            }

            if (isMouseOverNow)
            {
                this.Left = expandedLeft;
                _isCollapsed = false;
            }
            else
            {
                this.Left = collapsedLeft;
                _isCollapsed = true;
            }
        }

        public async void ApplySettingsAndRefresh()
        {
            ApplyWindowPosition(IsMouseOver);
            await LoadFoldersAsync();
        }

        private async Task LoadFoldersAsync()
        {
            TxtStatus.Text = LanguageHelper.GetString("StrStatusScanning");
            try
            {
                _allFolders = await FolderScanner.ScanAsync();
                _allFiles = await FolderScanner.ScanFilesAsync(_allFolders);
                _allRecentItems = await FolderScanner.ScanRecentItemsAsync();

                var config = ConfigHelper.Current;
                var customList = new List<CustomItem>();

                foreach (var folderPath in config.PinnedFolders)
                {
                    string name = string.Empty;
                    DateTime modified = DateTime.MinValue;
                    try
                    {
                        name = Path.GetFileName(folderPath);
                        if (string.IsNullOrEmpty(name)) name = folderPath;
                        if (Directory.Exists(folderPath))
                            modified = Directory.GetLastWriteTime(folderPath);
                    }
                    catch { name = folderPath; }

                    customList.Add(new CustomItem
                    {
                        Name = name,
                        FullPath = folderPath,
                        LastModified = modified,
                        Frequency = ConfigHelper.GetFrequency(folderPath),
                        IsDirectory = true
                    });
                }

                foreach (var filePath in config.PinnedFiles)
                {
                    string name = string.Empty;
                    DateTime modified = DateTime.MinValue;
                    try
                    {
                        name = Path.GetFileName(filePath);
                        if (string.IsNullOrEmpty(name)) name = filePath;
                        if (File.Exists(filePath))
                            modified = File.GetLastWriteTime(filePath);
                    }
                    catch { name = filePath; }

                    customList.Add(new CustomItem
                    {
                        Name = name,
                        FullPath = filePath,
                        LastModified = modified,
                        Frequency = ConfigHelper.GetFrequency(filePath),
                        IsDirectory = false
                    });
                }

                _allCustomItems = customList;
                FilterAndSortList();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = LanguageHelper.GetString("StrStatusFailed");
                Debug.WriteLine($"Failed to load items: {ex.Message}");
            }
        }

        private void FilterAndSortList()
        {
            string search = SearchBox.Text.Trim();
            
            if (_activeTab == "Folders")
            {
                var filteredFolders = string.IsNullOrEmpty(search) 
                    ? _allFolders 
                    : _allFolders.Where(f => f.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || 
                                             f.FullPath.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                IOrderedEnumerable<FolderItem>? sortedFolders;
                if (_activeSort == "Name")
                {
                    sortedFolders = _sortAscending 
                        ? filteredFolders.OrderBy(f => f.Name) 
                        : filteredFolders.OrderByDescending(f => f.Name);
                }
                else if (_activeSort == "Freq")
                {
                    sortedFolders = _sortAscending 
                        ? filteredFolders.OrderBy(f => f.Frequency) 
                        : filteredFolders.OrderByDescending(f => f.Frequency);
                }
                else
                {
                    sortedFolders = _sortAscending 
                        ? filteredFolders.OrderBy(f => f.LastModified) 
                        : filteredFolders.OrderByDescending(f => f.LastModified);
                }

                _displayFolders = sortedFolders.ToList();
                FoldersItemsControl.ItemsSource = _displayFolders;
                TxtStatus.Text = string.Format(LanguageHelper.GetString("StrStatusFolders"), _displayFolders.Count);
            }
            else if (_activeTab == "Files")
            {
                var filteredFiles = string.IsNullOrEmpty(search) 
                    ? _allFiles 
                    : _allFiles.Where(f => f.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || 
                                           f.FullPath.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                IOrderedEnumerable<FileItem>? sortedFiles;
                if (_activeSort == "Name")
                {
                    sortedFiles = _sortAscending 
                        ? filteredFiles.OrderBy(f => f.Name) 
                        : filteredFiles.OrderByDescending(f => f.Name);
                }
                else if (_activeSort == "Freq")
                {
                    sortedFiles = _sortAscending 
                        ? filteredFiles.OrderBy(f => f.Frequency) 
                        : filteredFiles.OrderByDescending(f => f.Frequency);
                }
                else
                {
                    sortedFiles = _sortAscending 
                        ? filteredFiles.OrderBy(f => f.LastModified) 
                        : filteredFiles.OrderByDescending(f => f.LastModified);
                }

                _displayFiles = sortedFiles.ToList();
                FilesItemsControl.ItemsSource = _displayFiles;
                TxtStatus.Text = string.Format(LanguageHelper.GetString("StrStatusFiles"), _displayFiles.Count);
            }
            else if (_activeTab == "Recent")
            {
                var filteredRecent = string.IsNullOrEmpty(search) 
                    ? _allRecentItems 
                    : _allRecentItems.Where(r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || 
                                                 r.FullPath.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                IOrderedEnumerable<RecentItem>? sortedRecent;
                if (_activeSort == "Name")
                {
                    sortedRecent = _sortAscending 
                        ? filteredRecent.OrderBy(r => r.Name) 
                        : filteredRecent.OrderByDescending(r => r.Name);
                }
                else
                {
                    sortedRecent = _sortAscending 
                        ? filteredRecent.OrderBy(r => r.LastAccessed) 
                        : filteredRecent.OrderByDescending(r => r.LastAccessed);
                }

                _displayRecentItems = sortedRecent.ToList();
                RecentItemsControl.ItemsSource = _displayRecentItems;
                TxtStatus.Text = string.Format(LanguageHelper.GetString("StrStatusRecent"), _displayRecentItems.Count);
            }
            else if (_activeTab == "Custom")
            {
                var filteredCustom = string.IsNullOrEmpty(search) 
                    ? _allCustomItems 
                    : _allCustomItems.Where(c => c.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || 
                                                 c.FullPath.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                IOrderedEnumerable<CustomItem>? sortedCustom;
                if (_activeSort == "Name")
                {
                    sortedCustom = _sortAscending 
                        ? filteredCustom.OrderBy(c => c.Name) 
                        : filteredCustom.OrderByDescending(c => c.Name);
                }
                else if (_activeSort == "Freq")
                {
                    sortedCustom = _sortAscending 
                        ? filteredCustom.OrderBy(c => c.Frequency) 
                        : filteredCustom.OrderByDescending(c => c.Frequency);
                }
                else
                {
                    sortedCustom = _sortAscending 
                        ? filteredCustom.OrderBy(c => c.LastModified) 
                        : filteredCustom.OrderByDescending(c => c.LastModified);
                }

                _displayCustomItems = sortedCustom.ToList();
                CustomItemsControl.ItemsSource = _displayCustomItems;
                TxtStatus.Text = string.Format(LanguageHelper.GetString("StrStatusCustom"), _displayCustomItems.Count);
            }
        }

        public void TriggerSlideIn()
        {
            ExpandSidebar();
        }

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _hideTimer?.Stop();
            if (_isCollapsed)
            {
                ExpandSidebar();
            }
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Don't collapse if pin mode is on, or if search box is focused
            if (_isPinMode || _searchBoxFocused) return;
            _hideTimer?.Start();
        }

        private void HideTimer_Tick(object? sender, EventArgs e)
        {
            _hideTimer?.Stop();
            if (!_isCollapsed && !IsMouseOver && !_isPinMode && !_searchBoxFocused)
            {
                CollapseSidebar();
            }
        }

        private void ExpandSidebar()
        {
            if (!_isCollapsed) return;

            var screen = GetCurrentScreen();
            if (screen == null) return;

            var config = ConfigHelper.Current;
            var workArea = screen.WorkingArea;

            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            double scaleX = dpi.DpiScaleX;
            double workAreaLeft = workArea.Left / scaleX;
            double workAreaRight = workArea.Right / scaleX;

            double targetLeft = config.Position.Equals("Left", StringComparison.OrdinalIgnoreCase) 
                ? workAreaLeft 
                : workAreaRight - this.Width;

            AnimateLeftProperty(targetLeft);
            _isCollapsed = false;
        }

        private void CollapseSidebar()
        {
            if (_isCollapsed) return;

            var screen = GetCurrentScreen();
            if (screen == null) return;

            var config = ConfigHelper.Current;
            var workArea = screen.WorkingArea;

            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            double scaleX = dpi.DpiScaleX;
            double workAreaLeft = workArea.Left / scaleX;
            double workAreaRight = workArea.Right / scaleX;

            double targetLeft = config.Position.Equals("Left", StringComparison.OrdinalIgnoreCase) 
                ? workAreaLeft - this.Width + _collapsedOffset 
                : workAreaRight - _collapsedOffset;

            AnimateLeftProperty(targetLeft);
            _isCollapsed = true;
        }

        private void AnimateLeftProperty(double targetLeft)
        {
            var anim = new DoubleAnimation
            {
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(Window.LeftProperty, anim);
        }

        private void FolderCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is FolderItem folder)
            {
                try
                {
                    if (Directory.Exists(folder.FullPath))
                    {
                        Process.Start("explorer.exe", folder.FullPath);
                        
                        ConfigHelper.IncrementFrequency(folder.FullPath);
                        folder.Frequency = ConfigHelper.GetFrequency(folder.FullPath);
                        
                        FilterAndSortList();
                        CollapseSidebar();
                    }
                    else
                    {
                        MessageBox.Show(LanguageHelper.GetString("MsgFolderNotExists"), LanguageHelper.GetString("MsgBoxError"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(LanguageHelper.GetString("MsgOpenError"), ex.Message), LanguageHelper.GetString("MsgBoxError"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void FileCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is FileItem file)
            {
                try
                {
                    if (File.Exists(file.FullPath))
                    {
                        var psi = new ProcessStartInfo(file.FullPath) { UseShellExecute = true };
                        Process.Start(psi);
                        
                        ConfigHelper.IncrementFrequency(file.FullPath);
                        file.Frequency = ConfigHelper.GetFrequency(file.FullPath);
                        
                        FilterAndSortList();
                        CollapseSidebar();
                    }
                    else
                    {
                        MessageBox.Show(LanguageHelper.GetString("MsgFileNotExists"), LanguageHelper.GetString("MsgBoxError"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(LanguageHelper.GetString("MsgOpenError"), ex.Message), LanguageHelper.GetString("MsgBoxError"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                var config = ConfigHelper.Current;
                if (btn.DataContext is FileItem file)
                {
                    if (config.PinnedFiles.Contains(path))
                        config.PinnedFiles.Remove(path);
                    else
                        config.PinnedFiles.Add(path);
                    file.IsPinned = config.PinnedFiles.Contains(path);
                }
                else if (btn.DataContext is RecentItem recent)
                {
                    if (recent.IsDirectory)
                    {
                        if (config.PinnedFolders.Contains(path))
                            config.PinnedFolders.Remove(path);
                        else
                            config.PinnedFolders.Add(path);
                        recent.IsPinned = config.PinnedFolders.Contains(path);
                    }
                    else
                    {
                        if (config.PinnedFiles.Contains(path))
                            config.PinnedFiles.Remove(path);
                        else
                            config.PinnedFiles.Add(path);
                        recent.IsPinned = config.PinnedFiles.Contains(path);
                    }
                }
                else if (btn.DataContext is CustomItem custom)
                {
                    if (custom.IsDirectory)
                        config.PinnedFolders.Remove(path);
                    else
                        config.PinnedFiles.Remove(path);
                }
                else if (btn.DataContext is FolderItem folder)
                {
                    if (config.PinnedFolders.Contains(path))
                        config.PinnedFolders.Remove(path);
                    else
                        config.PinnedFolders.Add(path);
                    folder.IsPinned = config.PinnedFolders.Contains(path);
                }
                ConfigHelper.Save();
                
                RefreshCustomItemsList();
                
                foreach (var f in _allFolders) f.IsPinned = config.PinnedFolders.Contains(f.FullPath);
                foreach (var f in _allFiles) f.IsPinned = config.PinnedFiles.Contains(f.FullPath);
                foreach (var r in _allRecentItems)
                {
                    r.IsPinned = r.IsDirectory 
                        ? config.PinnedFolders.Contains(r.FullPath) 
                        : config.PinnedFiles.Contains(r.FullPath);
                }
                
                FilterAndSortList();
            }
            e.Handled = true;
        }

        private void BtnSort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleBtn)
            {
                if (toggleBtn == BtnSortName)
                {
                    BtnSortTime.IsChecked = false;
                    BtnSortFreq.IsChecked = false;
                    _activeSort = "Name";
                }
                else if (toggleBtn == BtnSortTime)
                {
                    BtnSortName.IsChecked = false;
                    BtnSortFreq.IsChecked = false;
                    _activeSort = "Time";
                }
                else if (toggleBtn == BtnSortFreq)
                {
                    BtnSortName.IsChecked = false;
                    BtnSortTime.IsChecked = false;
                    _activeSort = "Freq";
                }

                _sortAscending = !_sortAscending;
                FilterAndSortList();
            }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _searchBoxFocused = true;
            _hideTimer?.Stop(); // Never collapse while typing
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _searchBoxFocused = false;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterAndSortList();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadFoldersAsync();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                app.ShowSettingsWindow();
            }
        }

        private void PinMode_Click(object sender, RoutedEventArgs e)
        {
            _isPinMode = !_isPinMode;
            ConfigHelper.Current.PinMode = _isPinMode;
            ConfigHelper.Save();
            UpdatePinModeVisual();

            if (_isPinMode)
            {
                // Expand immediately when pinning
                _hideTimer?.Stop();
                if (_isCollapsed) ExpandSidebar();
            }
        }

        private void UpdatePinModeVisual()
        {
            if (PinModeIcon != null)
            {
                // Highlight pin icon with accent color when pinned
                PinModeIcon.Fill = _isPinMode
                    ? (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"]
                    : (System.Windows.Media.Brush)Application.Current.Resources["TextSecondaryBrush"];
            }
        }

        /// <summary>Called by SettingsWindow for live preview of pin mode change.</summary>
        public void SetPinMode(bool pinned)
        {
            _isPinMode = pinned;
            ConfigHelper.Current.PinMode = pinned;
            UpdatePinModeVisual();
            if (pinned)
            {
                _hideTimer?.Stop();
                if (_isCollapsed) ExpandSidebar();
            }
        }

        private void SelectTab(string tabName)
        {
            _activeTab = tabName;
            
            // Sync check state of toggle buttons
            BtnTabFolders.IsChecked = (tabName == "Folders");
            BtnTabFiles.IsChecked = (tabName == "Files");
            BtnTabRecent.IsChecked = (tabName == "Recent");
            BtnTabCustom.IsChecked = (tabName == "Custom");
            
            // Sync Visibility of content areas
            FoldersScrollViewer.Visibility = (tabName == "Folders") ? Visibility.Visible : Visibility.Collapsed;
            FilesScrollViewer.Visibility = (tabName == "Files") ? Visibility.Visible : Visibility.Collapsed;
            RecentScrollViewer.Visibility = (tabName == "Recent") ? Visibility.Visible : Visibility.Collapsed;
            CustomArea.Visibility = (tabName == "Custom") ? Visibility.Visible : Visibility.Collapsed;
            
            FilterAndSortList();
        }

        private void BtnTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleBtn)
            {
                string tabName = "Folders";
                if (toggleBtn == BtnTabFolders) tabName = "Folders";
                else if (toggleBtn == BtnTabFiles) tabName = "Files";
                else if (toggleBtn == BtnTabRecent) tabName = "Recent";
                else if (toggleBtn == BtnTabCustom) tabName = "Custom";
                
                SelectTab(tabName);
                
                var config = ConfigHelper.Current;
                if (config.ActiveTab != tabName)
                {
                    config.ActiveTab = tabName;
                    ConfigHelper.Save();
                }
            }
        }

        private void RecentCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is RecentItem recent)
            {
                try
                {
                    if (recent.IsDirectory)
                    {
                        if (Directory.Exists(recent.FullPath))
                        {
                            Process.Start("explorer.exe", recent.FullPath);
                            ConfigHelper.IncrementFrequency(recent.FullPath);
                            FilterAndSortList();
                            CollapseSidebar();
                        }
                        else
                        {
                            MessageBox.Show(LanguageHelper.GetString("MsgFolderNotExists"), LanguageHelper.GetString("MsgBoxError"), MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        if (File.Exists(recent.FullPath))
                        {
                            var psi = new ProcessStartInfo(recent.FullPath) { UseShellExecute = true };
                            Process.Start(psi);
                            ConfigHelper.IncrementFrequency(recent.FullPath);
                            FilterAndSortList();
                            CollapseSidebar();
                        }
                        else
                        {
                            MessageBox.Show(LanguageHelper.GetString("MsgFileNotExists"), LanguageHelper.GetString("MsgBoxError"), MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(LanguageHelper.GetString("MsgOpenError"), ex.Message), LanguageHelper.GetString("MsgBoxError"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CustomCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is CustomItem custom)
            {
                try
                {
                    if (custom.IsDirectory)
                    {
                        if (Directory.Exists(custom.FullPath))
                        {
                            Process.Start("explorer.exe", custom.FullPath);
                            ConfigHelper.IncrementFrequency(custom.FullPath);
                            FilterAndSortList();
                            CollapseSidebar();
                        }
                        else
                        {
                            MessageBox.Show(LanguageHelper.GetString("MsgFolderNotExists"), LanguageHelper.GetString("MsgBoxError"), MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        if (File.Exists(custom.FullPath))
                        {
                            var psi = new ProcessStartInfo(custom.FullPath) { UseShellExecute = true };
                            Process.Start(psi);
                            ConfigHelper.IncrementFrequency(custom.FullPath);
                            FilterAndSortList();
                            CollapseSidebar();
                        }
                        else
                        {
                            MessageBox.Show(LanguageHelper.GetString("MsgFileNotExists"), LanguageHelper.GetString("MsgBoxError"), MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(LanguageHelper.GetString("MsgOpenError"), ex.Message), LanguageHelper.GetString("MsgBoxError"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddCustomFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = LanguageHelper.GetString("StrSelectFolderTitle")
            };
            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FolderName;
                if (!string.IsNullOrEmpty(path))
                {
                    var config = ConfigHelper.Current;
                    if (!config.PinnedFolders.Contains(path))
                    {
                        config.PinnedFolders.Add(path);
                        ConfigHelper.Save();
                        
                        RefreshCustomItemsList();
                        FilterAndSortList();
                        
                        _ = LoadFoldersAsync();
                    }
                }
            }
        }

        private void AddCustomFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = LanguageHelper.GetString("StrSelectFileTitle"),
                Filter = string.Format("{0} (*.*)|*.*", LanguageHelper.GetString("StrAllFilesFilter"))
            };
            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FileName;
                if (!string.IsNullOrEmpty(path))
                {
                    var config = ConfigHelper.Current;
                    if (!config.PinnedFiles.Contains(path))
                    {
                        config.PinnedFiles.Add(path);
                        ConfigHelper.Save();
                        
                        RefreshCustomItemsList();
                        FilterAndSortList();
                        
                        _ = LoadFoldersAsync();
                    }
                }
            }
        }

        private void RefreshCustomItemsList()
        {
            var config = ConfigHelper.Current;
            var customList = new List<CustomItem>();

            foreach (var folderPath in config.PinnedFolders)
            {
                string name = string.Empty;
                DateTime modified = DateTime.MinValue;
                try
                {
                    name = Path.GetFileName(folderPath);
                    if (string.IsNullOrEmpty(name)) name = folderPath;
                    if (Directory.Exists(folderPath))
                        modified = Directory.GetLastWriteTime(folderPath);
                }
                catch { name = folderPath; }

                customList.Add(new CustomItem
                {
                    Name = name,
                    FullPath = folderPath,
                    LastModified = modified,
                    Frequency = ConfigHelper.GetFrequency(folderPath),
                    IsDirectory = true
                });
            }

            foreach (var filePath in config.PinnedFiles)
            {
                string name = string.Empty;
                DateTime modified = DateTime.MinValue;
                try
                {
                    name = Path.GetFileName(filePath);
                    if (string.IsNullOrEmpty(name)) name = filePath;
                    if (File.Exists(filePath))
                        modified = File.GetLastWriteTime(filePath);
                }
                catch { name = filePath; }

                customList.Add(new CustomItem
                {
                    Name = name,
                    FullPath = filePath,
                    LastModified = modified,
                    Frequency = ConfigHelper.GetFrequency(filePath),
                    IsDirectory = false
                });
            }

            _allCustomItems = customList;
        }
    }
}