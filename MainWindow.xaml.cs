using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.UI;
using WinRT.Interop;
using System.Collections.Generic;

namespace WindowsCleaner
{
    public sealed partial class MainWindow : Window
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        private delegate IntPtr WinProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private readonly WinProc newWndProc;
        private readonly IntPtr oldWndProc = IntPtr.Zero;
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int GWL_WNDPROC = -4;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WinProc newProc);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint uintMsg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        private readonly SolidColorBrush OrangeBrush = new(Color.FromArgb(255, 255, 165, 0));
        private readonly SolidColorBrush GreenBrush = new(Color.FromArgb(255, 76, 175, 80));
        private readonly SolidColorBrush BlueBrush = new(Color.FromArgb(255, 33, 150, 243));
        private Brush GrayBrush => (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

        private readonly Dictionary<TextBlock, CancellationTokenSource> _fadeTokens = new();

        private int _minWidth = 620;
        private int _minHeight = 200;
        private bool _updateChecked = false;

        public MainWindow()
        {
            this.InitializeComponent();

            try
            {
                // Zuverlässiges Laden des Icons für das WinUI 3 AppWindow über einen Datei-Pfad
                string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                    Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                    // Setzt das Icon für Titelleiste und Taskleiste
                    appWindow.SetIcon(iconPath);
                }
            }
            catch { /* Fallback, falls Datei fehlt */ }

            IntPtr hwnd = WindowNative.GetWindowHandle(this);

            // Dynamische Anpassung an Inhalt und Skalierung aufrufen
            this.DispatcherQueue.TryEnqueue(() => AdjustWindowToContent());

            newWndProc = new WinProc(WindowProc);
            oldWndProc = SetWindowLongPtr(hwnd, GWL_WNDPROC, newWndProc);

            this.ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            UpdateTitleBarColors();
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.ActualThemeChanged += (s, e) => UpdateTitleBarColors();
                rootElement.Loaded += MainWindow_Loaded_CheckUpdate;
            }
        }

        private void AdjustWindowToContent()
        {
            if (this.Content is FrameworkElement rootElement)
            {
                // Erzwingt eine saubere Neuberechnung des Layouts
                rootElement.UpdateLayout();
                rootElement.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

                double scale = rootElement.XamlRoot?.RasterizationScale ?? 1.0;

                // Windows 11 hat unsichtbare Fensterrahmen (ca. 16px links/rechts zusammen, ca. 8px unten)
                int frameWidthOffset = (int)Math.Ceiling(16 * scale);
                int frameHeightOffset = (int)Math.Ceiling(8 * scale);

                int contentWidth = (int)Math.Ceiling(rootElement.DesiredSize.Width * scale);
                int contentHeight = (int)Math.Ceiling(rootElement.DesiredSize.Height * scale);

                // Mindestbreite 620 DIPs (skaliert)
                int targetWidth = Math.Max(contentWidth + frameWidthOffset, (int)(620 * scale));
                int targetHeight = contentHeight + frameHeightOffset;

                // Merkt sich die exakte Inhaltsgröße als minimale Fenstergröße
                _minWidth = targetWidth;
                _minHeight = targetHeight;

                this.AppWindow.Resize(new Windows.Graphics.SizeInt32(targetWidth, targetHeight));
            }
        }

        private void UpdateTitleBarColors()
        {
            var titleBar = this.AppWindow.TitleBar;
            if (titleBar == null) return;

            bool isDark = (this.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;

            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            if (isDark)
            {
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(15, 255, 255, 255);
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(10, 255, 255, 255);
                titleBar.ButtonPressedForegroundColor = Color.FromArgb(200, 255, 255, 255);
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(120, 255, 255, 255);
            }
            else
            {
                titleBar.ButtonForegroundColor = Colors.Black;
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(10, 0, 0, 0);
                titleBar.ButtonHoverForegroundColor = Colors.Black;
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(6, 0, 0, 0);
                titleBar.ButtonPressedForegroundColor = Color.FromArgb(200, 0, 0, 0);
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(120, 0, 0, 0);
            }
        }

        private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_GETMINMAXINFO && lParam != IntPtr.Zero)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                mmi.ptMinTrackSize.x = _minWidth;
                mmi.ptMinTrackSize.y = _minHeight;
                Marshal.StructureToPtr(mmi, lParam, true);
            }
            return CallWindowProc(oldWndProc, hWnd, msg, wParam, lParam);
        }

        private async void CleanJunk_Click(object sender, RoutedEventArgs e)
        {
            string tempPath = Path.GetTempPath();
            int deleted = await Task.Run(() => DeleteFiles(tempPath));

            if (deleted == 0)
            {
                ShowMessage(JunkStatusText, "Keine weiteren Junk-Dateien", GrayBrush);
            }
            else
            {
                ShowMessage(JunkStatusText, $"{deleted} Dateien gelöscht", GreenBrush);
            }
        }

        private async void CleanBrowser_Click(object sender, RoutedEventArgs e)
        {
            string edgeCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"AppData\Local\Microsoft\Edge\User Data\Default\Cache");
            int deleted = await Task.Run(() => DeleteFiles(edgeCache));

            if (deleted == 0)
            {
                ShowMessage(BrowserStatusText, "Kein weiterer Browser-Cache", GrayBrush);
            }
            else
            {
                ShowMessage(BrowserStatusText, $"{deleted} Dateien gelöscht", GreenBrush);
            }
        }

        private async void CleanExplorer_Click(object sender, RoutedEventArgs e)
        {
            string recentPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"AppData\Roaming\Microsoft\Windows\Recent");
            int deleted = await Task.Run(() => DeleteFiles(recentPath));

            if (deleted == 0)
            {
                ShowMessage(ExplorerStatusText, "Kein Explorer Verlauf vorhanden", GrayBrush);
            }
            else
            {
                ShowMessage(ExplorerStatusText, $"{deleted} Dateien gelöscht", GreenBrush);
            }
        }

        private async void FlushDns_Click(object sender, RoutedEventArgs e)
        {
            bool hasDnsEntries = await Task.Run(() => {
                try
                {
                    var psi = new ProcessStartInfo("ipconfig", "/displaydns")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc == null) return false;

                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    return output.Contains("Datensatz") || output.Contains("Record");
                }
                catch { return true; }
            });

            if (!hasDnsEntries)
            {
                ShowMessage(DnsStatusText, "Kein DNS-Cache vorhanden", GrayBrush);
                return;
            }

            ShowMessage(DnsStatusText, "DNS wird geleert...", BlueBrush);

            await Task.Run(() => {
                var startInfo = new ProcessStartInfo("ipconfig", "/flushdns") { CreateNoWindow = true, UseShellExecute = false };
                using var p = Process.Start(startInfo);
                p?.WaitForExit();
            });

            ShowMessage(DnsStatusText, "DNS-Cache bereinigt", GreenBrush);
        }

        private async void EmptyBin_Click(object sender, RoutedEventArgs e)
        {
            bool hasItems = await Task.Run(() => {
                try
                {
                    SHQUERYRBINFO qrb = new();
                    qrb.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
                    int hr = SHQueryRecycleBin(null, ref qrb);
                    return hr == 0 && (qrb.i64NumItems > 0 || qrb.i64Size > 0);
                }
                catch { return true; }
            });

            if (!hasItems)
            {
                ShowMessage(BinStatusText, "Papierkorb ist leer", GrayBrush);
                return;
            }

            ShowMessage(BinStatusText, "Papierkorb wird geleert...", OrangeBrush);

            await Task.Run(() => {
                try
                {
                    SHEmptyRecycleBin(IntPtr.Zero, null, 0x00000001 | 0x00000002 | 0x00000004);
                }
                catch { }
            });

            ShowMessage(BinStatusText, "Papierkorb wurde geleert", GreenBrush);
        }

        private async void ShowMessage(TextBlock textBlock, string message, Brush brush)
        {
            if (_fadeTokens.TryGetValue(textBlock, out var oldCts))
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var cts = new CancellationTokenSource();
            _fadeTokens[textBlock] = cts;

            textBlock.Foreground = brush;
            textBlock.Opacity = 1.0;
            textBlock.Text = message;

            try
            {
                await Task.Delay(3000, cts.Token);

                var sb = new Storyboard();
                var anim = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(500))
                };
                Storyboard.SetTarget(anim, textBlock);
                Storyboard.SetTargetProperty(anim, "Opacity");
                sb.Children.Add(anim);

                sb.Begin();
                await Task.Delay(500, cts.Token);

                textBlock.Text = "";
                textBlock.Opacity = 1.0;
            }
            catch (TaskCanceledException)
            {
                // Unterbrochen durch erneuten Klick
            }
        }

        private int DeleteFiles(string targetDir)
        {
            int count = 0;
            if (!Directory.Exists(targetDir)) return 0;
            try
            {
                foreach (var file in Directory.GetFiles(targetDir))
                {
                    try { File.Delete(file); count++; } catch { }
                }
                foreach (var dir in Directory.GetDirectories(targetDir))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                        count++;
                    }
                    catch { }
                }
            }
            catch { }
            return count;
        }

        private async void MainWindow_Loaded_CheckUpdate(object sender, RoutedEventArgs e)
        {
            if (_updateChecked) return;
            _updateChecked = true;

            try
            {
                var update = await GitHubUpdater.CheckForUpdatesAsync();
                if (update.IsUpdateAvailable && !string.IsNullOrEmpty(update.DownloadUrl))
                {
                    var contentPanel = new StackPanel { Spacing = 8 };

                    contentPanel.Children.Add(new TextBlock
                    {
                        Text = "Eine neue Version von Windows Cleaner steht bereit. Möchtest du sie jetzt herunterladen und installieren?",
                        TextWrapping = TextWrapping.Wrap
                    });

                    if (!string.IsNullOrEmpty(update.HtmlUrl) && Uri.TryCreate(update.HtmlUrl, UriKind.Absolute, out Uri? releaseUri))
                    {
                        contentPanel.Children.Add(new HyperlinkButton
                        {
                            Content = "Änderungen auf GitHub ansehen",
                            NavigateUri = releaseUri,
                            Padding = new Thickness(0, 2, 0, 2)
                        });
                    }

                    // Ein Grid mit 2 flexiblen Spalten (*), damit sich die Größe perfekt anpasst
                    var buttonGrid = new Grid
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    };
                    buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var progressFill = new Border
                    {
                        Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        CornerRadius = new CornerRadius(4),
                        Opacity = 0.85
                    };
                    // Spannt sich über die Spalten auf und wird per Breite gesteuert
                    Grid.SetColumnSpan(progressFill, 2);
                    progressFill.Width = 0;
                    progressFill.HorizontalAlignment = HorizontalAlignment.Left;

                    var buttonLabel = new TextBlock
                    {
                        Text = "Jetzt aktualisieren",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumnSpan(buttonLabel, 2);

                    buttonGrid.Children.Add(progressFill);
                    buttonGrid.Children.Add(buttonLabel);

                    var dialog = new ContentDialog
                    {
                        Title = $"Update verfügbar ({update.LatestVersion})",
                        Content = contentPanel,
                        PrimaryButtonText = "Jetzt aktualisieren",
                        CloseButtonText = "Später",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.Content.XamlRoot
                    };

                    Button? primaryBtn = null;
                    dialog.Opened += (s, args) =>
                    {
                        primaryBtn = FindVisualChild<Button>(dialog, b => b.Content?.ToString() == "Jetzt aktualisieren");
                        if (primaryBtn != null)
                        {
                            // Übernimmt exakt die Streckung und Ausrichtung des Standard-Buttons
                            primaryBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
                            primaryBtn.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                            primaryBtn.VerticalContentAlignment = VerticalAlignment.Stretch;
                            primaryBtn.Content = buttonGrid;
                        }
                    };

                    dialog.PrimaryButtonClick += async (s, args) =>
                    {
                        // Verhindert, dass sich das Update-Fenster schließt!
                        args.Cancel = true;

                        // Beide Buttons sperren, damit man während des Downloads nicht nochmal klickt
                        dialog.IsPrimaryButtonEnabled = false;
                        dialog.IsSecondaryButtonEnabled = false;
                        if (primaryBtn != null) primaryBtn.IsEnabled = false;

                        // "Später"-Button ebenfalls deaktivieren
                        var closeBtn = FindVisualChild<Button>(dialog, b => b.Content?.ToString() == "Später");
                        if (closeBtn != null) closeBtn.IsEnabled = false;

                        buttonLabel.Text = "0 %";

                        var progress = new Progress<double>(percent =>
                        {
                            double totalWidth = buttonGrid.ActualWidth;
                            if (totalWidth > 0)
                            {
                                progressFill.Width = totalWidth * (percent / 100.0);
                            }
                            buttonLabel.Text = $"{Math.Round(percent)} %";
                        });

                        try
                        {
                            await GitHubUpdater.DownloadAndInstallUpdateAsync(update.DownloadUrl, progress);
                        }
                        catch (Exception ex)
                        {
                            buttonLabel.Text = "Fehler beim Download";
                            if (closeBtn != null) closeBtn.IsEnabled = true;
                            Debug.WriteLine($"Download-Fehler: {ex.Message}");
                        }
                    };

                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update-Prüfung fehlgeschlagen: {ex.Message}");
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent, Func<T, bool>? predicate = null) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && (predicate == null || predicate(typedChild)))
                {
                    return typedChild;
                }

                var result = FindVisualChild(child, predicate);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}