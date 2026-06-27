using System;
using System.Linq;
using System.Windows;

namespace SidebarApp
{
    public static class LanguageHelper
    {
        public static void ApplyLanguage(string langCode)
        {
            var mergedDicts = System.Windows.Application.Current.Resources.MergedDictionaries;
            
            // Find and remove existing language dictionaries
            var langDicts = mergedDicts.Where(d => d.Source != null && 
                (d.Source.OriginalString.Contains("zh-CN.xaml") || d.Source.OriginalString.Contains("en-US.xaml"))).ToList();
            
            foreach (var dict in langDicts)
            {
                mergedDicts.Remove(dict);
            }

            // Load new language dictionary
            var newLang = new ResourceDictionary();
            string langPath = langCode.Equals("en-US", StringComparison.OrdinalIgnoreCase) 
                ? "Locales/en-US.xaml" 
                : "Locales/zh-CN.xaml";
            
            newLang.Source = new Uri(langPath, UriKind.Relative);
            mergedDicts.Add(newLang);
        }

        public static string GetString(string key)
        {
            if (System.Windows.Application.Current.Resources.Contains(key))
            {
                return System.Windows.Application.Current.Resources[key] as string ?? key;
            }
            return key;
        }
    }
}
