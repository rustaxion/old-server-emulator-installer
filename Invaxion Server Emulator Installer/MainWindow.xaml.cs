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
        private Debouncer _DiscordGameSDKInstallDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
        private Debouncer _GamePatchesInstallDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
        private Debouncer _ModInstallDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
        private Debouncer _BepinExInstallDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
        private Debouncer _InstallPathDebouncer = new(TimeSpan.FromSeconds(.400), () => { });
        private VistaFolderBrowserDialog dialog = new() { Description = "Please select game folder", UseDescriptionForTitle = true };

        TextBoxOutputter outputter;

        private string SelectedFolder = "";
        private static Label statusLabel;
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

            if (e.ExtentHeightChange != 0)
            {
                var lines = outputter.LogBlock.Text.Split("\n");
                statusLabel.Content = lines[^2];
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

            statusLabel = FindName("_statusText") as Label;
        }

        private void LogProgress(string name, long totalFileSize, long totalBytesDownloaded, double progressPercentage, Action onComplete)
        {
            var progress = $"{progressPercentage}% ({FormatFileSize(totalBytesDownloaded)}/{FormatFileSize((long)totalFileSize)})";
            var lines = outputter.LogBlock.Text.Split("\n");
            if (progressPercentage < 100)
            {
                if (lines[^2].StartsWith($"Downloading {name} | "))
                {
                    lines[^2] = $"Downloading {name} | {progress}";
                    outputter.LogBlock.Text = String.Join("\n", lines);
                    statusLabel.Content = lines[^2];
                }
                else
                {
                    Console.WriteLine($"Downloading {name} | {progress}");
                }
            }
            else
            {
                if (lines[^2].StartsWith($"Downloading {name} | "))
                {
                    lines[^2] = $"Downloading {name} | {progress}";
                    outputter.LogBlock.Text = String.Join("\n", lines);
                    statusLabel.Content = lines[^2];
                }
                else
                {
                    Console.WriteLine($"Downloading {name} | {progress}");
                }
                onComplete();
            }
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

        private bool ExitBtnEnabled = false;

        private void StartInstall(object sender, EventArgs e)
        {
            if (ExitBtnEnabled)
                Application.Current.Shutdown();
            _showDialogButton.IsEnabled = false;
            _StartInstall.IsEnabled = false;
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

        private static void StartCleaningUp()
        {
            Console.WriteLine("Install completed, starting cleanup");
            var patches_PATH = Path.Combine(Path.GetTempPath(), "INVAXION_patches.zip");
            var patches_extract_PATH = Path.Combine(Path.GetTempPath(), "INVAXION_patches");
            var bepinex_PATH = Path.Combine(Path.GetTempPath(), "BepInEx_x64_5.4.19.0.zip");
            var discord_game_dsk_download_PATH = Path.Combine(Path.GetTempPath(), "discord_game_sdk_2.5.6.zip");
            var discord_game_sdk_extract_PATH = Path.Combine(Path.GetTempPath(), "discord_game_sdk_2.5.6");
            Console.WriteLine("Deleting discord_game_sdk_2.5.6.zip");
            File.Delete(discord_game_dsk_download_PATH);
            Console.WriteLine("Deleted discord_game_sdk_2.5.6.zip");
            Directory.Delete(discord_game_sdk_extract_PATH, true);
            Console.WriteLine("Deleting BepInEx_x64_5.4.19.0.zip");
            File.Delete(bepinex_PATH);
            Console.WriteLine("Deleted BepInEx_x64_5.4.19.0.zip");
            Console.WriteLine("Deleting INVAXION_patches.zip");
            File.Delete(patches_PATH);
            Console.WriteLine("Deleted INVAXION_patches.zip");
            Directory.Delete(patches_extract_PATH, true);
            Console.WriteLine("Clean up completed successfully you may now close the window.");
        }

        private void StartBepinExDownload()
        {
            var name = "BepinEx v5.4.19.0";
            var downloadPath = Path.Combine(Path.GetTempPath(), "BepInEx_x64_5.4.19.0.zip");
            var downloadLink = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.19/BepInEx_x64_5.4.19.0.zip";

            Thread thread =
                new(async () =>
                {
                    HttpClientDownloadWithProgress client = new(downloadLink, downloadPath);
                    client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            LogProgress(
                                name,
                                totalFileSize ?? long.Parse("0"),
                                totalBytesDownloaded,
                                progressPercentage ?? double.Parse("0.0"),
                                () =>
                                {
                                    _BepinExInstallDebouncer = new Debouncer(
                                        TimeSpan.FromSeconds(.2),
                                        () =>
                                        {
                                            this.Dispatcher.Invoke(() =>
                                            {
                                                ZipFile.ExtractToDirectory(downloadPath, SelectedFolder, true);
                                                Console.WriteLine("Extracting BepinEx v5.4.19.0 to install directory");

                                                Console.WriteLine("Extracted BepinEx v5.4.19.0 to install directory");
                                                StartDiscordGameSDKDownload();
                                            });
                                        }
                                    );
                                    _BepinExInstallDebouncer.Invoke();
                                }
                            );
                        });
                    };
                    await client.StartDownload();
                });
            thread.Start();
        }

        private void StartDiscordGameSDKDownload()
        {
            var name = "Discord Game SDK v2.5.6";
            var downloadLink = "https://dl-game-sdk.discordapp.net/2.5.6/discord_game_sdk.zip";
            var downloadPath = Path.Combine(Path.GetTempPath(), "discord_game_sdk_2.5.6.zip");
            var extractionPath = Path.Combine(Path.GetTempPath(), "discord_game_sdk_2.5.6");

            if (!Directory.Exists(extractionPath))
            {
                Directory.CreateDirectory(extractionPath);
            }

            Thread thread =
                new(async () =>
                {
                    HttpClientDownloadWithProgress client = new(downloadLink, downloadPath);
                    client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            LogProgress(
                                name,
                                totalFileSize ?? long.Parse("0"),
                                totalBytesDownloaded,
                                progressPercentage ?? double.Parse("0.0"),
                                () =>
                                {
                                    _DiscordGameSDKInstallDebouncer = new Debouncer(
                                        TimeSpan.FromSeconds(.2),
                                        () =>
                                        {
                                            this.Dispatcher.Invoke(() =>
                                            {
                                                ZipFile.ExtractToDirectory(downloadPath, extractionPath, true);

                                                Console.WriteLine("Extracting discord_game_sdk_2.5.6.zip");
                                                Console.WriteLine("Extracted discord_game_sdk_2.5.6.zip");

                                                File.Copy(
                                                    Path.Combine(extractionPath, "lib", "x86_64", "discord_game_sdk.dll"),
                                                    Path.Combine(SelectedFolder, "INVAXION_Data", "Plugins", "discord_game_sdk.dll"),
                                                    true
                                                );
                                                StartModDownload();
                                            });
                                        }
                                    );
                                    _DiscordGameSDKInstallDebouncer.Invoke();
                                }
                            );
                        });
                    };
                    await client.StartDownload();
                });
            thread.Start();
        }

        private void StartModDownload()
        {
            var name = "Invaxion Server Emu@latest";
            var downloadLink = "https://github.com/Invaxion-Server-Emulator/invaxion-server-emulator/releases/download/latest/ServerEmulator.dll";
            var downloadPath = Path.Combine(SelectedFolder, "BepInEx", "Plugins", "ServerEmulator.dll");

            if (!Directory.Exists(Path.Combine(SelectedFolder, "BepInEx", "Plugins")))
            {
                Directory.CreateDirectory(Path.Combine(SelectedFolder, "BepInEx", "Plugins"));
            }

            Thread thread =
                new(async () =>
                {
                    HttpClientDownloadWithProgress client = new(downloadLink, downloadPath);
                    client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            LogProgress(
                                name,
                                totalFileSize ?? long.Parse("0"),
                                totalBytesDownloaded,
                                progressPercentage ?? double.Parse("0.0"),
                                () =>
                                {
                                    _ModInstallDebouncer = new Debouncer(
                                        TimeSpan.FromSeconds(.2),
                                        () =>
                                        {
                                            this.Dispatcher.Invoke(() =>
                                            {
                                                Console.WriteLine("Downloaded ServerEmulator.dll");
                                                DownloadGamePatches();
                                            });
                                        }
                                    );
                                    _ModInstallDebouncer.Invoke();
                                }
                            );
                        });
                    };
                    await client.StartDownload();
                });
            thread.Start();
        }

        private void DownloadGamePatches()
        {
            var name = "Game patches v6.0";
            var downloadLink = "https://github.com/Invaxion-Server-Emulator/patches/releases/download/latest/patches-main.zip";
            var downloadPath = Path.Combine(Path.GetTempPath(), "INVAXION_patches.zip");
            var extractionPath = Path.Combine(Path.GetTempPath(), "INVAXION_patches");

            if (!Directory.Exists(extractionPath))
            {
                Directory.CreateDirectory(extractionPath);
            }
            Thread thread =
                new(async () =>
                {
                    HttpClientDownloadWithProgress client = new(downloadLink, downloadPath);
                    client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            LogProgress(
                                name,
                                totalFileSize ?? long.Parse("0"),
                                totalBytesDownloaded,
                                progressPercentage ?? double.Parse("0.0"),
                                () =>
                                {
                                    _GamePatchesInstallDebouncer = new Debouncer(
                                        TimeSpan.FromSeconds(.25),
                                        () =>
                                        {
                                            this.Dispatcher.Invoke(() =>
                                            {
                                                Console.WriteLine("Downloaded INVAXION_patches.zip");

                                                try
                                                {
                                                    Console.WriteLine("Extracting INVAXION_patches.zip");
                                                    ZipFile.ExtractToDirectory(downloadPath, extractionPath, true);
                                                    foreach (string dir in System.IO.Directory.GetDirectories(Path.Combine(extractionPath, "patches-main"), "*", System.IO.SearchOption.AllDirectories))
                                                    {
                                                        System.IO.Directory.CreateDirectory(
                                                            System.IO.Path.Combine(SelectedFolder, dir.Substring(Path.Combine(extractionPath, "patches-main").Length + 1))
                                                        );
                                                    }

                                                    foreach (string file_name in System.IO.Directory.GetFiles(Path.Combine(extractionPath, "patches-main"), "*", System.IO.SearchOption.AllDirectories))
                                                    {
                                                        System.IO.File.Copy(
                                                            file_name,
                                                            System.IO.Path.Combine(SelectedFolder, file_name.Substring(Path.Combine(extractionPath, "patches-main").Length + 1)),
                                                            true
                                                        );
                                                    }
                                                }
                                                catch (Exception exception)
                                                {
                                                    Console.WriteLine(exception);
                                                    throw;
                                                }
                                                Console.WriteLine("Extracted INVAXION_patches.zip");

                                                PBar.IsIndeterminate = false;
                                                PBar.Value = 100;
                                                CleanUp();
                                            });
                                        }
                                    );
                                    _GamePatchesInstallDebouncer.Invoke();
                                }
                            );
                        });
                    };
                    await client.StartDownload();
                });
            thread.Start();
        }

        private void CleanUp()
        {
            StartCleaningUp();
            _StartInstall.IsEnabled = false;
            _StartInstall.Content = "Exit";
            _StartInstall.Name = "ExitBtn";
            ExitBtnEnabled = true;
            _StartInstall.IsEnabled = true;
        }
    }
}
