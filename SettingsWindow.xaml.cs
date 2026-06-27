using System;
using System.Windows;
using System.Windows.Controls;

namespace SidebarApp
{
    public partial class SettingsWindow : Window
    {
        private string _originalTheme;
        private string _originalLang;
        private string _originalPosition;
        private bool _isUpdatingDropdowns = false;

        public SettingsWindow()
        {
            InitializeComponent();

            var config = ConfigHelper.Current;
            _originalTheme = config.Theme;
            _originalLang = config.Language;
            _originalPosition = config.Position;

            // Load AutoStart
            AutoStartCheckBox.IsChecked = AutoStartHelper.IsAutoStartEnabled();

            // Load Size (Width) & Opacity
            WidthSlider.Value = config.Width;
            TxtWidthValue.Text = ((int)config.Width).ToString();

            OpacitySlider.Value = config.Opacity;
            TxtOpacityValue.Text = $"{(int)(config.Opacity * 100)}%";

            // Programmatically populate and select items in the ComboBoxes
            InitializeDropdowns();
        }

        private void InitializeDropdowns()
        {
            _isUpdatingDropdowns = true;

            var config = ConfigHelper.Current;
            int posIndex = PositionComboBox.SelectedIndex >= 0 ? PositionComboBox.SelectedIndex : (config.Position.Equals("Left", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
            int themeIndex = ThemeComboBox.SelectedIndex >= 0 ? ThemeComboBox.SelectedIndex : 
                (config.Theme.Equals("System", StringComparison.OrdinalIgnoreCase) ? 0 : 
                 config.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? 1 : 2);
            int langIndex = LanguageComboBox.SelectedIndex >= 0 ? LanguageComboBox.SelectedIndex : 
                (config.Language.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) ? 0 : 1);

            // Position ComboBox
            PositionComboBox.Items.Clear();
            PositionComboBox.Items.Add(LanguageHelper.GetString("StrSettingsPosRight"));
            PositionComboBox.Items.Add(LanguageHelper.GetString("StrSettingsPosLeft"));
            PositionComboBox.SelectedIndex = posIndex;

            // Theme ComboBox
            ThemeComboBox.Items.Clear();
            ThemeComboBox.Items.Add(LanguageHelper.GetString("StrThemeSystem"));
            ThemeComboBox.Items.Add(LanguageHelper.GetString("StrThemeDark"));
            ThemeComboBox.Items.Add(LanguageHelper.GetString("StrThemeLight"));
            ThemeComboBox.SelectedIndex = themeIndex;

            // Language ComboBox
            LanguageComboBox.Items.Clear();
            LanguageComboBox.Items.Add("简体中文");
            LanguageComboBox.Items.Add("English");
            LanguageComboBox.SelectedIndex = langIndex;

            // PinMode ComboBox
            int pinIndex = ConfigHelper.Current.PinMode ? 1 : 0;
            PinModeComboBox.Items.Clear();
            PinModeComboBox.Items.Add(LanguageHelper.GetString("StrPinModeAuto"));
            PinModeComboBox.Items.Add(LanguageHelper.GetString("StrPinModeFixed"));
            PinModeComboBox.SelectedIndex = pinIndex;

            _isUpdatingDropdowns = false;
        }

        private void PositionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingDropdowns) return;
            
            string pos = PositionComboBox.SelectedIndex == 1 ? "Left" : "Right";
            var config = ConfigHelper.Current;
            string prevPos = config.Position;
            config.Position = pos;
            
            if (Owner is MainWindow mainWin)
            {
                mainWin.ApplyWindowPosition(mainWin.IsMouseOver);
            }
            
            config.Position = prevPos; // Temporary preview, don't write permanently yet
        }

        private void PinModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingDropdowns) return;
            // Live preview: tell MainWindow to update pin mode immediately
            bool pinned = PinModeComboBox.SelectedIndex == 1;
            if (Owner is MainWindow mainWin)
            {
                mainWin.SetPinMode(pinned);
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingDropdowns) return;

            string theme = "System";
            if (ThemeComboBox.SelectedIndex == 1) theme = "Dark";
            else if (ThemeComboBox.SelectedIndex == 2) theme = "Light";

            ThemeHelper.ApplyTheme(theme);
            if (Owner is MainWindow mainWin)
            {
                mainWin.ApplyWindowPosition(mainWin.IsMouseOver);
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingDropdowns) return;

            string lang = LanguageComboBox.SelectedIndex == 1 ? "en-US" : "zh-CN";
            LanguageHelper.ApplyLanguage(lang);
            InitializeDropdowns();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var config = ConfigHelper.Current;

            // Save Position
            config.Position = PositionComboBox.SelectedIndex == 1 ? "Left" : "Right";

            // Save Size & Opacity
            config.Width = WidthSlider.Value;
            config.Opacity = OpacitySlider.Value;

            // Save AutoStart Settings
            bool enableAutoStart = AutoStartCheckBox.IsChecked == true;
            config.AutoStart = enableAutoStart;
            AutoStartHelper.SetAutoStart(enableAutoStart);

            // Save Theme
            if (ThemeComboBox.SelectedIndex == 0) config.Theme = "System";
            else if (ThemeComboBox.SelectedIndex == 1) config.Theme = "Dark";
            else if (ThemeComboBox.SelectedIndex == 2) config.Theme = "Light";

            // Save Language
            if (LanguageComboBox.SelectedIndex == 0) config.Language = "zh-CN";
            else if (LanguageComboBox.SelectedIndex == 1) config.Language = "en-US";

            // Save PinMode
            config.PinMode = PinModeComboBox.SelectedIndex == 1;

            ConfigHelper.Save();

            // Re-apply to ensure proper state
            ThemeHelper.ApplyTheme(config.Theme);
            LanguageHelper.ApplyLanguage(config.Language);

            // Notify MainWindow to apply changes
            if (Owner is MainWindow mainWin)
            {
                mainWin.ApplySettingsAndRefresh();
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            var config = ConfigHelper.Current;
            config.Position = _originalPosition;

            // Revert theme and language to original values
            ThemeHelper.ApplyTheme(_originalTheme);
            LanguageHelper.ApplyLanguage(_originalLang);

            if (Owner is MainWindow mainWin)
            {
                mainWin.ApplyWindowPosition(mainWin.IsMouseOver);
            }

            DialogResult = false;
            Close();
        }

        private void WidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtWidthValue != null)
            {
                TxtWidthValue.Text = ((int)e.NewValue).ToString();
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtOpacityValue != null)
            {
                TxtOpacityValue.Text = $"{(int)(e.NewValue * 100)}%";
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true };
                System.Diagnostics.Process.Start(psi);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open link: {ex.Message}");
            }
        }
    }
}
