using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ironmelt
{
    public partial class MainWindow : Window
    {
        private async Task Decode(string inputPath, string outputPath, Game currentGame, bool enforceCompression, bool reportTime, bool enforceUnixLE, Encoding encoding, bool suppressWindows)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            string output = "";

            MemoryStream stream;
            ZipArchiveEntry rnw = null;

            LabelProgress.Content = "Initializing...";

            if (IsCompressedSavegame(inputPath))
            {
                LabelProgress.Content = "Uncompressing savegame...";
                stream = (MemoryStream)await UnpackToStream(inputPath);
                using (System.IO.Compression.ZipArchive zip = new System.IO.Compression.ZipArchive(System.IO.File.Open(inputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), System.IO.Compression.ZipArchiveMode.Read))
                {
                    rnw = zip.Entries.FirstOrDefault(x => x.Name == "rnw.zip");
                }
            }
            else
            {
                using (var fs = File.Open(inputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    stream = new MemoryStream(4096);
                    fs.CopyTo(stream);
                }
            }

            if (stream == null) throw new Exception("Error unpacking savegame file or the file is not a supported binary savegame file.");

            if (!IsBinarySavegame(stream, currentGame.Encoding))
            {
                throw new Exception("The specified file is not a supported binary savegame file.");
            }

            LabelProgress.Content = "Decoding...";
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            file = new CEParser.BinaryFile(stream, currentGame.Ext, currentGame.BinaryTokens, encoding);
            if (file is CEParser.BinaryFile && (currentGame.Ext == "eu4" || currentGame.Ext == "hoi4")) (file as CEParser.BinaryFile).Decoder.EnforceDateDatatype = true;
            file.FileParseProgress += File_FileParseProgress;
            await file.ParseAsync();

            // Clean-up attempt
            if (CheckCleanUp.IsChecked == true)
            {
                file.CleanUp("UNKNOWN_*", true);
            }

            LabelProgress.Content = "Exporting...";
            output = file.Export();

            if (enforceUnixLE) output = output.Replace("\r\n", "\n"); // currently, for all the games Unix Lf line endings are used

            // VERIFICATION
            LabelProgress.Content = "Verifying...";

            errors.Clear();
            for (int i = 0; i < file.Errors.Count; i++)
            {
                if (file.Errors[i].Error == "Unknown token")
                {
                    int index = errors.FindIndex(x => x.Error == file.Errors[i].Error && x.Details == file.Errors[i].Details);
                    if (index >= 0)
                    {
                        errors[index].Count++;
                    }
                    else
                    {
                        errors.Add(new IronmeltError(file.Errors[i].Error, 1, file.Errors[i].Details, Severity.Unknown));
                    }
                }
            }

            SetSeverity();

            // WRITING TO DISK
            List<int> removedPos = new List<int>();
            List<ushort> removedLen = new List<ushort>();
            try
            {
                LabelProgress.Dispatcher.Invoke(new UpdateTextCallback(this.UpdateText), new object[] { LabelProgress, "Writing to file..." });

                if (IsFileInUse(new FileInfo(outputPath)))
                    outputPath = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileNameWithoutExtension(outputPath) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + Path.GetExtension(outputPath));

                using (StreamWriter tw = new StreamWriter(outputPath, false, encoding, 4096))
                {
                    char[] outputArray = output.ToCharArray();
                    // change bin to txt
                    tw.Write(outputArray, 0, currentGame.Ext.Length);
                    tw.Write("txt");

                    if (currentGame.Ext == "hoi4")
                    {
                    }
                    if (currentGame.Ext == "eu4")
                    {
                        if (output.IndexOf("local_ironman=yes") > -1)
                        {
                            removedPos.Add(output.IndexOf("local_ironman=yes"));
                            removedLen.Add((ushort)(("local_ironman=yes".Length - 1) + (outputArray[6] == '\r' ? 2 : 1)));
                        }
                    }
                    else if (currentGame.Ext == "ck2")
                    {
                        // remove ironman attributes file_name, multiplayer_random_seed, multiplayer_random_count
                        int fileName = output.IndexOf("file_name");
                        int playerRealm = output.IndexOf("player_realm");
                        if (fileName > -1 && playerRealm > -1)
                        {
                            removedPos.Insert(0, fileName);
                            removedLen.Insert(0, (ushort)(playerRealm - fileName - 1));
                        }

                        // remove achievement container
                        int removeAchievements = output.IndexOf("achievement={", Math.Max(output.Length - 1000, 0));
                        if (removeAchievements > 0)
                        {
                            int removeAchievementsEnd = output.IndexOf("}", removeAchievements);
                            removedPos.Add(removeAchievements);
                            removedLen.Add((ushort)(removeAchievementsEnd - removeAchievements));
                        }
                    }

                    int pos = currentGame.Ext.Length + 3;

                    for (int i = 0; i < removedPos.Count; i++)
                    {
                        if (removedPos[i] - pos < 1)
                        {
                        }
                        else
                        {
                            tw.Write(outputArray, pos, removedPos[i] - pos);
                        }
                        pos = removedPos[i] + removedLen[i] + 1;
                    }
                    if (pos < output.Length)
                        tw.Write(outputArray, pos, output.Length - pos);

                    output = null;
                    outputArray = null;

                }

                // Random New World or zipped output
                if (rnw != null || enforceCompression)
                {
                    if (rnw != null)
                    {
                        if (File.Exists(Path.Combine(Path.GetDirectoryName(outputPath), "rnw.zip")))
                        {
                            if (!suppressWindows && MessageBox.Show("There is an existing 'rnw.zip' file in the output location. Do you want to overwrite it and continue the decoding process?", "Overwrite", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                                return;
                        }
                        using (BinaryWriter writer = new BinaryWriter(System.IO.File.Open(Path.Combine(Path.GetDirectoryName(outputPath), "rnw.zip"), FileMode.Create, FileAccess.Write, FileShare.None)))
                        {
                            using (BinaryReader reader = new BinaryReader(rnw.Open()))
                            {
                                byte[] buffer = new byte[4096];
                                int count;
                                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                                    writer.Write(buffer, 0, count);
                            }
                        }
                    }

                    string zipTempPath = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileNameWithoutExtension(outputPath) + ".zip");
                    using (System.IO.Compression.ZipArchive zip = new System.IO.Compression.ZipArchive(System.IO.File.Open(zipTempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None), System.IO.Compression.ZipArchiveMode.Update))
                    {
                        ZipArchiveEntry entry = zip.CreateEntry(Path.GetFileName(outputPath));
                        using (BinaryWriter writer = new BinaryWriter(entry.Open()))
                        {
                            using (BinaryReader reader = new BinaryReader(System.IO.File.Open(outputPath, FileMode.Open, FileAccess.Read, FileShare.None)))
                            {
                                byte[] buffer = new byte[4096];
                                int count;
                                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                                    writer.Write(buffer, 0, count);
                            }
                        }
                        if (rnw != null)
                        {
                            entry = zip.CreateEntry("rnw.zip");
                            using (BinaryWriter writer = new BinaryWriter(entry.Open()))
                            {
                                using (BinaryReader reader = new BinaryReader(System.IO.File.Open(Path.Combine(Path.GetDirectoryName(outputPath), "rnw.zip"), FileMode.Open, FileAccess.Read, FileShare.None)))
                                {
                                    byte[] buffer = new byte[4096];
                                    int count;
                                    while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                                        writer.Write(buffer, 0, count);
                                }
                            }
                        }
                    }

                    File.Delete(outputPath);
                    File.Delete(Path.Combine(Path.GetDirectoryName(outputPath), "rnw.zip"));

                    // Try to rename the zip file
                    int failsafe = 10;
                    while (true)
                    {
                        try
                        {
                            File.Move(zipTempPath, outputPath);
                            break; // success!
                        }
                        catch
                        {
                            if (--failsafe == 0)
                            {
                                if (suppressWindows)
                                    Console.WriteLine("Warning: The decoding was successful but the resulting compressed file: " + zipTempPath + " is locked and cannot be renamed. You can try to rename it manually to " + outputPath + ".");
                                else
                                    MessageBox.Show("The decoding was successful but the resulting compressed file: " + zipTempPath + " is locked and cannot be renamed. You can try to rename it manually to " + outputPath + ".", "File locked", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            else Thread.Sleep(100);
                        }
                    }
                }

                sw.Stop();

                if (reportTime)
                {
                    if (suppressWindows)
                        Console.WriteLine("Decoding done in " + sw.ElapsedMilliseconds / 1000f + " s.\n\nThe decoded file has been saved as: <" + outputPath + ">." +
                        ((rnw != null && !enforceCompression) ? "\n\nBecause the saved game uses Random New World, the converted file was saved as a compressed file." : ""));
                    else
                        MessageBox.Show("Decoding done in " + sw.ElapsedMilliseconds / 1000f + " s.\n\nThe decoded file has been saved as: <" + outputPath + ">." +
                        ((rnw != null && !enforceCompression) ? "\n\nBecause the saved game uses Random New World, the converted file was saved as a compressed file." : ""));
                }
            }
            catch (IOException)
            {
                if (suppressWindows)
                    Console.WriteLine("Error: File at the output location could not be written to. Check if it is not already open in another program.");
                else
                    MessageBox.Show("File at the output location could not be written to. Check if it is not already open in another program.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception)
            {
                if (suppressWindows)
                    Console.WriteLine("Error: Unidentifier error.");
                else
                    MessageBox.Show("Unidentifier error.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        async Task<Stream> UnpackToStream(string path)
        {
            MemoryStream ms = new MemoryStream();
            await Task.Run(() =>
            {
                try
                {
                    // Unzip the save file
                    using (System.IO.Compression.ZipArchive zip = new System.IO.Compression.ZipArchive(System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), System.IO.Compression.ZipArchiveMode.Read))
                    {
                        // Check if zip archive consists of 'gamestate' and 'ai' entries
                        if (zip.Entries.Count(x => x.Name == "gamestate") > 0 && zip.Entries.Count(x => x.Name == "ai") > 0 && zip.Entries.Count(x => x.Name == "meta") > 0)
                        {
                            using (BinaryReader reader = new BinaryReader(zip.Entries.First(x => x.Name == "meta").Open()))
                            {
                                byte[] buffer = new byte[4096];
                                int count;
                                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                                    ms.Write(buffer, 0, count);
                                ms.SetLength(ms.Length - 40); // remove checksum
                            }
                            using (BinaryReader reader = new BinaryReader(zip.Entries.First(x=>x.Name=="gamestate").Open()))
                            {
                                byte[] buffer = new byte[4096];
                                int count;
                                reader.Read(buffer, 0, 6); // skip EU4bin
                                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                                    ms.Write(buffer, 0, count);
                                ms.SetLength(ms.Length - 40); // remove checksum
                            }
                            using (BinaryReader reader = new BinaryReader(zip.Entries.First(x => x.Name == "ai").Open()))
                            {
                                byte[] buffer = new byte[4096];
                                int count;
                                reader.Read(buffer, 0, 6); // skip EU4bin
                                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                                    ms.Write(buffer, 0, count);
                                ms.SetLength(ms.Length - 40); // remove checksum
                            }
                        }
                        else
                        {
                            using (BinaryReader reader = new BinaryReader(zip.Entries[0].Open()))
                            {
                                byte[] buffer = new byte[4096];
                                int count;
                                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                                    ms.Write(buffer, 0, count);
                                ms.SetLength(ms.Length - 40); // remove checksum
                            }
                        }
                    }
                }
                catch { }
                finally
                {
                    ms.Position = 0;
                }
            });
            return ms;
        }
    }
}
