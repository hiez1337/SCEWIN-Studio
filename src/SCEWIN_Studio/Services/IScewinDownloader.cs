using System;
using System.Threading;
using System.Threading.Tasks;

namespace SCEWIN_Studio.Services;

public interface IScewinDownloader
{
    Task<(bool success, string exePath, string message)> DownloadOrProvisionScewinAsync(
        string targetDirectory,
        IProgress<(int percentage, string status)> progress,
        CancellationToken ct = default);
}
