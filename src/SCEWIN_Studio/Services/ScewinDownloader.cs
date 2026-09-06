using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SCEWIN_Studio.Services;

public class ScewinDownloader : IScewinDownloader
{
    private static readonly string[] RequiredFiles = new[]
    {
        "SCEWIN_64.exe",
        "amifldrv64.sys",
        "amigendrv64.sys"
    };

    public async Task<(bool success, string exePath, string message)> DownloadOrProvisionScewinAsync(
        string targetDirectory,
        IProgress<(int percentage, string status)> progress,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                targetDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "SCEWIN");
            }

            Directory.CreateDirectory(targetDirectory);
            progress?.Report((5, "Проверка компонентов SCEWIN..."));

            var targetExe = Path.Combine(targetDirectory, "SCEWIN_64.exe");

            // 1. Check if already fully provisioned with drivers
            if (IsFullyProvisioned(targetDirectory))
            {
                progress?.Report((100, "SCEWIN и драйверы уже установлены!"));
                return (true, targetExe, "SCEWIN 5.05.01.0002 готов к использованию.");
            }

            // 2. Primary source: Extract from embedded or bundled SCEWIN.zip
            progress?.Report((20, "Распаковка проверенного пакета SCEWIN 5.05.01..."));
            bool extracted = await TryExtractBundledZipAsync(targetDirectory, progress, ct);
            if (extracted && IsFullyProvisioned(targetDirectory))
            {
                progress?.Report((100, "Установка успешно завершена!"));
                return (true, targetExe, "SCEWIN 5.05.01.0002 и драйверы ядра AMI успешно установлены.");
            }

            // 3. Secondary source: Copy from detected local directories (e.g. SCEHUB / Downloads)
            progress?.Report((40, "Поиск локальных копий SCEWIN..."));
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var localSources = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "publish", "bin", "SCEWIN"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SCEWIN"),
                Path.Combine(userProfile, "Downloads", "Скрипты и Конфиги", "SCEHUB-main", "SCEHUB-main", "SCEWIN", "5.05.01.0002"),
                @"F:\Downloads\Скрипты и Конфиги\SCEHUB-main\SCEHUB-main\SCEWIN\5.05.01.0002"
            };

            foreach (var localSrc in localSources)
            {
                if (Directory.Exists(localSrc) && IsFullyProvisioned(localSrc))
                {
                    progress?.Report((60, "Копирование компонентов SCEWIN из локального источника..."));
                    foreach (var rf in RequiredFiles)
                    {
                        ct.ThrowIfCancellationRequested();
                        File.Copy(Path.Combine(localSrc, rf), Path.Combine(targetDirectory, rf), overwrite: true);
                    }

                    if (IsFullyProvisioned(targetDirectory))
                    {
                        progress?.Report((100, "Установка успешно завершена!"));
                        return (true, targetExe, "SCEWIN 5.05.01.0002 успешно скопирован.");
                    }
                }
            }

            // 4. Online download fallback if bundled zip was not found
            progress?.Report((50, "Загрузка дистрибутива SCEWIN из онлайн-репозитория..."));
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SCEWIN_Studio/1.0");

            var downloadUrl = "https://github.com/ab3lkaizen/SCEHUB/releases/download/1.2.0/DL_SCEWIN.exe";
            var tempDlExe = Path.Combine(targetDirectory, "DL_SCEWIN.exe");

            using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var fs = new FileStream(tempDlExe, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[16384];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, bytesRead, ct);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        int p = 50 + (int)((totalRead * 40) / totalBytes);
                        progress?.Report((Math.Min(90, p), $"Загрузка: {totalRead / 1024} KB / {totalBytes / 1024} KB"));
                    }
                }
            }

            // Final check
            if (IsFullyProvisioned(targetDirectory))
            {
                progress?.Report((100, "Готово!"));
                return (true, targetExe, "SCEWIN успешно установлен.");
            }

            return (false, string.Empty, "Не удалось полностью установить SCEWIN и драйверы ядра.");
        }
        catch (Exception ex)
        {
            return (false, string.Empty, $"Ошибка загрузки/установки: {ex.Message}");
        }
    }

    private static bool IsFullyProvisioned(string directory)
    {
        if (!Directory.Exists(directory)) return false;
        foreach (var rf in RequiredFiles)
        {
            var p = Path.Combine(directory, rf);
            if (!File.Exists(p) || new FileInfo(p).Length == 0)
                return false;
        }
        return true;
    }

    private static async Task<bool> TryExtractBundledZipAsync(string targetDirectory, IProgress<(int percentage, string status)>? progress, CancellationToken ct)
    {
        Stream? zipStream = null;

        // 1. Try manifest resource
        var asm = Assembly.GetExecutingAssembly();
        var resName = $"{asm.GetName().Name}.Resources.SCEWIN.zip";
        zipStream = asm.GetManifestResourceStream(resName);

        // 2. Try file system relative to app base
        if (zipStream == null)
        {
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "SCEWIN.zip"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SCEWIN.zip"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "src", "SCEWIN_Studio", "Resources", "SCEWIN.zip"))
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand))
                {
                    zipStream = File.OpenRead(cand);
                    break;
                }
            }
        }

        if (zipStream == null) return false;

        using (zipStream)
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
        {
            int total = archive.Entries.Count;
            int idx = 0;
            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var targetDirFull = Path.GetFullPath(targetDirectory);
                if (!targetDirFull.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    targetDirFull += Path.DirectorySeparatorChar;
                }

                var dest = Path.GetFullPath(Path.Combine(targetDirectory, entry.Name));
                if (!dest.StartsWith(targetDirFull, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Security: zip entry '{entry.Name}' attempts path traversal outside target directory.");
                }

                entry.ExtractToFile(dest, overwrite: true);
                idx++;
                int p = 20 + (idx * 60 / Math.Max(1, total));
                progress?.Report((p, $"Распаковка: {entry.Name}"));
            }
        }

        await Task.Yield();
        return true;
    }
}
