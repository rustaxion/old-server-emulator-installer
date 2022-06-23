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
        private Debouncer _discordGameSdkInstallDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
        private Debouncer _gamePatchesInstallDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
        private Debouncer _modInstallDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
        private Debouncer _bepinExInstallDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
        private Debouncer _installPathDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
        private VistaFolderBrowserDialog _dialog = new() { Description = "Please select game folder", UseDescriptionForTitle = true };

        private TextBoxOutputter outputter;

        private string _selectedFolder = "";
        private static Label _statusLabel;
        private bool _autoScroll = true;

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // User scroll event : set or unset auto-scroll mode
            if (e.ExtentHeightChange == 0)
            {
                // Content unchanged : user scroll event
                _autoScroll = ScrollViewer.VerticalOffset == ScrollViewer.ScrollableHeight;
            }

            // Content scroll event : auto-scroll eventually
            if (_autoScroll && e.ExtentHeightChange != 0)
            { // Content changed and auto-scroll mode set
                // Autoscroll
                ScrollViewer.ScrollToVerticalOffset(ScrollViewer.ExtentHeight);
            }

            if (e.ExtentHeightChange == 0)
                return;
            var lines = outputter.LogBlock.Text.Split("\n");
            _statusLabel.Content = lines[^2];
        }

        public MainWindow()
        {
            InitializeComponent();
            outputter = new TextBoxOutputter(ProgressLog);
            Console.SetOut(outputter);
            Console.WriteLine("Waiting for input...");
            _installPathDebouncer = new Debouncer(TimeSpan.FromSeconds(.400), SetInstallPath);
            if (!File.Exists(_selectedFolder + "\\INVAXION.exe"))
                StartInstallButton.IsEnabled = false;

            _statusLabel = FindName("StatusText") as Label;
        }

        private void LogProgress(string name, long totalFileSize, long totalBytesDownloaded, double progressPercentage, Action onComplete)
        {
            var progress = $"{progressPercentage}% ({FormatFileSize(totalBytesDownloaded)}/{FormatFileSize(totalFileSize)})";
            var lines = outputter.LogBlock.Text.Split("\n");
            if (progressPercentage < 100)
            {
                if (lines[^2].StartsWith($"Downloading {name} | "))
                {
                    lines[^2] = $"Downloading {name} | {progress}";
                    outputter.LogBlock.Text = string.Join("\n", lines);
                    _statusLabel.Content = lines[^2];
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
                    outputter.LogBlock.Text = string.Join("\n", lines);
                    _statusLabel.Content = lines[^2];
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
            if ((bool)_dialog.ShowDialog(this))
            {
                _selectedFolder = _dialog.SelectedPath;
                InstallPath.Text = _dialog.SelectedPath;
            }
        }

        private void SetInstallPath()
        {
            this.Dispatcher.Invoke(() =>
            {
                if (File.Exists(InstallPath.Text + "\\INVAXION.exe"))
                {
                    _selectedFolder = InstallPath.Text;
                    _dialog.SelectedPath = InstallPath.Text;
                    StartInstallButton.IsEnabled = true;
                    Console.WriteLine($"Install directory was successfully set to {_selectedFolder}");
                }
                else
                {
                    Console.WriteLine($"Invalid install directory selected: {_selectedFolder}");
                    StartInstallButton.IsEnabled = false;
                }
            });
        }

        private void InstallPathTextChanged(object sender, EventArgs e)
        {
            _installPathDebouncer.Invoke();
        }

        private bool _exitBtnEnabled;

        private void StartInstall(object sender, EventArgs e)
        {
            if (_exitBtnEnabled)
                Application.Current.Shutdown();
            ShowDialogButton.IsEnabled = false;
            StartInstallButton.IsEnabled = false;
            PBar.IsIndeterminate = true;
            StartBepinExDownload();
        }

        private static string FormatFileSize(long bytes)
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
            var patchesPath = Path.Combine(Path.GetTempPath(), "INVAXION_patches.zip");
            var patchesExtractPath = Path.Combine(Path.GetTempPath(), "INVAXION_patches");
            var bepinexPath = Path.Combine(Path.GetTempPath(), "BepInEx_x64_5.4.19.0.zip");
            var discordGameDskDownloadPath = Path.Combine(Path.GetTempPath(), "discord_game_sdk_2.5.6.zip");
            var discordGameSdkExtractPath = Path.Combine(Path.GetTempPath(), "discord_game_sdk_2.5.6");
            Console.WriteLine("Deleting discord_game_sdk_2.5.6.zip");
            File.Delete(discordGameDskDownloadPath);
            Console.WriteLine("Deleted discord_game_sdk_2.5.6.zip");
            Directory.Delete(discordGameSdkExtractPath, true);
            Console.WriteLine("Deleting BepInEx_x64_5.4.19.0.zip");
            File.Delete(bepinexPath);
            Console.WriteLine("Deleted BepInEx_x64_5.4.19.0.zip");
            Console.WriteLine("Deleting INVAXION_patches.zip");
            File.Delete(patchesPath);
            Console.WriteLine("Deleted INVAXION_patches.zip");
            Directory.Delete(patchesExtractPath, true);
            Console.WriteLine("Clean up completed successfully you may now close the window.");
        }

        private void StartBepinExDownload()
        {
            const string name = "BepinEx v5.4.19.0";
            var downloadPath = Path.Combine(Path.GetTempPath(), "BepInEx_x64_5.4.19.0.zip");
            const string downloadLink = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.19/BepInEx_x64_5.4.19.0.zip";

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
                                    _bepinExInstallDebouncer = new Debouncer(
                                        TimeSpan.FromSeconds(.2),
                                        () =>
                                        {
                                            this.Dispatcher.Invoke(() =>
                                            {
                                                ZipFile.ExtractToDirectory(downloadPath, _selectedFolder, true);
                                                Console.WriteLine("Extracting BepinEx v5.4.19.0 to install directory");

                                                Console.WriteLine("Extracted BepinEx v5.4.19.0 to install directory");
                                                StartDiscordGameSdkDownload();
                                            });
                                        }
                                    );
                                    _bepinExInstallDebouncer.Invoke();
                                }
                            );
                        });
                    };
                    await client.StartDownload();
                });
            thread.Start();
        }

        private void StartDiscordGameSdkDownload()
        {
            const string name = "Discord Game SDK v2.5.6";
            const string downloadLink = "https://dl-game-sdk.discordapp.net/2.5.6/discord_game_sdk.zip";
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
                                    _discordGameSdkInstallDebouncer = new Debouncer(
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
                                                    Path.Combine(_selectedFolder, "INVAXION_Data", "Plugins", "discord_game_sdk.dll"),
                                                    true
                                                );
                                                StartModDownload();
                                            });
                                        }
                                    );
                                    _discordGameSdkInstallDebouncer.Invoke();
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
            const string name = "Invaxion Server Emu@latest";
            const string downloadLink = "https://github.com/Invaxion-Server-Emulator/invaxion-server-emulator/releases/download/latest/ServerEmulator.dll";
            var downloadPath = Path.Combine(_selectedFolder, "BepInEx", "Plugins", "ServerEmulator.dll");

            if (!Directory.Exists(Path.Combine(_selectedFolder, "BepInEx", "Plugins")))
            {
                Directory.CreateDirectory(Path.Combine(_selectedFolder, "BepInEx", "Plugins"));
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
                                    _modInstallDebouncer = new Debouncer(
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
                                    _modInstallDebouncer.Invoke();
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
            const string name = "Game patches v6.0";
            const string downloadLink = "https://github.com/Invaxion-Server-Emulator/patches/releases/download/latest/patches-main.zip";
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
                                    _gamePatchesInstallDebouncer = new Debouncer(
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
                                                    foreach (var dir in Directory.GetDirectories(Path.Combine(extractionPath, "patches-main"), "*", SearchOption.AllDirectories))
                                                    {
                                                        Directory.CreateDirectory(Path.Combine(_selectedFolder, dir[(Path.Combine(extractionPath, "patches-main").Length + 1)..]));
                                                    }

                                                    foreach (var fileName in Directory.GetFiles(Path.Combine(extractionPath, "patches-main"), "*", SearchOption.AllDirectories))
                                                    {
                                                        File.Copy(fileName, Path.Combine(_selectedFolder, fileName[(Path.Combine(extractionPath, "patches-main").Length + 1)..]), true);
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
                                    _gamePatchesInstallDebouncer.Invoke();
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
            StartInstallButton.IsEnabled = false;
            StartInstallButton.Content = "Exit";
            StartInstallButton.Name = "ExitBtn";
            _exitBtnEnabled = true;
            StartInstallButton.IsEnabled = true;
        }
    }
}
