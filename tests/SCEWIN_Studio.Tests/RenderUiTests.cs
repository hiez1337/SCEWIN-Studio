using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ModernWpf;
using SCEWIN_Studio.ViewModels;
using Xunit;

namespace SCEWIN_Studio.Tests;

[Collection("WpfUi")]
public class RenderUiTests
{
    private static readonly object _lock = new();
    private static Thread? _staThread;
    private static System.Windows.Threading.Dispatcher? _dispatcher;

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SCEWIN_Studio.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private static void RunOnWpfThread(Action action)
    {
        lock (_lock)
        {
            if (_dispatcher == null)
            {
                var readyEvent = new ManualResetEventSlim(false);
                _staThread = new Thread(() =>
                {
                    if (Application.Current == null)
                    {
                        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                        app.InitializeComponent();
                    }
                    ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                    _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                    readyEvent.Set();
                    System.Windows.Threading.Dispatcher.Run();
                });
                _staThread.SetApartmentState(ApartmentState.STA);
                _staThread.IsBackground = true;
                _staThread.Start();
                readyEvent.Wait();
            }
        }

        Exception? thrown = null;
        _dispatcher!.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
        });

        if (thrown != null)
        {
            throw new Exception("Render thread failed: " + thrown.Message, thrown);
        }
    }

    [Fact]
    public void RenderAllPagesToImages()
    {
        RunOnWpfThread(() =>
        {
            var window = new MainWindow();
            var solutionRoot = FindSolutionRoot();
            var dumpPath = Path.Combine(solutionRoot, "tests", "SCEWIN_Studio.Tests", "TestData", "nvramBEFORE.txt");
            if (!File.Exists(dumpPath))
            {
                dumpPath = Path.Combine(solutionRoot, "src", "SCEWIN_Studio", "nvramBEFORE.txt");
            }

            var vm = (MainViewModel)window.DataContext;
            if (File.Exists(dumpPath))
            {
                vm.LoadDumpFromFile(dumpPath);
            }

            // Set up dual dump comparison data
            var dumpBPath = Path.Combine(solutionRoot, "tests", "SCEWIN_Studio.Tests", "TestData", "nvram_dump_20260905_204705.txt");
            if (File.Exists(dumpBPath))
            {
                var parser = new Services.ScewinParser();
                vm.ComparisonDumpA = vm.CurrentDump;
                vm.ComparisonDumpAPath = "nvram_dump_20260905_202547.txt";
                vm.ComparisonDumpAInfo = "MSI MPG B550 GAMING PLUS • 1868 параметров";
                vm.ComparisonDumpB = parser.Parse(File.ReadAllText(dumpBPath), Path.GetFileName(dumpBPath));
                vm.ComparisonDumpBPath = "nvram_dump_20260905_204705.txt";
                vm.ComparisonDumpBInfo = "AM5 Profile (Друг) • 3140 параметров";
                vm.RunComparisonCommand.Execute(null);
            }

            var screenshotsDir = Path.Combine(solutionRoot, "docs", "screenshots");
            Directory.CreateDirectory(screenshotsDir);

            var pageMap = new Dictionary<string, string>
            {
                { "Dashboard", "01_dashboard.png" },
                { "Memory", "02_memory_tuning.png" },
                { "Overclocking", "08_overclocking.png" },
                { "PciePower", "03_pcie_power.png" },
                { "CpuPower", "04_cpu_power.png" },
                { "RawTokens", "05_raw_tokens.png" },
                { "ProfilesDiff", "06_dual_dump_comparison.png" },
                { "Settings", "07_settings.png" }
            };

            var visual = (UIElement)window.Content;
            visual.Measure(new Size(1350, 850));
            visual.Arrange(new Rect(0, 0, 1350, 850));

            foreach (var kvp in pageMap)
            {
                var page = kvp.Key;
                var fileName = kvp.Value;

                window.NavigateTo(page);
                visual.UpdateLayout();

                // Pump dispatcher to finish any pending bindings/layout
                var frame = new System.Windows.Threading.DispatcherFrame();
                window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                visual.UpdateLayout();

                var rtb = new RenderTargetBitmap(1350, 850, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(visual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                var outPath = Path.Combine(screenshotsDir, fileName);
                using (var fs = File.Create(outPath))
                {
                    encoder.Save(fs);
                }
                Console.WriteLine($"Successfully rendered: {page} -> {outPath}");
            }
        });
    }

    [Fact]
    public void RenderMemoryViewToImage()
    {
        RunOnWpfThread(() =>
        {
            var window = new MainWindow();
            var solutionRoot = FindSolutionRoot();
            var dumpPath = Path.Combine(solutionRoot, "src", "SCEWIN_Studio", "nvramBEFORE.txt");
            if (File.Exists(dumpPath))
            {
                var vm = (MainViewModel)window.DataContext;
                vm.LoadDumpFromFile(dumpPath);
            }

            window.NavigateTo("Memory");

            var visual = (UIElement)window.Content;
            visual.Measure(new Size(1300, 950));
            visual.Arrange(new Rect(0, 0, 1300, 950));
            visual.UpdateLayout();

            // Pump dispatcher to finish any pending layout/animations
            var frame = new System.Windows.Threading.DispatcherFrame();
            window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            visual.UpdateLayout();

            var rtb = new RenderTargetBitmap(1300, 950, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            var outPath = Path.Combine(solutionRoot, "tests", "rendered_memory.png");
            using (var fs = File.Create(outPath))
            {
                encoder.Save(fs);
            }

            // Also capture scrolled view to verify the collapsible expanders / spoilers
            var expanders = FindVisualChildren<Expander>(visual).ToList();
            if (expanders.Count > 1)
            {
                expanders[1].BringIntoView();
                visual.UpdateLayout();

                var rtbScrolled = new RenderTargetBitmap(1300, 950, 96, 96, PixelFormats.Pbgra32);
                rtbScrolled.Render(visual);

                var encoderScrolled = new PngBitmapEncoder();
                encoderScrolled.Frames.Add(BitmapFrame.Create(rtbScrolled));
                var outScrolledPath = Path.Combine(solutionRoot, "tests", "rendered_memory_spoilers.png");
                using (var fs = File.Create(outScrolledPath))
                {
                    encoderScrolled.Save(fs);
                }

                // Also capture expanded spoiler view to verify open state
                expanders[0].IsExpanded = true;
                visual.Measure(new Size(1300, 1400));
                visual.Arrange(new Rect(0, 0, 1300, 1400));
                visual.UpdateLayout();

                var rtbExpanded = new RenderTargetBitmap(1300, 1400, 96, 96, PixelFormats.Pbgra32);
                rtbExpanded.Render(visual);

                var encoderExpanded = new PngBitmapEncoder();
                encoderExpanded.Frames.Add(BitmapFrame.Create(rtbExpanded));
                var outExpandedPath = Path.Combine(solutionRoot, "tests", "rendered_memory_expanded.png");
                using (var fs = File.Create(outExpandedPath))
                {
                    encoderExpanded.Save(fs);
                }
            }
        });
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) yield return typedChild;
            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            var found = FindVisualChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }
}
