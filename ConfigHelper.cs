using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SidebarApp
{
    public class AppConfig
    {
        public List<string> ScanDirectories { get; set; } = new() 
        { 
            @"D:\aiworkfile", 
            @"D:\CAD_Projects", 
            @"D:\aiproject", 
            @"C:\000胡锐工作文件mac2025" 
        };
        public List<string> Keywords { get; set; } = new() { "大货图纸", "REV", "样板" };
        public bool AutoStart { get; set; } = true;
        public string Position { get; set; } = "Right"; // "Left" or "Right"
        public double Width { get; set; } = 280;
        public double Opacity { get; set; } = 0.8;
        public Dictionary<string, int> FolderFrequency { get; set; } = new();
        public List<string> PinnedFolders { get; set; } = new();
        public List<string> PinnedFiles { get; set; } = new();
        public string ActiveTab { get; set; } = "Folders";
        public string Theme { get; set; } = "System";
        public string Language { get; set; } = "zh-CN";
        public bool PinMode { get; set; } = false; // false = auto slide, true = always visible
    }

    public static class ConfigHelper
    {
        private static readonly string ConfigFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "SidebarApp"
        );
        private static readonly string ConfigPath = Path.Combine(ConfigFolder, "config.json");

        private static AppConfig? _current;
        public static AppConfig Current
        {
            get
            {
                if (_current == null)
                {
                    Load();
                }
                return _current!;
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    _current = JsonSerializer.Deserialize<AppConfig>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
            }

            if (_current == null)
            {
                _current = new AppConfig();
                // Check if workspace dir or parent dir exists, add it if it does
                string workspacePath = @"d:\aiworkfile\windows侧边栏";
                string parentPath = Path.GetDirectoryName(workspacePath) ?? "";
                if (Directory.Exists(parentPath) && !_current.ScanDirectories.Contains(parentPath))
                {
                    _current.ScanDirectories.Insert(0, parentPath);
                }
            }
        }

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                }
                string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
            }
        }

        public static void IncrementFrequency(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            
            if (Current.FolderFrequency.ContainsKey(folderPath))
            {
                Current.FolderFrequency[folderPath]++;
            }
            else
            {
                Current.FolderFrequency[folderPath] = 1;
            }
            Save();
        }

        public static int GetFrequency(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return 0;
            return Current.FolderFrequency.TryGetValue(folderPath, out int freq) ? freq : 0;
        }
    }
}
