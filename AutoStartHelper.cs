using System;
using Microsoft.Win32;

namespace SidebarApp
{
    public static class AutoStartHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "SidebarApp";

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                if (key != null)
                {
                    object? value = key.GetValue(AppName);
                    if (value != null)
                    {
                        string? path = value.ToString();
                        return string.Equals(path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking autostart: {ex.Message}");
            }
            return false;
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key != null)
                {
                    if (enable)
                    {
                        string? appPath = Environment.ProcessPath;
                        if (!string.IsNullOrEmpty(appPath))
                        {
                            key.SetValue(AppName, appPath);
                        }
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting autostart: {ex.Message}");
            }
        }
    }
}
