using Ookii.Dialogs.Wpf;
using System;
using System.IO;
using System.IO.Compression;
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
        private Debouncer _BepinExInstallDebouncer = new(TimeSpan.FromSeconds(.200), () => { });
        private Debouncer _InstallPathDebouncer = new(TimeSpan.FromSeconds(.400), () => { });
        private VistaFolderBrowserDialog dialog =
            new() { Description = "Please select game folder", UseDescriptionForTitle = true };

        TextBoxOutputter outputter;

        private string SelectedFolder = "";
        public static Label statusLabel;
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
                statusLabel.Content = lines[lines.Length - 2];
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
            if (ExitBtnEnabled) Application.Current.Shutdown();

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
            var name = "BepinEx v5.4.19.0";
            var downloadPath = $"{Path.GetTempPath()}\\BepInEx_x64_5.4.19.0.zip";
            var downloadLink = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.19/BepInEx_x64_5.4.19.0.zip";

            Thread thread = new(async () =>
                {
                    HttpClientDownloadWithProgress client = new(downloadLink, downloadPath);
                    client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                    {
                        var progress = $"{progressPercentage}% ({FormatFileSize(totalBytesDownloaded)}/{FormatFileSize((long)totalFileSize)})";
                        if (progressPercentage < 100)
                        {
                            Console.WriteLine($"Downloading {name} | {progress}");
                        }
                        else
                        {
                            Console.WriteLine($"Downloading {name} | {progress}");
                            _BepinExInstallDebouncer = new Debouncer(TimeSpan.FromSeconds(.2), () =>
                                {
                                    this.Dispatcher.Invoke(() =>
                                    {
                                        ZipFile.ExtractToDirectory(downloadPath, SelectedFolder, true);
                                        Console.WriteLine("Extracting BepinEx v5.4.19.0 to install directory");

                                        PBar.IsIndeterminate = false;
                                        PBar.Value = 100;

                                        Console.WriteLine("Extracted BepinEx v5.4.19.0 to install directory");
                                        StartDiscordGameSDKDownload();
                                    });
                                }
                            );
                            _BepinExInstallDebouncer.Invoke();
                        }
                    };
                    await client.StartDownload();
                });
            thread.Start();
        }

        private void StartDiscordGameSDKDownload()
        {
            var name = "Discord Game SDK v2.5.6";
            var downloadLink = "https://dl-game-sdk.discordapp.net/2.5.6/discord_game_sdk.zip";
            var downloadPath = $"{Path.GetTempPath()}\\discord_game_sdk_2.5.6.zip";
            var extractionPath = $"{Path.GetTempPath()}\\discord_game_sdk_2.5.6";

            if (!Directory.Exists(extractionPath))
            {
                Directory.CreateDirectory(extractionPath);
            }

            Thread thread = new(async () =>
                {
                    HttpClientDownloadWithProgress client = new(downloadLink, downloadPath);
                    client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                    {
                        var progress = $"{ progressPercentage }% ({ FormatFileSize(totalBytesDownloaded)}/{ FormatFileSize((long)totalFileSize)})";

                        if (progressPercentage < 100)
                        {
                            Console.WriteLine($"Downloading {name} | {progress}");
                        }
                        else
                        {
                            Console.WriteLine($"Downloading {name} | {progress}");

                            _BepinExInstallDebouncer = new Debouncer(TimeSpan.FromSeconds(.2), () =>
                            {
                                this.Dispatcher.Invoke(() =>
                                {
                                    ZipFile.ExtractToDirectory(downloadPath, extractionPath, true);

                                    Console.WriteLine("Extracting discord_game_sdk_2.5.6.zip");
                                    PBar.IsIndeterminate = false;
                                    PBar.Value = 100;
                                    Console.WriteLine("Extracted discord_game_sdk_2.5.6.zip");

                                    File.Copy(
                                        Path.Combine(extractionPath, "lib", "x86_64", "discord_game_sdk.dll"),
                                        Path.Combine(SelectedFolder, "INVAXION_Data", "Plugins", "discord_game_sdk.dll"),
                                        true
                                    );
                                    StartModDownload();
                                });
                            });
                            _BepinExInstallDebouncer.Invoke();
                        }
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

            Thread thread = new(async () =>
            {
                HttpClientDownloadWithProgress client = new(downloadLink, downloadPath);
                client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                {
                    var progress = $"{ progressPercentage }% ({ FormatFileSize(totalBytesDownloaded)}/{ FormatFileSize((long)totalFileSize)})";

                    if (progressPercentage < 100)
                    {
                        Console.WriteLine($"Downloading {name} | {progress}");
                    }
                    else
                    {
                        Console.WriteLine($"Downloading {name} | {progress}");

                        _BepinExInstallDebouncer = new Debouncer(TimeSpan.FromSeconds(.2), () =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                Console.WriteLine("Downloaded ServerEmulator.dll");
                                PBar.IsIndeterminate = false;
                                PBar.Value = 100;
                                DownloadGamePatches();
                            });
                        });
                        _BepinExInstallDebouncer.Invoke();
                    }
                };
                await client.StartDownload();
            });
            thread.Start();
        }

        private void DownloadGamePatches()
        {
            var name = "Game patches v6.0";
            var downloadLink = "https://github.com/Invaxion-Server-Emulator/patches/archive/refs/heads/main.zip";
            var downloadPath = $"{Path.GetTempPath()}\\INVAXION_patches.zip";

            Thread thread = new(async () =>
            {
                HttpClientDownloadWithProgress client = new(downloadLink, downloadPath);
                client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                {
                    var progress = $"{ progressPercentage }% ({ FormatFileSize(totalBytesDownloaded)}/{ FormatFileSize(8200791) })";

                    if (progressPercentage < 100)
                    {
                        Console.WriteLine($"Downloading '{name}' | {progress}");
                    }
                    else
                    {
                        Console.WriteLine($"Downloading '{name}' | {progress}");

                        _BepinExInstallDebouncer = new Debouncer(TimeSpan.FromSeconds(.2), () =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                Console.WriteLine("Downloaded INVAXION_patches.zip");
                                PBar.IsIndeterminate = false;
                                PBar.Value = 100;
                                ZipFile.ExtractToDirectory(downloadPath, SelectedFolder, true);
                                Thread.Sleep(1000);
                                foreach (var item in Directory.EnumerateFiles(Path.Combine(SelectedFolder, "patches-main")))
                                {
                                    Directory.Move(item, SelectedFolder);
                                }
                                CleanUp();
                            });
                        });
                        _BepinExInstallDebouncer.Invoke();
                    }
                };
                await client.StartDownload();
            });
            thread.Start();
        }

        private void CleanUp()
        {
            _StartInstall.IsEnabled = false;
            _StartInstall.Content = "Exit";
            _StartInstall.Name = "ExitBtn";
            ExitBtnEnabled = true;
            _StartInstall.IsEnabled = true;
        }
    }
}
