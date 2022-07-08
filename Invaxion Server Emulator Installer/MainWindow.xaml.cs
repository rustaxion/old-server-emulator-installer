using Ookii.Dialogs.Wpf;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Invaxion_Server_Emulator_Installer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static void CopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            Directory.CreateDirectory(target.FullName);
            foreach (FileInfo fi in source.GetFiles())
            {
                Console.WriteLine(@"Copying {0}\{1}", target.FullName, fi.Name);
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }

        private Debouncer _discordGameSdkInstallDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
        private Debouncer _modInstallDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
        private Debouncer _zedrainsGameUpdatesDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
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
            _statusLabel.Content = lines[^2].Substring(11);
            ;
        }

        public MainWindow()
        {
            InitializeComponent();
            outputter = new TextBoxOutputter(ProgressLog);
            Console.SetOut(outputter);
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Waiting for input...");
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
                if (lines[^2].Contains($"Downloading {name} | "))
                {
                    lines[^2] = $"[{DateTime.Now.ToString("HH:mm:ss")}] Downloading {name} | {progress}";
                    outputter.LogBlock.Text = string.Join("\n", lines);
                    _statusLabel.Content = lines[^2].Substring(11);
                    ;
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Downloading {name} | {progress}");
                }
            }
            else
            {
                if (lines[^2].Contains($"Downloading {name} | "))
                {
                    lines[^2] = $"[{DateTime.Now.ToString("HH:mm:ss")}] Downloading {name} | {progress}";
                    outputter.LogBlock.Text = string.Join("\n", lines);
                    _statusLabel.Content = lines[^2].Substring(11);
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Downloading {name} | {progress}");
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
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Install directory was successfully set to {_selectedFolder}");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Invalid install directory selected: {_selectedFolder}");
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
            DownloadZedRainUpdates();
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
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Install completed, starting cleanup");
            var bepinexPath = Path.Combine(Path.GetTempPath(), "BepInEx_x64_5.4.19.0.zip");
            var discordGameDskDownloadPath = Path.Combine(Path.GetTempPath(), "discord_game_sdk_2.5.6.zip");
            var discordGameSdkExtractPath = Path.Combine(Path.GetTempPath(), "discord_game_sdk_2.5.6");
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Deleting discord_game_sdk_2.5.6.zip");
            File.Delete(discordGameDskDownloadPath);
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Deleted discord_game_sdk_2.5.6.zip");
            Directory.Delete(discordGameSdkExtractPath, true);
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Deleting BepInEx_x64_5.4.19.0.zip");
            File.Delete(bepinexPath);
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Deleted BepInEx_x64_5.4.19.0.zip");

            var zedrainsUpdate = Path.Combine(Path.GetTempPath(), "ZedRain's_update_6.0.zip");
            var zedrainsUpdateExtracted = Path.Combine(Path.GetTempPath(), "ZedRain's_update_6.0_extracted");
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Deleting ZedRain's_update_6.0.zip");
            File.Delete(zedrainsUpdate);
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Deleted ZedRain's_update_6.0.zip");
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Deleting ZedRain's_update_6.0_extracted");
            Directory.Delete(zedrainsUpdateExtracted, true);
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Deleted ZedRain's_update_6.0_extracted");

            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Clean up completed successfully you may now close the window.");
        }

        private string GetFilenameFromLink(string link)
        {
            return link.TrimEnd('/').Split("/").Last();
        }

        private void DownloadZedRainUpdates()
        {
            const string downloadLink = "https://github.com/Invaxion-Server-Emulator/zedrain-private-server-game-assets/releases/download/6.0/6.0.zip";
            var name = "ZedRain's_update_" + GetFilenameFromLink(downloadLink);
            var downloadPath = Path.Combine(Path.GetTempPath(), name);
            var extractPath = $"{downloadPath.Replace(".zip", "")}_extracted";
            if (!Directory.Exists(extractPath))
            {
                Directory.CreateDirectory(extractPath);
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
                                totalFileSize ?? 0L,
                                totalBytesDownloaded,
                                progressPercentage ?? 0d,
                                () =>
                                {
                                    _zedrainsGameUpdatesDebouncer = new Debouncer(
                                        TimeSpan.FromSeconds(.2),
                                        () =>
                                        {
                                            this.Dispatcher.Invoke(() =>
                                            {
                                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Extracting {name}...");
                                                ZipFile.ExtractToDirectory(downloadPath, extractPath, true);
                                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Extracted {name}!");

                                                var parentDir = extractPath;
                                                var tries = 0;
                                                while (tries < 5 && !parentDir.EndsWith("INVAXION_Data"))
                                                {
                                                    var directories = Directory.EnumerateDirectories(parentDir).ToArray();
                                                    if (directories.Length == 1)
                                                    {
                                                        parentDir = directories[0];
                                                    }
                                                    else
                                                    {
                                                        foreach (var dir in directories)
                                                        {
                                                            if (dir.EndsWith("INVAXION_Data"))
                                                            {
                                                                parentDir = dir;
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }

                                                if (tries >= 5 && !parentDir.EndsWith("INVAXION_Data"))
                                                {
                                                    Console.WriteLine("An Error has occured...");
                                                    Thread.Sleep(1000);
                                                    Application.Current.Shutdown();
                                                }

                                                Console.WriteLine($"Applying {name} to the game...");
                                                CopyAll(new(parentDir), new(Path.Combine(_selectedFolder, "INVAXION_Data")));
                                                File.Delete(Path.Combine(_selectedFolder, "INVAXION_Data", "Managed", "0Harmony.dll"));

                                                StartBepinExDownload();
                                            });
                                        }
                                    );
                                    _zedrainsGameUpdatesDebouncer.Invoke();
                                }
                            );
                        });
                    };
                    await client.StartDownload();
                });
            thread.Start();
        }

        private void StartBepinExDownload()
        {
            const string downloadLink = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.19/BepInEx_x64_5.4.19.0.zip";
            var name = GetFilenameFromLink(downloadLink);
            var downloadPath = Path.Combine(Path.GetTempPath(), name);

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
                                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Extracting {name} to install directory");
                                                ZipFile.ExtractToDirectory(downloadPath, _selectedFolder, true);
                                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Extracted {name} to install directory");

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

                                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Extracting discord_game_sdk_2.5.6.zip");
                                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Extracted discord_game_sdk_2.5.6.zip");

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
            const string downloadLink = "https://github.com/Invaxion-Server-Emulator/invaxion-server-emulator/releases/latest/download/ServerEmulator.dll";
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
                                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Downloaded ServerEmulator.dll");
                                                CleanUp();
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
