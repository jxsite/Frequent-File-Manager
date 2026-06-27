using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace SidebarApp
{
    public static class ThemeHelper
    {
        public static void ApplyTheme(string themeName)
        {
            bool isLight = false;
            if (themeName.Equals("Light", StringComparison.OrdinalIgnoreCase))
            {
                isLight = true;
            }
            else if (themeName.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                isLight = IsSystemLightTheme();
            }

            var mergedDicts = System.Windows.Application.Current.Resources.MergedDictionaries;
            
            // Find and remove existing theme dictionaries
            var themeDicts = mergedDicts.Where(d => d.Source != null && 
                (d.Source.OriginalString.Contains("DarkTheme.xaml") || d.Source.OriginalString.Contains("LightTheme.xaml"))).ToList();
            
            foreach (var dict in themeDicts)
            {
                mergedDicts.Remove(dict);
            }

            // Load new theme dictionary
            var newTheme = new ResourceDictionary();
            string themePath = isLight ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml";
            newTheme.Source = new Uri(themePath, UriKind.Relative);
            mergedDicts.Add(newTheme);
        }

        public static bool IsSystemLightTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key?.GetValue("AppsUseLightTheme");
                    if (value is int i)
                    {
                        return i == 1;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
