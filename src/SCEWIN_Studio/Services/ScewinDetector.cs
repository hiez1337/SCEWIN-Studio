using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SCEWIN_Studio.Models;

namespace SCEWIN_Studio.Services;

public class ScewinDetector : IScewinDetector
{
    public async Task<DetectionResult> AutoDetectAsync()
    {
        return await Task.Run(() =>
        {
            var candidates = GetSearchPaths();
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    var result = ValidatePath(candidate);
                    if (result.Found && result.DriversReady)
                    {
                        return result; // Best match: exe + both drivers
                    }
                }
            }

            // Secondary pass: found exe even if drivers missing in directory
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    var result = ValidatePath(candidate);
                    if (result.Found)
                    {
                        return result;
                    }
                }
            }

            return new DetectionResult
            {
                Found = false,
                Details = "SCEWIN_64.exe not found in standard paths"
            };
        });
    }

    public DetectionResult ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new DetectionResult { Found = false, Details = "File does not exist" };
        }

        var dir = Path.GetDirectoryName(path) ?? "";
        bool hasFldrv = File.Exists(Path.Combine(dir, "amifldrv64.sys"));
        bool hasGendrv = File.Exists(Path.Combine(dir, "amigendrv64.sys"));

        string version = "Unknown";
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            if (!string.IsNullOrEmpty(info.FileVersion)) version = info.FileVersion;
            else if (!string.IsNullOrEmpty(info.ProductVersion)) version = info.ProductVersion;
        }
        catch { }

        return new DetectionResult
        {
            Found = true,
            ExePath = Path.GetFullPath(path),
            Version = version,
            HasAmiFldrv = hasFldrv,
            HasAmiGendrv = hasGendrv,
            Details = (hasFldrv && hasGendrv) ? "SCEWIN and required drivers ready" : "SCEWIN found, but drivers (amifldrv64.sys / amigendrv64.sys) missing in folder"
        };
    }

    private static IEnumerable<string> GetSearchPaths()
    {
        var list = new List<string>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // 1. App local folder (provisioned or bundled)
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        list.Add(Path.Combine(baseDir, "bin", "SCEWIN", "SCEWIN_64.exe"));
        list.Add(Path.Combine(baseDir, "SCEWIN", "SCEWIN_64.exe"));
        list.Add(Path.Combine(baseDir, "SCEWIN_64.exe"));
        try
        {
            var devPublish = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "publish", "bin", "SCEWIN", "SCEWIN_64.exe"));
            list.Add(devPublish);
        }
        catch { }

        // 2. User Downloads and Desktop/Documents
        if (!string.IsNullOrEmpty(userProfile))
        {
            var knownScehub = Path.Combine(userProfile, "Downloads", "Скрипты и Конфиги", "SCEHUB-main", "SCEHUB-main", "SCEWIN", "5.05.01.0002", "SCEWIN_64.exe");
            list.Add(knownScehub);

            ScanFolderForScewin(Path.Combine(userProfile, "Downloads"), list, maxDepth: 4);
            ScanFolderForScewin(Path.Combine(userProfile, "Desktop"), list, maxDepth: 2);
            ScanFolderForScewin(Path.Combine(userProfile, "Documents"), list, maxDepth: 2);
        }

        // 3. Dynamic drive discovery for common tools and redirected folders
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var driveRoot = drive.RootDirectory.FullName;
                list.Add(Path.Combine(driveRoot, "SCEWIN", "SCEWIN_64.exe"));
                list.Add(Path.Combine(driveRoot, "Tools", "SCEWIN", "SCEWIN_64.exe"));
                
                var scehubPath = Path.Combine(driveRoot, "Downloads", "Скрипты и Конфиги", "SCEHUB-main", "SCEHUB-main", "SCEWIN", "5.05.01.0002", "SCEWIN_64.exe");
                if (File.Exists(scehubPath))
                {
                    list.Add(scehubPath);
                }

                var driveDownloads = Path.Combine(driveRoot, "Downloads");
                if (Directory.Exists(driveDownloads) && !string.Equals(driveDownloads, Path.Combine(userProfile, "Downloads"), StringComparison.OrdinalIgnoreCase))
                {
                    ScanFolderForScewin(driveDownloads, list, maxDepth: 3);
                }
            }
        }
        catch { }

        // 6. PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var p in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (Directory.Exists(p))
                {
                    list.Add(Path.Combine(p, "SCEWIN_64.exe"));
                    list.Add(Path.Combine(p, "SCEWIN.exe"));
                }
            }
            catch { }
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static void ScanFolderForScewin(string root, List<string> results, int maxDepth, int currentDepth = 0)
    {
        if (currentDepth > maxDepth || !Directory.Exists(root)) return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "SCEWIN*.exe", SearchOption.TopDirectoryOnly))
            {
                results.Add(file);
            }

            foreach (var sub in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(sub);
                // Filter out non-relevant large directories
                if (name.StartsWith(".") || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) || name.Equals("AppData", StringComparison.OrdinalIgnoreCase))
                    continue;

                ScanFolderForScewin(sub, results, maxDepth, currentDepth + 1);
            }
        }
        catch { }
    }
}
