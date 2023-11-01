using ComputerUtils.Logging;
using Oculus.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using ComputerUtils.ConsoleUi;
using OculusGraphQLApiLib.Game;
using OculusGraphQLApiLib;

namespace RIFT_Downgrader
{
    public class PackageMeta
    {
        public string packageName { get; set; } = "";
        public string packageVersion { get; set; } = "";
        public string packageAuthor { get; set; } = "";
        public List<PackageAction> actions { get; set; } = new List<PackageAction>();
    }

    public class PackageAction
    {
        public List<PackageOn> on { get; set; } = new List<PackageOn>();
        public string actionType { get; set; } = "";
        public string actionName { get; set; } = "";

        // File copy
        public string fileInZip { get; set; } = null;
        public string fileDestination { get; set; } = null; // File download, File copy

        // File delete
        public string file { get; set; } = null;

        // File download
        public string downloadUri { get; set; } = null;

        // Directory copy
        public string directoryInZip { get; set; } = null;
        public string directoryDestination { get; set; } = null;

        // Change launch args
        public string newLaunchArgs { get; set; } = null;

        public ActionType GetActionType()
        {
            return (ActionType)Enum.Parse(typeof(ActionType), actionType);
        }

        public void MakeFilenamesSafe()
        {
            if(file != null) file = MakeFilenameSafe(file);
            if(directoryDestination != null) directoryDestination = MakeFilenameSafe(directoryDestination);
            if(fileDestination != null) fileDestination = MakeFilenameSafe(fileDestination);
        }

        public string MakeFilenameSafe(string filename)
        {
            return filename.Replace(".." + Path.DirectorySeparatorChar, "");
        }

        public void ExecuteAction(Package package)
        {
            Logger.Log("Starting " + actionName);
            Console.WriteLine("Starting " + actionName);
            foreach(PackageOn o in on)
            {
                App app = null;
                if ((app = DowngradeManager.config.apps.FirstOrDefault(x => (x.canonicalName == o.canonicalName || x.id == o.appId) && x.headset == Headset.RIFT)) != null)
                {
                    ReleaseChannelReleaseBinary binary = null;
                    if((binary = app.versions.FirstOrDefault(x => o.versions.Contains(x.version) || o.versionCodes.Contains(x.version_code))) != null) // Check against database
                    {
                        Manifest m = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(DowngradeManager.config.oculusSoftwareFolder + Path.DirectorySeparatorChar + "Manifests"+ Path.DirectorySeparatorChar + app.canonicalName + ".json"));
                        if(!o.versionCodes.Contains(m.versionCode) && !o.versions.Contains(m.version)) break; // Check installed
                        string dir = DowngradeManager.config.oculusSoftwareFolder + Path.DirectorySeparatorChar + "Software" + Path.DirectorySeparatorChar + app.canonicalName + Path.DirectorySeparatorChar;
                        MakeFilenamesSafe();
                        switch (GetActionType())
                        {
                            case ActionType.FILECOPY:
                                Logger.Log("Starting FileCopy from " + fileInZip + " to " + dir + fileDestination);
                                Console.WriteLine("Starting FileCopy from " + fileInZip + " to " + fileDestination);
                                if (fileInZip == null)
                                {
                                    Logger.Log("Missing fileInZip value in package", LoggingType.Warning);
                                    Console.WriteLine("No file to copy specified");
                                    return;
                                }
                                if (fileDestination == null)
                                {
                                    Logger.Log("Missing fileDestination value in package", LoggingType.Warning);
                                    Console.WriteLine("No fileDestination specified");
                                    return;
                                }
                                try
                                {
                                    File.WriteAllBytes(dir + fileDestination, package.GetFile(fileInZip));
                                } catch
                                {
                                    Logger.Log("Copying file failed. Does it exist in the package?");
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("Copying file failed. Does it exist in the package?");
                                    Console.ForegroundColor = ConsoleColor.White;
                                    return;
                                }
                                
                                break;
                            case ActionType.DIRECTORYCOPY:
                                Logger.Log("Starting DirectoryCopy from " + directoryInZip + " to " + dir + directoryDestination);
                                Console.WriteLine("Starting DirectoryCopy from " + directoryInZip + " to " + directoryDestination);
                                if (directoryInZip == null)
                                {
                                    Logger.Log("Missing directoryInZip value in package", LoggingType.Warning);
                                    Console.WriteLine("No directory to copy specified");
                                    return;
                                }
                                if (directoryDestination == null)
                                {
                                    Logger.Log("Missing directoryDestination value in package", LoggingType.Warning);
                                    Console.WriteLine("No directoryDestination specified");
                                    return;
                                }
                                try
                                {
                                    if (!directoryDestination.EndsWith(Path.DirectorySeparatorChar) && directoryDestination != "") directoryDestination += Path.DirectorySeparatorChar;
                                    if (!directoryInZip.EndsWith("/")) directoryInZip += "/";
                                    Directory.CreateDirectory(dir + directoryDestination);

                                    foreach(string f in package.files)
                                    {
                                        if(f.StartsWith(directoryInZip) && !f.EndsWith("/"))
                                        {
                                            string dest = dir + directoryDestination + f.Substring(directoryInZip.Length).Replace('/', Path.DirectorySeparatorChar);
                                            Logger.Log("Copying " + f + " to " + dest);
                                            Console.WriteLine("Copying " + f + " to " + dest);
                                            if (!Directory.Exists(dest.Substring(0, dest.LastIndexOf(Path.DirectorySeparatorChar)))) Directory.CreateDirectory(dest.Substring(0, dest.LastIndexOf(Path.DirectorySeparatorChar)));
                                            if(package.DoesFileExist(f)) File.WriteAllBytes(dest, package.GetFile(f));
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Logger.Log("Copying directory failed. Does it exist in the package?\n" + e.ToString(), LoggingType.Warning);
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("Copying directory failed. Does it exist in the package?");
                                    Console.ForegroundColor = ConsoleColor.White;
                                    return;
                                }

                                break;
                            case ActionType.FILEDELETE:
                                Logger.Log("Deleting " + dir + file);
                                Console.WriteLine("Deleting " + file);
                                if(file == null)
                                {
                                    Logger.Log("Missing file value in package", LoggingType.Warning);
                                    Console.WriteLine("No file to delete specified");
                                    return;
                                }
                                if(File.Exists(dir + file)) File.Delete(dir + file);
                                Logger.Log("Deleted");
                                Console.WriteLine("Deleted");
                                break;
                            case ActionType.FILEDOWNLOAD:
                                Logger.Log("Downloading from " + downloadUri + " to " + dir + fileDestination);
                                Console.WriteLine("Downloading from " + downloadUri + " to " + fileDestination);
                                DownloadProgressUI d = new DownloadProgressUI();
                                d.StartDownload(downloadUri, dir + fileDestination, true, true, new Dictionary<string, string> { { "User-Agent", "Rift-Downgrader/" + DowngradeManager.updater.version } });
                                break;
                            case ActionType.CHANGELAUNCHARGS:
                                Logger.Log("Changing launch args to " + newLaunchArgs);
                                Console.WriteLine("Changing launch args to " + newLaunchArgs);
                                if (newLaunchArgs == null)
                                {
                                    Logger.Log("Missing newLaunchArgs", LoggingType.Warning);
                                    Console.WriteLine("No launch arguments specified");
                                    return;
                                }
                                DowngradeManager.config.apps.FirstOrDefault(x => (x.canonicalName == o.canonicalName || x.id == o.appId) && x.headset == Headset.RIFT).versions.FirstOrDefault(x => o.versions.Contains(x.version) || o.versionCodes.Contains(x.version_code)).extraLaunchArgs += newLaunchArgs + " ";
                                DowngradeManager.config.Save();
                                break;
                            default:
                                Logger.Log("Unknown action type", LoggingType.Warning);
                                Console.WriteLine("Unknown action type");
                                return;
                        }
                        Logger.Log("Action completed");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Action done");
                        Console.ForegroundColor = ConsoleColor.White;
                        return;
                    }
                }
            }
            Logger.Log("Action does not match any installed app. skipping");
            Console.WriteLine("Action does not match any installed app. skipping");
        }
    }

