using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oculus.API;
using ComputerUtils.GraphQL;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Net;
using System.IO.Compression;
using System.Threading;
using System.Security.Cryptography;
using ComputerUtils.ADB;
using ComputerUtils.Logging;
using ComputerUtils.ConsoleUi;
using ComputerUtils.FileManaging;

namespace RIFT_Downgrader
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Logger.SetLogFile(AppDomain.CurrentDomain.BaseDirectory + "Log.log");
            SetupExceptionHandlers();
            Logger.LogRaw("\n\n");
            Logger.Log("Starting rift downgrader version " + Updater.version);
            Console.WriteLine("Welcome to the Rift downgrader. Navigate the program by typing the number corresponding to your action and hitting enter. You can always cancel an action by closing the program.");
            if(args.Length == 1 && args[0] == "--update")
            {
                Logger.Log("Starting in update mode");
                Updater u = new Updater();
                u.Update();
                return;
            }
            DowngradeManager m = new DowngradeManager();
            m.Menu();
        }

        public static void SetupExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            HandleExtenption((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                HandleExtenption(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved();
            };
        }

        public static void HandleExtenption(Exception e, string source)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Logger.Log("An unhandled exception has occured:\n" + e.ToString(), LoggingType.Crash);
            Console.WriteLine("\n\nAn unhandled exception has occured. Check the log for more info and send it to ComputerElite for the (probably) bug to get fix. Press any key to close out.");
            Console.ReadKey();
            Logger.Log("Exiting cause of unhandled exception.");
            Environment.Exit(0);
        }
    }

    public class Updater
    {
        public static string version = "1.2.6";
        public bool CheckUpdate()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Checking for updates");
            UpdateEntry latest = GetLatestVersion();
            if (latest.comparedToCurrentVersion == 1) {
                Logger.Log("Update available");
                Console.WriteLine("New update availabel! Current version: " + version + ", latest version: " + latest.Version);
                return true;
            }
            else if(latest.comparedToCurrentVersion == -2)
            {
                Logger.Log("Error while checking for updates", LoggingType.Error);
                Console.WriteLine("An Error occured while checking for updates");
            }
            else if (latest.comparedToCurrentVersion == -1)
            {
                Logger.Log("User on preview version");
                Console.WriteLine("Have fun on a preview version (" + version + "). You can downgrade to the latest stable release (" + latest.Version + ") by pressing enter.");
                return true;
            }
            else
            {
                Logger.Log("User on newest version");
                Console.WriteLine("You are on the newest version");
            }
            return false;
        }

        public UpdateEntry GetLatestVersion()
        {
            try
            {
                Logger.Log("Fetching newest version");
                WebClient c = new WebClient();
                c.Headers.Add("user-agent", "RiftDowngrader/" + version);
                String json = c.DownloadString("https://raw.githubusercontent.com/ComputerElite/Rift-downgrader/main/update.json");
                UpdateFile updates = JsonSerializer.Deserialize<UpdateFile>(json);
                UpdateEntry latest = updates.Updates[0];
                latest.comparedToCurrentVersion = latest.GetVersion().CompareTo(new System.Version(version));
                return latest;
            } catch
            {
                Logger.Log("Fetching of newest version failed", LoggingType.Error);
                return new UpdateEntry();
            }
            
        }

        public void Update()
        {
            Console.WriteLine("Rift downgrader started in update mode. Fetching newest version");
            UpdateEntry e = GetLatestVersion();
            Console.WriteLine("Updating to version " + e.Version + ". Starting download (this may take a few seconds)");
            WebClient c = new WebClient();
            Logger.Log("Downloading update");
            c.DownloadFile(e.Download, DowngradeManager.exe + "update.zip");
            Logger.Log("Unpacking");
            Console.WriteLine("Unpacking update");
            string destDir = new DirectoryInfo(Path.GetDirectoryName(DowngradeManager.exe)).Parent.FullName + "\\";
            using (ZipArchive archive = ZipFile.OpenRead(DowngradeManager.exe + "update.zip"))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    String name = entry.FullName;
                    if (name.EndsWith("/")) continue;
                    if (name.Contains("/")) Directory.CreateDirectory(destDir + System.IO.Path.GetDirectoryName(name));
                    entry.ExtractToFile(destDir + entry.FullName, true);
                }
            }
            File.Delete(DowngradeManager.exe + "update.zip");
            Logger.Log("Update successful");
            Console.WriteLine("Updated to version " + e.Version + ". Changelog:\n" + e.Changelog + "\n\nStart Rift downgrader by pressing any key");
            Console.ReadKey();
            Process.Start(destDir + "RIFT Downgrader.exe");
        }

        public void StartUpdate()
        {
            Logger.Log("Duplicating exe for update");
            Console.WriteLine("Duplicating required files");
            if (Directory.Exists(DowngradeManager.exe + "updater")) Directory.Delete(DowngradeManager.exe + "updater", true);
            Directory.CreateDirectory(DowngradeManager.exe + "updater");
            foreach(string f in Directory.GetFiles(DowngradeManager.exe))
            {
                File.Copy(f, DowngradeManager.exe + "updater\\" + Path.GetFileName(f), true);
            }
            Logger.Log("Starting update. Closing program");
            Console.WriteLine("Starting update.");
            Process.Start(DowngradeManager.exe + "updater\\" + Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location), "--update");
            Environment.Exit(0);
        }
    }

    public class DowngradeManager
    {
        public static string exe = AppDomain.CurrentDomain.BaseDirectory;
        public static string RiftBSAppId = "1304877726278670";
        public static string QuestBSAppId = "2448060205267927";
        public static string RiftPolygonNightmareAppId = "1333056616777885";
        public static Config config = Config.LoadConfig();
        public void Menu()
        {
            SetupProgram();
            while (true)
            {
                Console.WriteLine();
                if(!IsTokenValid(config.access_token)) Console.WriteLine("Hello. For Rift downgrader to function you need to provide your access_token in order to do requests to Oculus and basically use this tool");
                if (!UpdateAccessToken(true))
                {
                    Logger.Log("Access token not provided. You cannot do.", LoggingType.Warning);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Valid access token is needed to proceed. Please try again.");
                } else
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Logger.Log("Showing main menu");
                    Console.WriteLine("[1] Downgrade Beat Saber");
                    Console.WriteLine("[2] Downgrade another " + GetHeadsetDisplayName(config.headset) + " app");
                    Console.WriteLine("[3] " + (config.headset == Headset.RIFT ? "Launch" : "Install") + " App");
                    Console.WriteLine("[4] Open app installation directory");
                    Console.WriteLine("[5] Update access_token");
                    Console.WriteLine("[6] Update oculus folder");
                    Console.WriteLine("[7] Validate installed app");
                    Console.WriteLine("[8] Change Headset (currently " + GetHeadsetDisplayName(config.headset) + ")");
                    Console.WriteLine("[9] Exit");
                    string choice = QuestionString("Choice: ");
                    Logger.Log("User choose option " + choice);
                    switch (choice)
                    {
                        case "1":
                            ShowVersions(config.headset == Headset.RIFT ? RiftBSAppId : QuestBSAppId);
                            break;
                        case "2":
                            StoreSearch();
                            break;
                        case "3":
                            LaunchApp();
                            break;
                        case "4":
                            LaunchApp(true);
                            break;
                        case "5":
                            UpdateAccessToken();
                            break;
                        case "6":
                            CheckOculusFolder(true);
                            break;
                        case "7":
                            ValidateVersionUser();
                            break;
                        case "8":
                            ChangeHeadsetType();
                            break;
                        case "9":
                            Logger.Log("Exiting");
                            System.Environment.Exit(0);
                            break;
                    }
                }
                
            }
        }

        public void ChangeHeadsetType()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Logger.Log("Asking which headset the user wants");
            string choice = QuestionString("Which headset do you want to select? (Quest or Rift): ");
            switch(choice.ToLower())
            {
                case "quest":
                    Logger.Log("Setting headset to Quest");
                    config.headset = Headset.MONTEREY;
                    Console.WriteLine("Set headset to Quest");
                    break;
                case "rift":
                    Logger.Log("Setting headset to Rift");
                    config.headset = Headset.RIFT;
                    Console.WriteLine("Set headset to Rift");
                    break;
                default:
                    Console.WriteLine("This headset does not exist. Not setting");
                    Logger.Log("Headset does not exist. Not setting");
                    break;
            }
            config.Save();
        }

        public string GetHeadsetDisplayName(Headset headset)
        {
            switch(headset)
            {
                case Headset.RIFT:
                    return "Rift";
                case Headset.MONTEREY:
                    return "Quest";
                default:
                    return "unknown";
            }
        }

        public void ValidateVersionUser()
        {
            Console.ForegroundColor = ConsoleColor.White;
            AppReturnVersion selected = SelectFromInstalledApps();
            if (selected.app.headset == Headset.MONTEREY)
            {
                Logger.Log("Cannot validate files of Quest app.", LoggingType.Warning);
                Console.WriteLine("Cannot validate files of Quest app.");
                return;
            }
            Console.WriteLine();
            ValidateVersion(selected);
        }

        public void ValidateVersion(AppReturnVersion selected)
        {
            Console.ForegroundColor = ConsoleColor.White;
            if (selected.app.headset == Headset.MONTEREY)
            {
                Logger.Log("Cannot validate files of Quest app.", LoggingType.Warning);
                Console.WriteLine("Cannot validate files of Quest app.");
                return;
            }
            Logger.Log("Validating files of " + selected.app.name + " version " + selected.version.version);
            Console.WriteLine("Validating files of " + selected.app.name + " version " + selected.version.version);
            Logger.Log("Loading manifest");
            Console.WriteLine("Loading manifest");
            string baseDirectory = exe + "apps\\" + selected.app.id + "\\" + selected.version.id + "\\";
            Manifest manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(baseDirectory + "manifest.json"));
            SHA256 shaCalculator = SHA256.Create();
            int i = 0;
            int valid = 0;
            foreach (KeyValuePair<string, ManifestFile> f in manifest.files)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Logger.Log("Validating " + f.Key);
                Console.WriteLine("Validating " + f.Key);
                if (!File.Exists(baseDirectory + f.Key))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Logger.Log("File does not exist", LoggingType.Warning);
                    Console.WriteLine("File does not exist");
                    continue;
                }
                if (BitConverter.ToString(shaCalculator.ComputeHash(File.ReadAllBytes(baseDirectory + f.Key))).Replace("-", "").ToLower() != f.Value.sha256.ToLower())
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Logger.Log("Hash does not match", LoggingType.Warning);
                    Console.WriteLine("Hash of " + f.Key + " doesn't match with the one in the manifest!");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Logger.Log("Hash checks out");
                    Console.WriteLine("Hash checks out.");
                    valid++;
                }
                i++;
            }
            if (i != valid)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Logger.Log(valid + " out of " + i + " files are valid");
                Console.WriteLine("Only " + valid + " out of " + i + " files are valid! Have you modded any file? You can reinstall the version by simply downloading it again via the tool.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Logger.Log("Game OK");
                Console.WriteLine("Every included file with the game is the one it's intended to be. All files ok");
            }
        }

        public AppReturnVersion SelectFromInstalledApps()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Logger.Log("Showing downloaded apps");
            Console.WriteLine("Downloaded apps:");
            Console.WriteLine();
            Dictionary<string, App> nameApp = new Dictionary<string, App>();
            foreach (App a in config.apps)
            {
                if(a.headset == config.headset)
                {
                    nameApp.Add(a.name.ToLower(), a);
                    Logger.Log("   - " + a.name);
                    Console.WriteLine(a.name);
                } else
                {
                    Logger.Log("Not showing " + a.name + " as it is not for " + GetHeadsetDisplayName(config.headset));
                }
            }
            Console.WriteLine();
            bool choosen = false;
            string sel = "";
            while (!choosen)
            {
                sel = QuestionString("Which app do you want to " + (config.headset == Headset.RIFT ? "launch" : "install") + ": ");
                if (nameApp.ContainsKey(sel.ToLower()))
                {
                    choosen = true;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("That app is not downloaded. Please type the full name displayed above.");
                }
            }
            Logger.Log("User selected " + sel);
            App selected = nameApp[sel.ToLower()];
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Downloaded versions of " + selected.name);
            Logger.Log("Downloaded versions of " + selected.name);

            Dictionary<string, ReleaseChannelReleaseBinary> versionBinary = new Dictionary<string, ReleaseChannelReleaseBinary>();
            foreach (ReleaseChannelReleaseBinary b in selected.versions)
            {
                bool exists = false;
                foreach (ReleaseChannelReleaseBinary e in selected.versions)
                {
                    if (e.version == b.version && e.version_code != b.version_code)
                    {
                        exists = true;
                        break;
                    }
                }
                string displayName = b.version + (exists ? " " + b.version_code : "");
                versionBinary.Add(displayName, b);
                DateTime t = UnixTimeStampToDateTime(b.created_date);
                Logger.Log("   - " + displayName);
                Console.WriteLine(t.Day.ToString("D2") + "." + t.Month.ToString("D2") + "." + t.Year + "     " + displayName);
            }
            choosen = false;
            string ver = "";
            while (!choosen)
            {
                Console.WriteLine();
                ver = QuestionString("Which version do you want?: ");
                if (!versionBinary.ContainsKey(ver))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("This version does not exist.");
                }
                else
                {
                    choosen = true;
                }
            }
            Logger.Log("User choose " + ver);
            ReleaseChannelReleaseBinary selectedVersion = versionBinary[ver];
            Console.ForegroundColor = ConsoleColor.White;
            return new AppReturnVersion(selected, selectedVersion);
        }

        public void LaunchApp(bool openDir = false)
        {
            AppReturnVersion selected = SelectFromInstalledApps();
            Console.ForegroundColor = ConsoleColor.White;
            string baseDirectory = exe + "apps\\" + selected.app.id + "\\" + selected.version.id + "\\";
            if (openDir)
            {
                Logger.Log("Only opening directory of install.");
                Console.WriteLine("Opening directory");
                Process.Start("explorer", "/select," + baseDirectory);
                return;
            }
            if (selected.app.headset == Headset.MONTEREY)
            {
                Logger.Log("Finding downloaded apk in " + baseDirectory);
                Console.WriteLine("Finding downloaded APK");
                string apk = "";
                foreach(string file in Directory.GetFiles(baseDirectory))
                {
                    if(file.ToLower().EndsWith("apk"))
                    {
                        Logger.Log("Found downloaded APK: " + file);
                        Console.WriteLine("Found downloaded APK: " + Path.GetFileName(file));
                        apk = file;
                        break;
                    }
                }
                if(apk == "")
                {
                    Logger.Log("No APK found. Can't install APK");
                    Console.WriteLine("No APK found. Can't install APK");
                    return;
                }
                ADBInteractor interactor = new ADBInteractor();
                Console.WriteLine("Installing apk to Quest if connected (this can take a minute):");
                Logger.Log("Installing apk");
                if(!interactor.adb("install -d \"" + apk + "\""))
                {
                    Logger.Log("Install failed", LoggingType.Warning);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Install failed. See above for more info");
                    return;
                }
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("APK Installed. You should now be able to launch it from your Quest");
                return;
            }
            Logger.Log("Launching selected version");
            Logger.Log("Loading manifest");
            Console.WriteLine("Loading manifest");
            Manifest manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(baseDirectory + "manifest.json"));
            if(!CheckOculusFolder())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Aborting since oculus software folder isn't set.");
                Logger.Log("Aborting since oculus software folder isn't set", LoggingType.Warning);
                return;
            }
            Console.ForegroundColor = ConsoleColor.White;
            string appDir = config.oculusSoftwareFolder + "\\Software\\" + manifest.canonicalName + "\\";
            Logger.Log("Starting app copy to " + appDir);
            Console.WriteLine("Copying application (this can take a few minutes)");
            if (File.Exists(appDir + "manifest.json"))
            {
                Manifest existingManifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(appDir + "manifest.json"));
                if(existingManifest.versionCode == manifest.versionCode)
                {
                    Logger.Log("Version is already copied. Launching");
                    Console.WriteLine("Version is already in the library folder. Launching");
                    Process.Start(appDir + manifest.launchFile, manifest.launchParameters != null ? manifest.launchParameters : "");
                    return;
                } else if(File.Exists(appDir + "RiftDowngrader_appId.txt"))
                {
                    String installedId = File.ReadAllText(appDir + "RiftDowngrader_appId.txt");
                    Logger.Log("Downgraded game already installed. Asking user wether to save the existing install.");
                    string choice = QuestionString("You already have a downgraded game version installed. Do you want me to save the files from " + existingManifest.version + " for next time you launch that version? (Y/n): ");
                    if (choice.ToLower() == "y" || choice == "")
                    {
                        Logger.Log("User wanted to save installed version. Copying");
                        Console.WriteLine("Copying from Oculus to app directory");
                        FileManager.DirectoryCopy(config.oculusSoftwareFolder + "\\Software\\" + manifest.canonicalName, exe + "apps\\" + selected.app.id + "\\" + installedId, true);
                    }
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Finished\n");
                    Console.ForegroundColor = ConsoleColor.White;
                    Logger.Log("Continuing with copy to oculus folder");
                    Console.WriteLine("Copying from app directory to oculus");
                }
            } else
            {
                Logger.Log("Installation not done by rift downgrader has been detected. Asking user if they want to save the installation.");
                string choice = QuestionString("Do you want to backup your current install? (Y/n): ");
                if (choice.ToLower() == "y" || choice == "")
                {
                    Logger.Log("User wanted to save installed version. Copying");
                    Console.WriteLine("Copying from Oculus to app directory");
                    FileManager.DirectoryCopy(config.oculusSoftwareFolder + "\\Software\\" + manifest.canonicalName, exe + "apps\\" + selected.app.id + "\\original_install", true);
                }
            }
            Logger.Log("Copying game");
            FileManager.DirectoryCopy(baseDirectory, config.oculusSoftwareFolder + "\\Software\\" + manifest.canonicalName, true);
            Logger.Log("Copying manifest");
            File.Copy(baseDirectory + "manifest.json", config.oculusSoftwareFolder + "\\Manifests\\" + manifest.canonicalName + ".json", true);
            Logger.Log("Adding minimal manifest");
            File.WriteAllText(config.oculusSoftwareFolder + "\\Manifests\\" + manifest.canonicalName + ".json.mini", JsonSerializer.Serialize(manifest.GetMinimal()));
            Logger.Log("Adding version id into RiftDowngrader_appId.txt");
            File.WriteAllText(appDir + "RiftDowngrader_appId.txt", selected.version.id);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Finished.\nLaunching");
            Logger.Log("Copying finished. Launching.");
            Process.Start(appDir + manifest.launchFile, manifest.launchParameters != null ? manifest.launchParameters : "");
        }

        public bool CheckOculusFolder(bool set = false)
        {
            if(!config.oculusSoftwareFolderSet || set)
            {
                Logger.Log("Asking user for Oculus folder");
                string f = QuestionString("I need to move all the files to your Oculus software folder. " + (set ? "" : "You haven't set it yet.") + "Please enter it now (default: " + config.oculusSoftwareFolder + "): ");
                string before = config.oculusSoftwareFolder;
                config.oculusSoftwareFolder = f == "" ? config.oculusSoftwareFolder : f;
                if (config.oculusSoftwareFolder.EndsWith("\\")) config.oculusSoftwareFolder = config.oculusSoftwareFolder.Substring(0, config.oculusSoftwareFolder.Length - 1);
                if (config.oculusSoftwareFolder.EndsWith("\\Software\\Software")) config.oculusSoftwareFolder = config.oculusSoftwareFolder.Substring(0, config.oculusSoftwareFolder.Length - 9);
                if(!Directory.Exists(config.oculusSoftwareFolder))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("This folder does not exist. Try setting the folder again to a valid folder via the option in the main menu");
                    Logger.Log("User wanted to set a non existent folder as oculus software directory: " + config.oculusSoftwareFolder + ". Falling back to " + before, LoggingType.Warning);
                    config.oculusSoftwareFolder = before;
                    return false;
                }
                if (!Directory.Exists(config.oculusSoftwareFolder + "\\Software"))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("This folder does not contain a Software directory where your games are stored. Did you set it as Oculus library in the Oculus app? If you did make sure you pasted the right path to the folder.");
                    Logger.Log(config.oculusSoftwareFolder + " does not contain Software folder. Falling back to " + before, LoggingType.Warning);
                    config.oculusSoftwareFolder = before;
                    return false;
                }
                if (!Directory.Exists(config.oculusSoftwareFolder + "\\Manifests"))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("This folder does not contain a Manifests directory where your games manifests are stored. Did you set it as Oculus library in the Oculus app? If you did make sure you pasted the right path to the folder.");
                    Logger.Log(config.oculusSoftwareFolder + " does not contain Manifests folder. Falling back to " + before, LoggingType.Warning);
                    config.oculusSoftwareFolder = before;
                    return false;
                }
                config.oculusSoftwareFolderSet = true;
                Logger.Log("Oculus folder set to " + config.oculusSoftwareFolder + ". Saving config");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Saving");
                config.Save();
            }
            return true;
        }

        public void StoreSearch()
        {
            Console.WriteLine();
            Logger.Log("Stating store search. Asking for search term");
            string term = QuestionString("Search term: ");
            Logger.Log("User entered " + term);
            GraphQLClient cl = GraphQLClient.StoreSearch(term, config.headset);
            Console.ForegroundColor = ConsoleColor.White;
            Logger.Log("Requesting results");
            Console.WriteLine("Requesting results");
            StoreSearchResultSkeleton s = JsonSerializer.Deserialize<StoreSearchResultSkeleton>(cl.Request());
            Console.WriteLine();
            Logger.Log("Results: ");
            Console.WriteLine("Results: ");
            Console.WriteLine();
            Dictionary<string, string> nameId = new Dictionary<string, string>();
            /*
            foreach(StoreSearchSearchResult c in s.data.application_search.results.edges)
            {
                Console.WriteLine(c.node.display_name);
                Logger.Log("   - " + c.node.display_name);
                Console.WriteLine(c.node.id);
                if (c.node.display_name.ToLower() == term.ToLower())
                {
                    Logger.Log("Result is exact match. Auto selecting");
                    Console.WriteLine("Result is exact match. Auto selecting");
                    ShowVersions(c.node.id);
                    return;
                }
                nameId.Add(c.node.display_name.ToLower(), c.node.id);
            }
            */
            foreach (StoreSearchResultCategory c in s.data.viewer.contextual_search.all_category_results)
            {
                if (c.name == "APPS")
                {
                    foreach (StoreSearchSearchResult r in c.search_results.nodes)
                    {
                        Console.WriteLine(r.target_object.display_name);
                        Console.WriteLine("   - " + r.target_object.display_name);
                        if (r.target_object.display_name.ToLower() == term.ToLower())
                        {
                            Logger.Log("Result is exact match. Auto selecting");
                            Console.WriteLine("Result is exact match. Auto selecting");
                            ShowVersions(r.target_object.id);
                            return;
                        }
                        nameId.Add(r.target_object.display_name.ToLower(), r.target_object.id);
                    }
                }
            }
            Console.WriteLine();
            bool choosen = false;
            string sel = "";
            if(nameId.Count == 0)
            {
                Logger.Log("No results found");
                Console.WriteLine("No results found");
                return;
            }
            while(!choosen)
            {
                sel = QuestionString("App name: ");
                if(nameId.ContainsKey(sel.ToLower()))
                {
                    choosen = true;
                } else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("That app does not exist in the results. Please type the full name displayed above.");
                }
            }
            Logger.Log("Final selection: " + sel);
            ShowVersions(nameId[sel.ToLower()]);
        }

        public void ShowVersions(string appId)
        {
            Logger.Log("Showing versions for " + appId);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            UndefinedEndProgressBar undefinedEndProgressBar = new UndefinedEndProgressBar();
            undefinedEndProgressBar.Start();
            Logger.Log("Fetching versions");
            List<ReleaseChannelReleaseBinary> versions = new List<ReleaseChannelReleaseBinary>();
            undefinedEndProgressBar.SetupSpinningWheel(500);
            undefinedEndProgressBar.UpdateProgress("Fetching Versions");
            GraphQLClient client = GraphQLClient.VersionHistory(appId);
            VersionHistorySkeleton versionS = JsonSerializer.Deserialize<VersionHistorySkeleton>(client.Request());
            string appName = versionS.data.node.displayName;
            foreach (VersionHistoryVersion v in versionS.data.node.supportedBinaries.edges)
            {
                versions.Add(v.node.ToReleaseChannelReleaseBinary());
            }
            //Logger.Log("Fetching release dates of versions");
            //undefinedEndProgressBar.UpdateProgress("Fetching release dates of versions");
            //client = GraphQLClient.AppRevisions(appId);
            //foreach(AppRevisionsBinary b in JsonSerializer.Deserialize<AppRevisionsSkeleton>(client.Request()).data.node.primary_binaries.nodes)
            //{
                
            //}
            Logger.Log("Fetching versions from online cache");
            undefinedEndProgressBar.UpdateProgress("Fetching versions from online cache");
            WebClient webClient = new WebClient();
            Logger.Log("Requesting apps in cache from https://computerelite.github.io/tools/Oculus/OlderAppVersions/index.json");
            List<IndexEntry> apps = JsonSerializer.Deserialize<List<IndexEntry>>(webClient.DownloadString("https://computerelite.github.io/tools/Oculus/OlderAppVersions/index.json"));
            if(apps.FirstOrDefault(x => x.id == appId) != null)
            {
                Logger.Log("Versions for " + appId + " exist online. Requesting them from https://computerelite.github.io/tools/Oculus/OlderAppVersions/" + appId + ".json and adding.");
                ReleaseChannelReleasesSkeleton s = JsonSerializer.Deserialize<ReleaseChannelReleasesSkeleton>(webClient.DownloadString("https://computerelite.github.io/tools/Oculus/OlderAppVersions/" + appId + ".json"));
                foreach (ReleaseChannelReleaseBinaryNode b in s.data.node.binaries.edges)
                {
                    bool exists = false;
                    for (int i = 0; i < versions.Count; i++)
                    {
                        if (versions[i].id == b.node.id)
                        {
                            versions[i].created_date = b.node.created_date;
                            exists = true;
                        }
                    }
                    if (!exists) versions.Add(b.node);
                }
            } else
            {
                Logger.Log("No online entry existing");
                Console.WriteLine("No online entry existing");
            }
            undefinedEndProgressBar.StopSpinningWheel();
            Console.WriteLine("Date is in format DD-MM-YYYY");
            Logger.Log("Versions of " + appName);
            Console.WriteLine("Versions of " + appName);
            Console.WriteLine();
            versions = versions.OrderBy(b => b.created_date).ToList<ReleaseChannelReleaseBinary>();
            Dictionary<string, ReleaseChannelReleaseBinary> versionBinary = new Dictionary<string, ReleaseChannelReleaseBinary>();
            foreach(ReleaseChannelReleaseBinary b in versions)
            {
                bool exists = false;
                foreach (ReleaseChannelReleaseBinary e in versions)
                {
                    if(e.version == b.version && e.version_code != b.version_code)
                    {
                        exists = true;
                        break;
                    }
                }
                string displayName = b.version + (exists ? " " + b.version_code : "");
                versionBinary.Add(displayName, b);
                DateTime t = UnixTimeStampToDateTime(b.created_date);
                Logger.Log("   - " + displayName);
                Console.WriteLine((b.created_date != 0 ? t.Day.ToString("D2") + "." + t.Month.ToString("D2") + "." + t.Year : "Date not available") + "     " + displayName);
            }
            bool choosen = false;
            string ver = "";
            while(!choosen)
            {
                ver = QuestionString("Which version do you want?: ");
                if (!versionBinary.ContainsKey(ver))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("This version does not exist.");
                } else
                {
                    choosen = true;
                }
            }
            Logger.Log("Selection of user is " + ver);
            ReleaseChannelReleaseBinary selected = versionBinary[ver];
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Console.WriteLine(selected.ToString());
            Console.WriteLine();
            Logger.Log("Asking if user wants to download " + selected.ToString());
            string choice = QuestionString("Do you want to download this version? (Y/n): ");
            if (choice.ToLower() == "y" || choice == "")
            {
                if(Directory.Exists(exe + "apps\\" + appId + "\\" + selected.id))
                {
                    Logger.Log("Version is already downloaded. Asking if user wants to download a second time");
                    choice = QuestionString("Seems like you already have the version " + selected.version + " installed. Do you want to download it again? (y/N): ");
                    if (choice.ToLower() != "y") return;
                    Console.WriteLine("Answer was yes. Deleting existing versions");
                    RecreateDirectoryIfExisting(exe + "apps\\" + appId + "\\" + selected.id);
                }
                Console.WriteLine("Starting download");
                StartDownload(selected, appId, appName);
            } else
            {
                Logger.Log("Downgrading aborted");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Downgrading aborted");
            }
        }

        public string ByteSizeToString(long input, int decimals = 2)
        {
            // TB
            if (input > 1099511627776) return (input / 1099511627776).ToString("D" + decimals) + " TB";
            // GB
            else if (input > 1073741824) return (input / 1073741824).ToString("D" + decimals) + " GB";
            // MB
            else if (input > 1048576) return (input / 1048576).ToString("D" + decimals) + " MB";
            // KB
            else if (input > 1024) return (input / 1024).ToString("D" + decimals) + " KB";
            // Bytes
            else return input + " Bytes";
        }

        public void StartDownload(ReleaseChannelReleaseBinary binary, string appId, string appName)
        {
            Console.ForegroundColor = ConsoleColor.White;
            if (!UpdateAccessToken(true))
            {
                Logger.Log("Access token not provided. aborting.", LoggingType.Warning);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Valid access token is needed to proceed. Aborting.");
                return;
            }
            WebClient downloader = new WebClient();
            if (!File.Exists(exe + "ovr-platform-util.exe"))
            {
                Logger.Log("Downloading ovr-platform-util.exe from https://securecdn.oculus.com/binaries/download/?id=3606802009426978&access_token=OC|1196467420370658|");
                Console.WriteLine("Downloading ovr-platform-util.exe from Oculus");
                DownloadProgressUI downloadProgressUI = new DownloadProgressUI();
                downloadProgressUI.StartDownload("https://securecdn.oculus.com/binaries/download/?id=3606802009426978&access_token=OC|1196467420370658|", exe + "ovr-platform-util.exe");
                Logger.Log("Download finished");
            }
            string baseDirectory = exe + "apps\\" + appId + "\\" + binary.id + "\\";
            string baseDownloadLink = "https://securecdn.oculus.com/binaries/download/?id=" + binary.id + "&access_token=" + config.access_token;
            Logger.Log("Creating " + baseDirectory);
            Directory.CreateDirectory(baseDirectory);
            if (config.headset == Headset.MONTEREY)
            {
                Logger.Log("Starting download of " + appName + " via ovr-platform-util: " + "ovr-platform-util.exe download-mobile-build -b " + binary.id + " -d \"" + baseDirectory.Substring(0, baseDirectory.Length - 1) + "\" -t [token (will not share)]");
                Console.WriteLine("Starting download of " + appName + " via ovr-platform-util");
                Process mp = Process.Start(exe + "ovr-platform-util.exe", "download-mobile-build -b " + binary.id + " -d \"" + baseDirectory.Substring(0, baseDirectory.Length - 1) + "\" -t " + config.access_token);
                mp.WaitForExit();
                Logger.Log("Download finished");
            } else
            {
                baseDownloadLink += "&get_";
                Console.WriteLine();
                Console.WriteLine("Downloading manifest");
                try
                {
                    Logger.Log("Downloading manifest");
                    downloader.DownloadFile(baseDownloadLink + "manifest=1", baseDirectory + "manifest.zip");
                    Logger.Log("Download finished");
                }
                catch
                {
                    Logger.Log("Download of manifest failed. Aborting.", LoggingType.Warning);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine();
                    Console.WriteLine("Download of manifest failed. Do you own this game? If you do then please update your access token in case it's expired");
                    return;
                }
                ZipFile.ExtractToDirectory(baseDirectory + "manifest.zip", baseDirectory);
                Console.WriteLine();
                Logger.Log("Starting download of " + appName + " via ovr-platform-util: " + "ovr-platform-util.exe download-rift-build -b " + binary.id + " -d \"" + baseDirectory.Substring(0, baseDirectory.Length - 1) + "\" -t [token (will not share)]");
                Console.WriteLine("Starting download of " + appName + " via ovr-platform-util");
                Process p = Process.Start(exe + "ovr-platform-util.exe", "download-rift-build -b " + binary.id + " -d \"" + baseDirectory.Substring(0, baseDirectory.Length - 1) + "\" -t " + config.access_token);
                p.WaitForExit();
                Logger.Log("Download finished");

                ValidateVersion(new AppReturnVersion(new App(appName, appId), binary));
            }

            Console.ForegroundColor = ConsoleColor.White;
            Logger.Log("Adding version to config");
            Console.WriteLine("Saving version info");
            bool found = false;
            for(int a = 0; a < config.apps.Count; a++)
            {
                if(config.apps[a].id == appId)
                {
                    found = true;
                    bool exists = false;
                    foreach(ReleaseChannelReleaseBinary b in config.apps[a].versions)
                    {
                        if(b.id == binary.id)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if(!exists) config.apps[a].versions.Add(binary);
                }
            }
            if(!found)
            {
                App a = new App();
                a.name = appName;
                a.id = appId;
                a.headset = config.headset;
                a.versions.Add(binary);
                config.apps.Add(a);
            }
            config.Save();
            Console.ForegroundColor = ConsoleColor.Green;
            Logger.Log("Downgrading finished");
            Console.WriteLine("Finished");
        }

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        public bool UpdateAccessToken(bool onlyIfNeeded = false)
        {
            Console.ForegroundColor = ConsoleColor.White;
            if (IsTokenValid(config.access_token) && onlyIfNeeded)
            {
                GraphQLClient.oculusStoreToken = config.access_token;
                return true;
            }
            Console.WriteLine();
            Logger.Log("Updating access_token");
            if (onlyIfNeeded) Console.WriteLine("Your access_token is needed to authenticate downloads.");
            Logger.Log("Asking user if they want a guide");
            string choice = QuestionString("Do you need a guide on how to get the access token? (Y/n): ");
            Console.ForegroundColor = ConsoleColor.White;
            if (choice.ToLower() == "y" || choice == "")
            {
                //Console.WriteLine("Guide does not exist atm.");
                Logger.Log("Showing guide");
                Process.Start("https://computerelite.github.io/tools/Oculus/ObtainToken.html");
            }
            Console.WriteLine();
            Logger.Log("Asking for access_token");
            Console.WriteLine("Please enter your access_token (it'll be saved locally and is used to authenticate downloads)");
            string at = QuestionString("access_token: ");
            Logger.Log("Removing property name if needed");
            String[] parts = at.Split(':');
            if(parts.Length >= 2)
            {
                at = parts[2];
            }
            at = at.Replace(" ", "");
            if (IsTokenValid(at))
            {
                Logger.Log("Token valid. saving");
                Console.WriteLine("Saving token");
                config.access_token = at;
                config.Save();
                GraphQLClient.oculusStoreToken = config.access_token;
                return true;
            } else
            {
                Logger.Log("Token not valid", LoggingType.Warning);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Token is not valid. Please try getting you access_token with another request described in the guide.");
                return false;
            }
        }

        public bool IsTokenValid(string token)
        {
            //yes this is basic
            Logger.Log("Checking if token matches requirements");
            if (token.StartsWith("OC") && !token.Contains("|")) return true;
            return false;
        }

        public void SetupProgram()
        {
            Logger.Log("Setting up program");
            Console.WriteLine();
            Console.WriteLine("Setting up Program directory");
            Logger.Log("Creating apps dir");
            CreateDirectoryIfNotExisting(exe + "apps");
            Console.WriteLine("Finished");
            Updater u = new Updater();
            if(u.CheckUpdate())
            {
                Logger.Log("Update available. Asking user if they want to update");
                string choice = QuestionString("Do you want to update? (Y/n): ");
                if (choice.ToLower() == "y" || choice == "")
                {
                    u.StartUpdate();
                }
                Logger.Log("Not updating.");
            }
        }

        public string QuestionString(string question)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write(question);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.ForegroundColor = ConsoleColor.White;
            return Console.ReadLine();
        }

        public void CreateDirectoryIfNotExisting(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        public void RecreateDirectoryIfExisting(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
            Directory.CreateDirectory(path);
        }
    }

    public class GitHubTag
    {
        public string name { get; set; } = "";
    }
}
