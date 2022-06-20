using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Ookii.Dialogs.Wpf;

namespace Invaxion_Server_Emulator_Installer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Debouncer _BepinExInstallDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
        private Debouncer _InstallPathDebouncer = new(TimeSpan.FromSeconds(.400), () => { });
        private VistaFolderBrowserDialog dialog =
            new() { Description = "Please select game folder", UseDescriptionForTitle = true };

        TextBoxOutputter outputter;

        private string SelectedFolder = "";

        private Boolean AutoScroll = true;

        private void ScrollViewer_ScrollChanged(Object sender, ScrollChangedEventArgs e)
        {
            // User scroll event : set or unset auto-scroll mode
            if (e.ExtentHeightChange == 0)
            { // Content unchanged : user scroll event
                if (ScrollViewer.VerticalOffset == ScrollViewer.ScrollableHeight)
                { // Scroll bar is in bottom
                    // Set auto-scroll mode
                    AutoScroll = true;
                }
                else
                { // Scroll bar isn't in bottom
                    // Unset auto-scroll mode
                    AutoScroll = false;
                }
            }

            // Content scroll event : auto-scroll eventually
            if (AutoScroll && e.ExtentHeightChange != 0)
            { // Content changed and auto-scroll mode set
                // Autoscroll
                ScrollViewer.ScrollToVerticalOffset(ScrollViewer.ExtentHeight);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            outputter = new TextBoxOutputter(ProgressLog);
            Console.SetOut(outputter);
            Console.WriteLine("Waiting for input...");
            _InstallPathDebouncer = new Debouncer(TimeSpan.FromSeconds(.400), SetInstallPath);
            if (!File.Exists(SelectedFolder + "\\INVAXION.exe"))
            {
                _StartInstall.IsEnabled = false;
            }
            ;
        }

        private void SelectInstallFolder(object sender, RoutedEventArgs e)
        {
            if (!VistaFolderBrowserDialog.IsVistaFolderDialogSupported)
            {
                MessageBox.Show(
                    this,
                    "Because you are not using Windows Vista or later, the regular folder browser dialog will be used. Please use Windows Vista to see the new dialog.",
                    "Please select game folder"
                );
            }
            if ((bool)dialog.ShowDialog(this))
            {
                SelectedFolder = dialog.SelectedPath;
                InstallPath.Text = dialog.SelectedPath;
            }
        }

        private void SetInstallPath()
        {
            this.Dispatcher.Invoke(() =>
            {
                if (File.Exists(InstallPath.Text + "\\INVAXION.exe"))
                {
                    SelectedFolder = InstallPath.Text;
                    dialog.SelectedPath = InstallPath.Text;
                    _StartInstall.IsEnabled = true;
                    Console.WriteLine($"Install directory was successfully set to {SelectedFolder}");
                }
                else
                {
                    Console.WriteLine($"Invalid install directory selected: {SelectedFolder}");

                    _StartInstall.IsEnabled = false;
                }
                ;
            });
        }

        private void InstallPathTextChanged(object sender, EventArgs e)
        {
            _InstallPathDebouncer.Invoke();
        }

        private void StartInstall(object sender, EventArgs e)
        {
            PBar.IsIndeterminate = true;
            StartBepinExDownload();
        }

        public static string FormatFileSize(long bytes)
        {
            var unit = 1024;
            if (bytes < unit)
            {
                return $"{bytes} B";
            }

            var exp = (int)(Math.Log(bytes) / Math.Log(unit));
            return $"{bytes / Math.Pow(unit, exp):F2} {("KMGTPE")[exp - 1]}B";
        }

        private void StartBepinExDownload()
        {
            Thread thread =
                new(async () =>
                {
                    HttpClientDownloadWithProgress client =
                        new(
                            "https://github.com/BepInEx/BepInEx/releases/download/v5.4.19/BepInEx_x64_5.4.19.0.zip",
                            $"{System.IO.Path.GetTempPath()}\\BepInEx_x64_5.4.19.0.zip"
                        );
                    client.ProgressChanged += (
                        totalFileSize,
                        totalBytesDownloaded,
                        progressPercentage
                    ) =>
                    {
                        if (progressPercentage < 100)
                        {
                            Console.WriteLine(
                                $"Downloading BepinEx v5.4.19.0 {progressPercentage}% ({FormatFileSize(totalBytesDownloaded)}/{FormatFileSize((long)totalFileSize)})"
                            );
                        }
                        else
                        {
                            Console.WriteLine(
                                $"Downloading BepinEx v5.4.19.0 {progressPercentage}% ({FormatFileSize(totalBytesDownloaded)}/{FormatFileSize((long)totalFileSize)})"
                            );
                            _BepinExInstallDebouncer = new Debouncer(
                                TimeSpan.FromSeconds(.2),
                                () =>
                                {
                                    this.Dispatcher.Invoke(() =>
                                    {
                                        ZipFile.ExtractToDirectory(
                                            $"{System.IO.Path.GetTempPath()}\\BepInEx_x64_5.4.19.0.zip",
                                            SelectedFolder, true
                                        );
                                        Console.WriteLine("Extracting BepinEx v5.4.19.0 to install directory");
                                        PBar.IsIndeterminate = false;
                                        PBar.Value = 100;
                                        Console.WriteLine("Extracted BepinEx v5.4.19.0 to install directory");
                                    });
                                }
                            );

                            _BepinExInstallDebouncer.Invoke();
                        }
                        ;
                    };
                    await client.StartDownload();
                });
            thread.Start();
            ;
        }
    }
}
