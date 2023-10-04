using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Net;
using System.Linq;

namespace Ironmelt
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Encoding ANSI = Encoding.GetEncoding("iso-8859-2");
        Encoding UTF8 = new System.Text.UTF8Encoding(false);

        List<Game> games = new List<Game>();
        CEParser.File file;
        List<IronmeltError> errors = new List<IronmeltError>();
        bool autoUpdate = false;

        public MainWindow()
        {
            InitializeComponent();
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                TextPath.Text = Properties.Settings.Default.SavegamePath;
                CheckAppendSuffix.IsChecked = Properties.Settings.Default.AppendSuffix;
                CheckCopyCloudToLocal.IsChecked = Properties.Settings.Default.CopyCloudLocal;
                CheckCleanUp.IsChecked = Properties.Settings.Default.CleanUp;
                CheckCompressBack.IsChecked = Properties.Settings.Default.CompressBack;
                MenuCheckForUpdatesAutomatically.IsChecked = Properties.Settings.Default.CheckForUpdatesOnStartup;
            }
            catch
            {
                MessageBox.Show("Settings reverted to default values.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            try
            {
                if (Properties.Settings.Default.CheckForUpdatesOnStartup)
                    CheckForUpdates(true);
            }
            catch
            {
                MessageBox.Show("Checking for updates failed.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            LocateFolders();

            // Command line support
            if (Environment.GetCommandLineArgs().Length > 1)
            {
                var path = Environment.GetCommandLineArgs()[1];
                if (!File.Exists(path)) return;
                //ConsoleManager.Show();
                bool auto = await Process(path, true);
                if (auto)
                {
                    Application.Current.MainWindow.Close();
                }
            }
        }

        private void CheckForUpdates(bool auto)
        {
            autoUpdate = auto;
            WebClient w = new WebClient();
            w.DownloadStringCompleted += W_DownloadStringCompleted;
            w.DownloadStringAsync(new Uri("https://codeofwar.wbudziszewski.pl/ironmelt/"), auto);
        }

        private void W_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                int location = e.Result.IndexOf("<a href", e.Result.IndexOf(@"<section class=""latest-post-selection"""));
                string src = e.Result.Substring(location, e.Result.IndexOf("</a>", location + 5) - location);
                // Get link
                string link = src.Substring(src.IndexOf(@"href=""") + 6);
                link = link.Remove(link.IndexOf(" ") - 1);

                // Get version
                string ver = src.Substring(src.IndexOf(@"<span") + 5);
                ver = ver.Remove(ver.IndexOf("<"));
                ver = ver.Substring(ver.LastIndexOf(" ") + 1).Trim();

                // Compare version
                var verstr = ver.Split('.');
                int[] latestVersion = new int[4];
                for (int i = 0; i < Math.Min(latestVersion.Length, verstr.Length); i++)
                {
                    if (Int32.TryParse(verstr[i], out int o))
                        latestVersion[i] = o;
                }

                Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                if (autoUpdate)
                {
                    if (currentVersion.Major > latestVersion[0]) { return; }
                    else if (currentVersion.Major < latestVersion[0]) { PerformUpdate(ver, link); }
                    else if (currentVersion.Minor > latestVersion[1]) { return; }
                    else if (currentVersion.Minor < latestVersion[1]) { PerformUpdate(ver, link); }
                    else if (currentVersion.Build > latestVersion[2]) { return; }
                    else if (currentVersion.Build < latestVersion[2]) { PerformUpdate(ver, link); }
                    else if (currentVersion.Revision >= latestVersion[3]) { return; }
                    else { PerformUpdate(ver, link); }
                }
                else
                {
                    if (currentVersion.Major > latestVersion[0]) { MessageBox.Show("No new version found.", "No updates", MessageBoxButton.OK, MessageBoxImage.Information); }
                    else if (currentVersion.Major < latestVersion[0]) { PerformUpdate(ver, link); }
                    else if (currentVersion.Minor > latestVersion[1]) { MessageBox.Show("No new version found.", "No updates", MessageBoxButton.OK, MessageBoxImage.Information); }
                    else if (currentVersion.Minor < latestVersion[1]) { PerformUpdate(ver, link); }
                    else if (currentVersion.Build > latestVersion[2]) { MessageBox.Show("No new version found.", "No updates", MessageBoxButton.OK, MessageBoxImage.Information); }
                    else if (currentVersion.Build < latestVersion[2]) { PerformUpdate(ver, link); }
                    else if (currentVersion.Revision >= latestVersion[3]) { MessageBox.Show("No new version found.", "No updates", MessageBoxButton.OK, MessageBoxImage.Information); }
                    else { PerformUpdate(ver, link); }
                }

            }
            catch (Exception ex)
            {
                if (!autoUpdate)
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PerformUpdate(string version, string link)
        {
            if (MessageBox.Show("A new version of Ironmelt: " + version + " is available. Do you want to go the update webpage?", "Update available", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(link);
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            Properties.Settings.Default.SavegamePath = TextPath.Text;
            Properties.Settings.Default.AppendSuffix = CheckAppendSuffix.IsChecked == true;
            Properties.Settings.Default.CopyCloudLocal = CheckCopyCloudToLocal.IsChecked == true;
            Properties.Settings.Default.CleanUp = CheckCleanUp.IsChecked == true;
            Properties.Settings.Default.CheckForUpdatesOnStartup = MenuCheckForUpdatesAutomatically.IsChecked == true;
            Properties.Settings.Default.CompressBack = CheckCompressBack.IsChecked == true;
            Properties.Settings.Default.Save();
        }

        private void InitializeGames(string steamPath, string steamID)
        {
            games.Add(new Game(
                "Crusader Kings II", "ck2", "ck2bin.csv",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Paradox Interactive\\Crusader Kings II\\save games"),
                Path.Combine(steamPath, "userdata\\" + steamID + "\\203770\\remote\\save games"),
                ANSI));
            games.Add(new Game(
                "Europa Universalis IV", "eu4", "eu4bin.csv",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Paradox Interactive\\Europa Universalis IV\\save games"),
                Path.Combine(steamPath, "userdata\\" + steamID + "\\236850\\remote\\save games"),
                ANSI));
            games.Add(new Game(
                "Hearts of Iron IV", "hoi4", "hoi4bin.csv",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Paradox Interactive\\Hearts of Iron IV\\save games"),
                Path.Combine(steamPath, "userdata\\" + steamID + "\\394360\\remote\\save games"),
                UTF8));

            mDocumentsHoI4.Visibility = Directory.Exists(games[2].LocalFolderPath) ? Visibility.Visible : Visibility.Collapsed;
            mSteamHoI4.Visibility = Directory.Exists(games[2].CloudFolderPath) ? Visibility.Visible : Visibility.Collapsed;
            mDocumentsEU4.Visibility = Directory.Exists(games[1].LocalFolderPath) ? Visibility.Visible : Visibility.Collapsed;
            mSteamEU4.Visibility = Directory.Exists(games[1].CloudFolderPath) ? Visibility.Visible : Visibility.Collapsed;
            mDocumentsCK2.Visibility = Directory.Exists(games[0].LocalFolderPath) ? Visibility.Visible : Visibility.Collapsed;
            mSteamCK2.Visibility = Directory.Exists(games[0].CloudFolderPath) ? Visibility.Visible : Visibility.Collapsed;

            separatorHoI4.Visibility = mDocumentsHoI4.Visibility == Visibility.Visible || mSteamHoI4.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
            separatorEU4.Visibility = mDocumentsEU4.Visibility == Visibility.Visible || mSteamEU4.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LocateFolders()
        {
            string steamPath = "";
            string steamID = "";

            // Steam path
            try
            {
                RegistryKey key = Registry.CurrentUser;
                key = key.OpenSubKey("Software");
                key = key.OpenSubKey("Valve");
                key = key.OpenSubKey("Steam");
                steamPath = key.GetValue("SteamPath", "").ToString().Replace('/', '\\');

                //key = key.OpenSubKey("Users");
                //steamID = key.GetSubKeyNames()[0];

                // Add Steam ID
                var steamUserFolder = Directory.GetDirectories(Path.Combine(steamPath, "userdata")).FirstOrDefault();
                steamID = Path.GetFileName(steamUserFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error identifying Steam data (Steam is possibly not installed properly): <" + ex.Message + ">.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                InitializeGames(steamPath, steamID);
            }
        }

        private async void ButtonDecode_Click(object sender, RoutedEventArgs e)
        {
            await Process(TextPath.Text, false);
        }

        private async Task<bool> Process(string path, bool suppressWindows)
        {
            if (!File.Exists(path))
            {
                if (suppressWindows)
                    Console.WriteLine("File error: Specified file does not exist.");
                else
                    MessageBox.Show("Specified file does not exist.", "File error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Decide game type
            string ext = Path.GetExtension(path).ToLowerInvariant();
            Game currentGame = games.Find(x => ext.EndsWith(x.Ext));
            if (currentGame.Ext == null)
            {
                if (suppressWindows)
                    Console.WriteLine("File error: The program could not decide which game the savegame file originates from - unknown file extension.");
                else
                    MessageBox.Show("The program could not decide which game the savegame file originates from - unknown file extension.", "File error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            string outputPath;
            if (CheckCopyCloudToLocal.IsChecked == true)
            {
                outputPath = path;
                var outputPathCheck = outputPath;
                while (Path.GetDirectoryName(outputPathCheck) != Path.GetPathRoot(outputPathCheck))
                {
                    if (outputPathCheck == currentGame.LocalFolderPath)
                    {
                        outputPath = path;
                        break;
                    }
                    outputPathCheck = Path.GetDirectoryName(outputPathCheck);
                }
                outputPath = Path.Combine(currentGame.LocalFolderPath, Path.GetFileName(path));
            }
            else
            {
                outputPath = path;
            }

            if (File.Exists(outputPath))
            {
                if (CheckAppendSuffix.IsChecked == true)
                {
                    string filename = Path.GetFileNameWithoutExtension(outputPath);
                    while (File.Exists(Path.Combine(Path.GetDirectoryName(outputPath), filename + Path.GetExtension(outputPath))))
                    {
                        if (filename.Contains("_"))
                        {
                            string sindex = filename.Substring(filename.LastIndexOf("_") + 1);
                            if (Int32.TryParse(sindex, out int index))
                            {
                                index++;
                                filename = filename.Remove(filename.LastIndexOf("_") + 1);
                                filename += index;
                            }
                            else
                            {
                                filename += "_1";
                            }
                        }
                        else
                        {
                            filename += "_1";
                        }
                    }
                    outputPath = Path.Combine(Path.GetDirectoryName(outputPath), filename + Path.GetExtension(outputPath));
                }
                if (File.Exists(outputPath))
                {
                    if (!suppressWindows && MessageBox.Show("The file with a given suffix already exists in the destination: <" + outputPath + ". Do you want to overwrite this file?", "Overwrite warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        return false;
                }
            }

            try
            {
                IsEnabled = false;
                ListErrors.ItemsSource = null;
                await Decode(TextPath.Text, outputPath, currentGame, CheckCompressBack.IsChecked == true, true, currentGame.Ext != "hoi4", currentGame.Encoding, suppressWindows);
                LabelProgress.Content = "Processing complete.";
                errors.Sort();
                errors.Reverse();
                ListErrors.ItemsSource = errors;
                IsEnabled = true;
                return true;
            }
            catch (Exception ex)
            {
                if (suppressWindows)
                    Console.WriteLine("Error: The program encountered the following error and could not complete the task: " + ex.Message);
                else
                    MessageBox.Show("The program encountered the following error and could not complete the task: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                IsEnabled = true;
                GC.Collect();
            }
        }

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            try
            {
                if (Directory.Exists(Path.GetDirectoryName(TextPath.Text)))
                {
                    openFileDialog.InitialDirectory = Path.GetDirectoryName(TextPath.Text);
                }
            }
            catch
            {
                openFileDialog.InitialDirectory = null;
            }
            if (openFileDialog.ShowDialog() == true)
                TextPath.Text = openFileDialog.FileName;
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuHelp_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("Readme.txt");
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            string text = @"Ironmelt " + Assembly.GetExecutingAssembly().GetName().Version.ToString() + @"

Witold Budziszewski

Credits: Binary files decoding routines based on the work of PreXident (Java Save Game Replayer)
";

            text += "\nSupported games:\n";
            foreach (var game in games)
            {
                text += "- " + game.Name + ", tokens: " + game.BinaryTokens.Length + "\n";
            }

            MessageBox.Show(text, "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TextPath_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] data = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (data == null || data.Length < 1) return;
                string text = data[0].Substring(0, (int)Math.Min(data[0].Length, 200));
                TextPath.Text = text;
            }
        }

        private void TextPath_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void SetSeverity()
        {
            foreach (var e in errors)
            {
                if (e.Count < 10) e.Severity = Severity.Low;
                else if (e.Count < 100) e.Severity = Severity.Medium;
                else e.Severity = Severity.High;
            }
        }

        bool IsFileInUse(FileInfo file)
        {
            FileStream stream = null;
            if (!file.Exists) return false;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }
            return false;
        }

        private void File_FileParseProgress(object source, CEParser.FileParseEventArgs e)
        {
            LabelProgress.Dispatcher.Invoke(new UpdateTextCallback(this.UpdateText), new object[] { LabelProgress, "Decoding..." });
            ProgressBar.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { ProgressBar, e.Progress });
        }

        public delegate void UpdateTextCallback(Label control, string message);
        private void UpdateText(Label control, string message)
        {
            control.Content = message;
        }

        public delegate void UpdateProgressCallback(ProgressBar control, double progress);
        private void UpdateProgress(ProgressBar control, double progress)
        {
            control.Value = progress;
        }

        public delegate void AppendTextCallback(TextBox control, string text);
        private void AppendText(TextBox control, string text)
        {
            control.AppendText(text);
        }

        private bool IsCompressedSavegame(string path)
        {
            using (BinaryReader br = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                byte[] code = br.ReadBytes(2);
                return (code[0] == 80 && code[1] == 75);
            }
        }

        private bool IsBinarySavegame(Stream stream, Encoding encoding)
        {
            byte[] raw = new byte[7];
            stream.Seek(0, SeekOrigin.Begin);
            stream.Read(raw, 0, 7);
            stream.Seek(0, SeekOrigin.Begin);
            string prefix = encoding.GetString(raw, 0, 7);

            if (prefix.ToLowerInvariant().StartsWith("ck2bin")) return true;
            if (prefix.ToLowerInvariant().StartsWith("eu4bin")) return true;
            if (prefix.ToLowerInvariant().StartsWith("hoi4bin")) return true;

            return false;
        }

        private void MenuDocumentsEU4_Click(object sender, RoutedEventArgs e)
        {
            Game game = games.Find(x => x.Ext == "eu4");
            if (Directory.Exists(game.LocalFolderPath))
                System.Diagnostics.Process.Start(game.LocalFolderPath);
            else
                MessageBox.Show("The local savegame folder cannot be located at the path: <" + game.LocalFolderPath + ">.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void MenuSteamEU4_Click(object sender, RoutedEventArgs e)
        {
            Game game = games.Find(x => x.Ext == "eu4");
            if (Directory.Exists(game.CloudFolderPath))
                System.Diagnostics.Process.Start(game.CloudFolderPath);
            else
                MessageBox.Show("The local savegame folder cannot be located at the path: <" + game.CloudFolderPath + ">.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void MenuDocumentsCK2_Click(object sender, RoutedEventArgs e)
        {
            Game game = games.Find(x => x.Ext == "ck2");
            if (Directory.Exists(game.LocalFolderPath))
                System.Diagnostics.Process.Start(game.LocalFolderPath);
            else
                MessageBox.Show("The local savegame folder cannot be located at the path: <" + game.LocalFolderPath + ">.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void MenuSteamCK2_Click(object sender, RoutedEventArgs e)
        {
            Game game = games.Find(x => x.Ext == "ck2");
            if (Directory.Exists(game.CloudFolderPath))
                System.Diagnostics.Process.Start(game.CloudFolderPath);
            else
                MessageBox.Show("The local savegame folder cannot be located at the path: <" + game.CloudFolderPath + ">.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void MenuDocumentsHoI4_Click(object sender, RoutedEventArgs e)
        {
            Game game = games.Find(x => x.Ext == "hoi4");
            if (Directory.Exists(game.LocalFolderPath))
                System.Diagnostics.Process.Start(game.LocalFolderPath);
            else
                MessageBox.Show("The local savegame folder cannot be located at the path: <" + game.LocalFolderPath + ">.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void MenuSteamHoI4_Click(object sender, RoutedEventArgs e)
        {
            Game game = games.Find(x => x.Ext == "hoi4");
            if (Directory.Exists(game.CloudFolderPath))
                System.Diagnostics.Process.Start(game.CloudFolderPath);
            else
                MessageBox.Show("The local savegame folder cannot be located at the path: <" + game.CloudFolderPath + ">.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void MenuCheckForUpdatesAutomatically_Click(object sender, RoutedEventArgs e)
        {
            MenuCheckForUpdatesAutomatically.IsChecked = !MenuCheckForUpdatesAutomatically.IsChecked;
        }

        private void MenuCheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            CheckForUpdates(false);
        }
    }
}
