using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SidebarApp
{
    public class FolderItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public int Frequency { get; set; }
        public bool IsPinned { get; set; }
        
        public string LastModifiedString => LastModified.ToString("yyyy-MM-dd HH:mm");
        public string ShortPath
        {
            get
            {
                if (FullPath.Length > 40)
                {
                    string drive = Path.GetPathRoot(FullPath) ?? "";
                    string dirName = Path.GetFileName(FullPath);
                    return $"{drive}...\\{dirName}";
                }
                return FullPath;
            }
        }
    }

    public class FileItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public int Frequency { get; set; }
        public bool IsPinned { get; set; }
        
        public string LastModifiedString => LastModified.ToString("yyyy-MM-dd HH:mm");
        public string ShortPath
        {
            get
            {
                try
                {
                    string dir = Path.GetDirectoryName(FullPath) ?? "";
                    string dirName = Path.GetFileName(dir);
                    string drive = Path.GetPathRoot(FullPath) ?? "";
                    return $"{drive}...\\{dirName}";
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }

    public class RecentItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime LastAccessed { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsPinned { get; set; }
        public string LastAccessedString => LastAccessed.ToString("yyyy-MM-dd HH:mm");
        public string ShortPath
        {
            get
            {
                try
                {
                    string dir = IsDirectory ? FullPath : (Path.GetDirectoryName(FullPath) ?? "");
                    string dirName = Path.GetFileName(dir);
                    string drive = Path.GetPathRoot(FullPath) ?? "";
                    return $"{drive}...\\{dirName}";
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }

    public class CustomItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public int Frequency { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsPinned => true; // Always pinned since it's custom
        
        public string LastModifiedString => LastModified.ToString("yyyy-MM-dd HH:mm");
        public string ShortPath
        {
            get
            {
                try
                {
                    string dir = IsDirectory ? FullPath : (Path.GetDirectoryName(FullPath) ?? "");
                    string dirName = Path.GetFileName(dir);
                    string drive = Path.GetPathRoot(FullPath) ?? "";
                    return $"{drive}...\\{dirName}";
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }

    public static class FolderScanner
    {
        public static async Task<List<FolderItem>> ScanAsync()
        {
            var config = ConfigHelper.Current;
            var result = new List<FolderItem>();
            var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Process manually pinned folders first
            foreach (var pinned in config.PinnedFolders)
            {
                if (scannedPaths.Add(pinned))
                {
                    result.Add(new FolderItem
                    {
                        Name = Path.GetFileName(pinned) is string n && !string.IsNullOrEmpty(n) ? n : pinned,
                        FullPath = pinned,
                        LastModified = Directory.Exists(pinned) ? Directory.GetLastWriteTime(pinned) : DateTime.MinValue,
                        Frequency = ConfigHelper.GetFrequency(pinned),
                        IsPinned = true
                    });
                }
            }

            // 2. Fetch Recent Folders
            var recentItems = await ScanRecentItemsAsync();
            foreach (var item in recentItems.Where(r => r.IsDirectory))
            {
                if (scannedPaths.Add(item.FullPath))
                {
                    result.Add(new FolderItem
                    {
                        Name = item.Name,
                        FullPath = item.FullPath,
                        LastModified = item.LastAccessed,
                        Frequency = ConfigHelper.GetFrequency(item.FullPath),
                        IsPinned = false
                    });
                }
            }

            return result;
        }

        public static async Task<List<FileItem>> ScanFilesAsync(List<FolderItem> folderItems)
        {
            var config = ConfigHelper.Current;
            var result = new List<FileItem>();
            var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Add pinned files
            foreach (var pinned in config.PinnedFiles)
            {
                if (scannedPaths.Add(pinned))
                {
                    result.Add(new FileItem
                    {
                        Name = Path.GetFileName(pinned) is string n && !string.IsNullOrEmpty(n) ? n : pinned,
                        FullPath = pinned,
                        LastModified = File.Exists(pinned) ? File.GetLastWriteTime(pinned) : DateTime.MinValue,
                        Frequency = ConfigHelper.GetFrequency(pinned),
                        IsPinned = true
                    });
                }
            }

            // 2. Fetch Recent Files
            var recentItems = await ScanRecentItemsAsync();
            foreach (var item in recentItems.Where(r => !r.IsDirectory))
            {
                if (scannedPaths.Add(item.FullPath))
                {
                    result.Add(new FileItem
                    {
                        Name = item.Name,
                        FullPath = item.FullPath,
                        LastModified = item.LastAccessed,
                        Frequency = ConfigHelper.GetFrequency(item.FullPath),
                        IsPinned = false
                    });
                }
            }

            return result;
        }

        private static bool IsDocumentFile(string path)
        {
            try
            {
                string ext = Path.GetExtension(path).ToLower();
                return ext == ".dwg" || ext == ".dxf" || ext == ".xls" || ext == ".xlsx" || ext == ".doc" || ext == ".docx" || ext == ".pdf";
            }
            catch
            {
                return false;
            }
        }

        public static async Task<List<RecentItem>> ScanRecentItemsAsync()
        {
            return await Task.Run(() =>
            {
                var result = new List<RecentItem>();
                try
                {
                    string recentFolder = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                    if (!Directory.Exists(recentFolder)) return result;

                    var directoryInfo = new DirectoryInfo(recentFolder);
                    var lnkFiles = directoryInfo.GetFiles("*.lnk")
                        .OrderByDescending(f => f.LastWriteTime)
                        .Take(50); // limit to top 50 recent items

                    Type? shellType = Type.GetTypeFromProgID("Wscript.Shell");
                    if (shellType == null) return result;
                    dynamic shell = Activator.CreateInstance(shellType)!;

                    var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var file in lnkFiles)
                    {
                        try
                        {
                            dynamic shortcut = shell.CreateShortcut(file.FullName);
                            string target = shortcut.TargetPath;
                            if (string.IsNullOrEmpty(target) || scannedPaths.Contains(target)) continue;

                            bool isDir = Directory.Exists(target);
                            bool isFile = File.Exists(target);

                            if (isDir || isFile)
                            {
                                if (isFile && !IsDocumentFile(target)) continue;

                                result.Add(new RecentItem
                                {
                                    Name = Path.GetFileName(target),
                                    FullPath = target,
                                    LastAccessed = file.LastWriteTime,
                                    IsDirectory = isDir,
                                    IsPinned = isDir ? ConfigHelper.Current.PinnedFolders.Contains(target) : ConfigHelper.Current.PinnedFiles.Contains(target)
                                });
                                scannedPaths.Add(target);
                            }
                        }
                        catch
                        {
                            // ignore bad shortcuts
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error scanning recent items: {ex.Message}");
                }
                return result;
            });
        }
    }
}
