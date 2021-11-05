using ComputerUtils.GraphQL;
using ComputerUtils.Logging;
using Oculus.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

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
        public string fileInZip { get; set; } = "";
        public string fileDestination { get; set; } = "";

        // File delete
        public string file { get; set; } = "";

        public ActionType GetActionType()
        {
            return (ActionType)Enum.Parse(typeof(ActionType), actionType);
        }

        public void ExecuteAction(Package package)
        {
            bool execute = false;
            Logger.Log("Starting " + actionName);
            Console.WriteLine("Starting " + actionName);
            foreach(PackageOn o in on)
            {
                App app = null;
                if ((app = DowngradeManager.config.apps.FirstOrDefault(x => (x.canonicalName == o.canonicalName || x.id == o.appId) && x.headset == Headset.RIFT)) != null)
                {
                    Oculus.API.ReleaseChannelReleaseBinary binary = null;
                    if((binary = app.versions.FirstOrDefault(x => o.versions.Contains(x.version) || o.versionCodes.Contains(x.version_code))) != null) // Check against database
                    {
                        Manifest m = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(DowngradeManager.config.oculusSoftwareFolder + "\\Manifests\\" + app.canonicalName + ".json"));
                        if(!o.versionCodes.Contains(m.versionCode) && !o.versions.Contains(m.version)) break; // Check installed
                        string dir = DowngradeManager.config.oculusSoftwareFolder + "\\Software\\" + app.canonicalName + "\\";
                        switch (GetActionType())
                        {
                            case ActionType.FILECOPY:
                                Logger.Log("Starting FileCopy from " + fileInZip + " to " + dir + fileDestination);
                                Console.WriteLine("Starting FileCopy from " + fileInZip + " to " + fileDestination);
                                try
                                {
                                    File.WriteAllBytes(dir + fileDestination, package.GetFile(fileInZip));
                                } catch
                                {
                                    Logger.Log("Copying file failed. Does it exist in the package?");
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("Copying file failed. Does it exist in the package?");
                                    Console.ForegroundColor = ConsoleColor.White;
                                }
                                
                                break;
                            case ActionType.FILEDELETE:
                                Logger.Log("Deleting " + dir + file);
                                Console.WriteLine("Deleting " + file);
                                if(File.Exists(dir + file)) File.Delete(dir + file);
                                Logger.Log("Deleted");
                                Console.WriteLine("Deleted");
                                break;
                            default:
                                Logger.Log("Unknown action type");
                                Console.WriteLine("Unknown action type");
                                return;
                        }
                        Logger.Log("Action completed");
                        Console.WriteLine("Action done");
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
        FILEDELETE
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
            foreach (PackageAction a in metadata.actions) a.ExecuteAction(this);
        }

        public static Package LoadPackage(string path)
        {
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

        public byte[] GetFile(string name)
        {
            Stream s = ZipFile.OpenRead(path).GetEntry(name).Open();
            byte[] b = new byte[s.Length];
            s.Read(b, 0, (int)s.Length);
            s.Close();
            return b;
        }
    }
}