using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace SCEWIN_Studio.Services;

public class ScewinRunner : IScewinRunner
{
    private static readonly object s_processLock = new();
    private static readonly HashSet<Process> s_trackedProcesses = new();
    private static readonly IntPtr s_jobHandle = InitializeJobObject();

    public static readonly string[] TargetProcessNames = new[]
    {
        "SCEWIN_64",
        "SCEWIN64",
        "SCEWIN"
    };

    #region Win32 Job Object (Ensures child processes die with parent)
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryLimit;
        public UIntPtr PeakJobMemoryLimit;
    }

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    private static IntPtr InitializeJobObject()
    {
        try
        {
            var hJob = CreateJobObject(IntPtr.Zero, null);
            if (hJob != IntPtr.Zero)
            {
                var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                    }
                };

                int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr infoPtr = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(info, infoPtr, false);
                    SetInformationJobObject(hJob, JobObjectExtendedLimitInformation, infoPtr, (uint)length);
                }
                finally
                {
                    Marshal.FreeHGlobal(infoPtr);
                }
            }
            return hJob;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static void TryAssignToJob(Process process)
    {
        if (s_jobHandle != IntPtr.Zero)
        {
            try
            {
                AssignProcessToJobObject(s_jobHandle, process.Handle);
            }
            catch
            {
            }
        }
    }
    #endregion

    public static void KillOrphanProcesses()
    {
        // First kill any tracked active processes
        lock (s_processLock)
        {
            foreach (var p in s_trackedProcesses)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        p.Kill(entireProcessTree: true);
                        p.WaitForExit(2000);
                    }
                    p.Dispose();
                }
                catch
                {
                }
            }
            s_trackedProcesses.Clear();
        }

        // Also search OS process table for any orphan SCEWIN processes
        foreach (var procName in TargetProcessNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(procName);
                foreach (var p in processes)
                {
                    try
                    {
                        if (!p.HasExited)
                        {
                            p.Kill(entireProcessTree: true);
                            p.WaitForExit(2000);
                        }
                        p.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }

    public void KillOrphans()
    {
        KillOrphanProcesses();
    }

    public bool IsAdministrator()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool success, string output, string error)> ExportNvramAsync(string scewinPath, string outputFilePath)
    {
        return await ExecuteScewinAsync(scewinPath, $"/o /s \"{outputFilePath}\" /q");
    }

    public async Task<(bool success, string output, string error)> ImportNvramAsync(string scewinPath, string diffFilePath)
    {
        return await ExecuteScewinAsync(scewinPath, $"/i /s \"{diffFilePath}\" /ds /q");
    }

    private async Task<(bool success, string output, string error)> ExecuteScewinAsync(string scewinPath, string arguments)
    {
        if (!File.Exists(scewinPath))
        {
            return (false, string.Empty, $"SCEWIN binary not found at: {scewinPath}");
        }

        var workingDir = Path.GetDirectoryName(scewinPath) ?? AppDomain.CurrentDomain.BaseDirectory;

        // Check required driver files
        if (!File.Exists(Path.Combine(workingDir, "amifldrv64.sys")) ||
            !File.Exists(Path.Combine(workingDir, "amigendrv64.sys")))
        {
            return (false, string.Empty, "Missing required kernel drivers (amifldrv64.sys, amigendrv64.sys) in the SCEWIN folder.");
        }

        if (!IsAdministrator())
        {
            return (false, string.Empty, "Для прямого доступа к NVRAM через драйверы ядра AMI требуются права Администратора. Запустите SCEWIN Studio от имени Администратора (Run as administrator).");
        }

        // Clean up any stale/orphaned instances before executing
        KillOrphanProcesses();

        return await Task.Run(() =>
        {
            Process? process = null;
            try
            {
                Encoding consoleEncoding;
                try
                {
                    consoleEncoding = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
                }
                catch
                {
                    consoleEncoding = Encoding.UTF8;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = scewinPath,
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = consoleEncoding,
                    StandardErrorEncoding = consoleEncoding
                };

                process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                process.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                lock (s_processLock)
                {
                    s_trackedProcesses.Add(process);
                }

                process.Start();
                TryAssignToJob(process);

                // Close standard input immediately so console never blocks waiting for input
                try
                {
                    process.StandardInput.Close();
                }
                catch
                {
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool exited = process.WaitForExit(60000); // 60s timeout
                if (!exited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(3000);
                    }
                    catch
                    {
                    }
                    return (false, stdout.ToString(), "Command timed out after 60 seconds.");
                }

                // Check log-file.txt if generated by SCEWIN
                var localLog = Path.Combine(workingDir, "log-file.txt");
                if (File.Exists(localLog))
                {
                    try
                    {
                        var logContent = File.ReadAllText(localLog);
                        stdout.AppendLine("\n--- log-file.txt ---");
                        stdout.AppendLine(logContent);
                    }
                    catch
                    {
                    }
                }

                bool isSuccess = process.ExitCode == 0;
                return (isSuccess, stdout.ToString(), stderr.ToString());
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Failed to run SCEWIN: {ex.Message}");
            }
            finally
            {
                if (process != null)
                {
                    lock (s_processLock)
                    {
                        s_trackedProcesses.Remove(process);
                    }

                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                            process.WaitForExit(3000);
                        }
                    }
                    catch
                    {
                    }
                    process.Dispose();
                }
            }
        });
    }
}