    public enum ActionType
    {
        FILECOPY,
        FILEDELETE,
        FILEDOWNLOAD,
        DIRECTORYCOPY,
        CHANGELAUNCHARGS
    }

    public class PackageOn
    {
        public List<string> versions { get; set; } = new List<string>(); // Any of versionCodes or versions
        public List<long> versionCodes { get; set; } = new List<long>(); // Any of versionCodes or versions
        public string canonicalName { get; set; } = ""; // Any of appId or canonicalName
        public string appId { get; set; } = ""; // Any of appId or canonicalName
    }

    public class Package
    {
        public PackageMeta metadata { get; set; } = new PackageMeta();
        public List<string> files { get; set; } = new List<string>();
        public string path { get; set; } = "";

        public void Execute()
        {
            Logger.Log("executing " + metadata.packageName + " " + metadata.packageVersion + " by " + metadata.packageAuthor);
            foreach (PackageAction a in metadata.actions)
            {
                a.ExecuteAction(this);
                Console.WriteLine();
            }
        }

        public static Package LoadPackage(string path)
        {
            Logger.Log("Loading package " + path);
            ZipArchive archive = ZipFile.OpenRead(path);
            Package p = new Package();
            p.path = path;
            if(archive.Entries.FirstOrDefault(x => x.FullName == "manifest.json") == null)
            {
                
                return null;
            }
            p.metadata = JsonSerializer.Deserialize<PackageMeta>(new StreamReader(archive.GetEntry("manifest.json").Open()).ReadToEnd());
            foreach(ZipArchiveEntry e in archive.Entries)
            {
                p.files.Add(e.FullName);
            }
            archive.Dispose();
            return p;
        }

        public bool DoesFileExist(string name)
        {
            return ZipFile.OpenRead(path).GetEntry(name) != null;
        }

        public byte[] GetFile(string name)
        {
            ZipArchiveEntry e = ZipFile.OpenRead(path).GetEntry(name);
            byte[] b = new byte[e.Length];
            Stream s = e.Open();
            s.Read(b, 0, (int)e.Length);
            s.Close();
            return b;
        }
    }
}