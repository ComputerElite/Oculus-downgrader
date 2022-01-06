using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oculus.API;
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
using Microsoft.Win32;
using ComputerUtils.Encryption;
using ComputerUtils.Updating;
using System.Reflection;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using System.Web;
using OculusGraphQLApiLib.Game;
using OculusGraphQLApiLib;
using OculusGraphQLApiLib.Results;
using ComputerUtils.VarUtils;
using ComputerUtils.CommandLine;

namespace RIFT_Downgrader
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Logger.SetLogFile(AppDomain.CurrentDomain.BaseDirectory + "Log.log");
            SetupExceptionHandlers();
            DowngradeManager.updater = new Updater("1.6.0", "https://github.com/ComputerElite/Rift-downgrader", "Rift Downgrader", Assembly.GetExecutingAssembly().Location);
            Logger.LogRaw("\n\n");
            Logger.Log("Starting rift downgrader version " + DowngradeManager.updater.version);
            if (args.Length == 1 && args[0] == "--update")
            {
                Logger.Log("Starting in update mode");
                DowngradeManager.updater.Update();
                return;
            }

            DowngradeManager.commands = new CommandLineCommandContainer(args);
            DowngradeManager.commands.AddCommandLineArgument(new List<string>() { "--update", "-U" }, true, "Starts in update mode trying to install an update in the parent folder"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string>() { "--noupdatecheck" }, true, "Starts Rift Downgrader without checking for updates"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string>() { "download", "d" }, true, "Starts download of an app/game or at least opens the version page");
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--token" }, false, "Sets the oculus token for Rift Downgrader", "Oculus token"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--savetoken" }, true, "Saves the token provided via --token. Needs --password to encrypt the token"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--password" }, false, "Password to encrypt a token if --savetoken is specified. If no token is specified this password will be used to decrypt the saved token", "password"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--destination" }, false, "Destination to download a game to", "location"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--search", "-s" }, false, "Searches for an app in the oculus store", "query"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--headset", "-h" }, false, "Changes and saves the headset. QUEST and RIFT are supported", "Headset", "RIFT"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--mod", "-m" }, true, "Attempts to mod quest games if you launch them and then installs the modded version"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--continue" }, true, "Allow user input if some arguments are missing. If not pressemt Rift Downgrader will show an Error if you miss an argument");

            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "launch", "l" }, true, "Launches an app/game if downloaded"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--appid" }, false, "Appid of game to download/launch", "appid"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--appname" }, false, "Name of game to launch", "name"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--versioncode" }, false, "VersonCode of the game version to download/launch", "versioncode"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--versionid" }, false, "Id of the game version to download/launch", "versionid"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--versionstring" }, false, "VersionString of the game version to download/launch. Less precise than other version selecting", "versionstring"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--copyold" }, true, "If you want to backup your current install"); // Done

            if (DowngradeManager.commands.HasArgument("help") || DowngradeManager.commands.HasArgument("?") || DowngradeManager.commands.HasArgument("imconfused"))
            {
                DowngradeManager.commands.ShowHelp(DowngradeManager.updater.AppName, "You can't count on me for implementing every argument. Some may be there but some aren't.");
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
            Logger.Log("An unhandled exception has occured:\n" + e.ToString(), LoggingType.Crash);
            DowngradeManager.Error("\n\nAn unhandled exception has occured. Check the log for more info and send it to ComputerElite for the (probably) bug to get fix. Press any key to close out.");
            Console.ReadKey();
            Logger.Log("Exiting cause of unhandled exception.");
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
        public static string password = "";
        public static Updater updater = new Updater();
        public static CommandLineCommandContainer commands = null;
        string qPVersion = "2.2.4";
        string qPDownloadLink = "https://github.com/ComputerElite/QuestPatcherBuilds/releases/download/2.2.4/QuestPatcher.zip";
        public bool first = true;
        public bool auto = false;
        public bool cont = false;

        public static void Error(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void Good(string error)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(error);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public void HandleCLIArgs()
        {
            if (commands.HasArgument("--token"))
            {
                GraphQLClient.oculusStoreToken = commands.GetValue("--token");
                Console.WriteLine("Set token to " + GraphQLClient.oculusStoreToken);
                if(commands.HasArgument("--savetoken") && commands.HasArgument("--password"))
                {
                    if(commands.GetValue("--password").Length < 8)
                    {
                        Error("The password has to be at least 8 characters long. Not saving");
                    } else
                    {
                        if(!SavePasswordAndToken(GraphQLClient.oculusStoreToken, commands.GetValue("--password")))
                        {
                            Error("Issue saving password and token");
                        } else
                        {
                            Good("Saved password and token");
                        }
                    }
                }
                password = "fuck off I don't need a password you idiot";
            }
            cont = commands.HasArgument("--continue");
            Headset headset = Headset.RIFT;
            bool hasHeadset = false;
            if(commands.HasArgument("--headset"))
            {
                hasHeadset = true;
                headset = commands.GetValue("--headset").ToLower() == "quest" ? Headset.MONTEREY : Headset.RIFT;
                config.headset = headset;
                config.Save();
            }
            if (commands.HasArgument("--password"))
            {
                password = commands.GetValue("--password");
                if (!IsPasswordValid(password))
                {
                    Error("Password is invalid. Closing application");
                    Console.ForegroundColor = ConsoleColor.White;
                    Environment.Exit(1);
                }
                DecryptToken();
            }
            if (commands.HasArgument("launch"))
            {
                if ((commands.HasArgument("--appid") || commands.HasArgument("--appname")) && (commands.HasArgument("--versionstring") || commands.HasArgument("--versioncode") || commands.HasArgument("--versionid"))) {
                    foreach(App a in config.apps)
                    {
                        Console.WriteLine(a.name);
                        Console.WriteLine(commands.GetValue("--appname").ToLower());
                        if(a.name.ToLower() == commands.GetValue("--appname").ToLower() || a.id == commands.GetValue("--appid"))
                        {
                            if (hasHeadset && headset != a.headset) continue;
                            foreach(ReleaseChannelReleaseBinary b in a.versions)
                            {
                                Console.WriteLine(b.ToString());
                                if(b.id == commands.GetValue("--versionid") || b.version_code.ToString() == commands.GetValue("--versioncode") || b.version == commands.GetValue("--versionstring"))
                                {
                                    // Matching version, launch it you idiot
                                    auto = true;
                                    LaunchApp(new AppReturnVersion(a, b), false);
                                    Environment.Exit(0);
                                }
                            }
                        }
                    }
                    Error("App not found");
                }
                else
                {
                    Error("You have to have --appid/--appname and --versionstring/--versioncode/--versionid to launch an app");
                }
                Environment.Exit(1);
            }
            if(commands.HasArgument("download"))
            {
                auto = true;
                if((commands.HasArgument("--appname") || commands.HasArgument("--appid")) && (commands.HasArgument("--versionstring") || commands.HasArgument("--versioncode") || commands.HasArgument("--versionid")) || commands.HasArgument("--search") && commands.HasArgument("--"))
                {
                    if(commands.HasArgument("--appid"))
                    {
                        ShowVersions(commands.GetValue("--appid"));
                    }
                    if(commands.HasArgument("--appname") || commands.HasArgument("--search"))
                    {
                        StoreSearch(commands.HasArgument("--appname") ? commands.GetValue("--appname") : commands.GetValue("--search"));
                    }
                    Environment.Exit(0);
                } else
                {
                    Error("You need --appname/--appid/--search and --versionid/--versioncode/--versionstring or --search");
                    Environment.Exit(1);
                }
            }
        }

        public void Menu()
        {
            Console.WriteLine("Welcome to the Rift downgrader. Navigate the program by typing the number corresponding to your action and hitting enter. You can always cancel an action by closing the program.");
            SetupProgram();
            HandleCLIArgs();
            //LoginWithFacebook("secret");
            while (true)
            {
                
                Console.WriteLine();
                //if(!IsTokenValid(config.access_token)) Console.WriteLine("Hello. For Rift downgrader to function you need to provide your access_token in order to do requests to Oculus and basically use this tool");
                if (UpdateAccessToken(true))
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
                    Console.WriteLine("[9] Install Package");
                    Console.WriteLine("[10] Exit");
                    string choice = ConsoleUiController.QuestionString("Choice: ");
                    Logger.Log("User choose option " + choice);
                    switch (choice)
                    {
                        case "1":
                            if (CheckPassword())
                                ShowVersions(config.headset == Headset.RIFT ? RiftBSAppId : QuestBSAppId);
                            break;
                        case "2":
                            if (CheckPassword())
                                StoreSearch();
                            break;
                        case "3":
                            if (CheckPassword())
                                LaunchApp();
                            break;
                        case "4":
                            if (CheckPassword())
                                LaunchApp(true);
                            break;
                        case "5":
                            UpdateAccessToken();
                            break;
                        case "6":
                            CheckOculusFolder(true);
                            break;
                        case "7":
                            if (CheckPassword())
                                ValidateVersionUser();
                            break;
                        case "8":
                            ChangeHeadsetType();
                            break;
                        case "9":
                            InstallPackage();
                            break;
                        case "10":
                            Logger.Log("Exiting");
                            Environment.Exit(0);
                            break;
                    }
                } else
                {
                    Error("Token is needed to continue. Please press any key to exit.");
                    Console.ReadLine();
                    Environment.Exit(0);
                }
            }
        }

        // This almost works. I can get a token but can't sign in to oculus with it.
        public void LoginWithFacebook(string appId)
        {
            Logger.Log("Starting login via Facebook");
            if(!File.Exists(exe + "msedgedriver.exe"))
            {
                Console.WriteLine("Downloading Microsoft edge driver");
                DownloadProgressUI d = new DownloadProgressUI();
                d.StartDownload("https://msedgedriver.azureedge.net/96.0.1054.53/edgedriver_win32.zip", "msedgedriver.zip");
                Logger.Log("Extracting zip");
                Console.WriteLine("Extracting package");
                ZipArchive a = ZipFile.OpenRead("msedgedriver.zip");
                foreach(ZipArchiveEntry e in a.Entries)
                {
                    if(e.Name.EndsWith(".exe"))
                    {
                        e.ExtractToFile("msedgedriver.exe");
                        break;
                    }
                }
                a.Dispose();
                if(!File.Exists("msedgedriver.exe"))
                {
                    Error("Failed to extract Microsoft edge driver. You can't log in with Facebook");
                    Logger.Log("Extract failed");
                    return;
                }
            }
            Console.WriteLine("You have 5 minutes to log in. After that the login window will be closed");
            Console.WriteLine();
            string loginUrl = "https://www.facebook.com/v12.0/dialog/oauth?client_id=" + appId + "&redirect_uri=https://www.facebook.com/connect/login_success.html&state=Login&response_type=token&scope=email,public_profile,user_birthday,user_friends";
            Logger.Log("Navigating Edge driver to " + loginUrl);
            EdgeDriver driver = new EdgeDriver(exe);
            driver.Url = loginUrl;
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromMinutes(5));
            wait.Until(d => d.Url.StartsWith("https://www.facebook.com/connect/login_success.html"));
            string url = driver.Url.Replace("#", "?");
            driver.Quit();
            if(!url.StartsWith("https://www.facebook.com/connect/login_success.html"))
            {
                Error("The login window has been closed due to exceeding 5 minutes. If you wish to try again simpyl select the login option again");
                return;
            }
            string fbToken = HttpUtility.ParseQueryString(new Uri(url).Query.Substring(1)).Get("access_token");
            if(fbToken == null)
            {
                Error("There was an error logging you in.");
                return;
            }
            Logger.Log("Facebook token recieved. Requesting Oculus token");
            Console.WriteLine("Logged into Facebook. I'll now request a token for Oculus");

            Console.WriteLine(fbToken);
            Console.WriteLine();
            Console.WriteLine(url);
        }

        public void InstallPackage()
        {
            config.AddCanonicalNames();
            string package = ConsoleUiController.QuestionString("Drag and drop package and press enter (Packages are not verified. They could contain malware): ").Replace("\"", "");
            Package p = Package.LoadPackage(package);
            if(p == null)
            {
                Logger.Log("Package doesn't contain manifest.json Aborting");
                Error("Package doesn't contain manifest.json Aborting");
                return;
            }
            Logger.Log("Loaded package " + JsonSerializer.Serialize(p));
            string install = ConsoleUiController.QuestionString("Do you want to install " + p.metadata.packageName + " version " + p.metadata.packageVersion + " by " + p.metadata.packageAuthor + " (y/N): ");
            if(install != "y")
            {
                Logger.Log("User aborted package installation");
                Console.WriteLine("Aborted");
                return;
            }
            p.Execute();
        }

        public bool CheckPassword()
        {
            if(password == "")
            {
                if(config.access_token == "")
                {
                    Error("Token is not set");
                    return false;
                }
                Console.WriteLine("Please enter the password you entered when setting your token. If you forgot this password please restart Rift Downgrader and change your token to set a new password.");
                password = ConsoleUiController.SecureQuestionString("password (input hidden): ");
                if(!IsPasswordValid(password))
                {
                    Error("The password is wrong. Please try again or set a new password");
                    password = "";
                    return false;
                }
                
            }
            if (first)
            {
                first = false;
                if(!ShowUsername()) return false;
            }
            return true;
        }

        public void ChangeHeadsetType()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Logger.Log("Asking which headset the user wants");
            string choice = ConsoleUiController.QuestionString("Which headset do you want to select? (Quest or Rift): ");
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
                Console.ForegroundColor= ConsoleColor.DarkYellow;
                Console.WriteLine("Cannot validate files of Quest app.");
                return;
            }
            Console.WriteLine();
            ValidateVersion(selected);
        }

        public void ValidateVersion(AppReturnVersion selected)
        {
            string baseDirectory = exe + "apps\\" + selected.app.id + "\\" + selected.version.id + "\\";
            Validator.ValidateGameInstall(baseDirectory, baseDirectory + "manifest.json");
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
                sel = ConsoleUiController.QuestionString("Which app do you want to " + (config.headset == Headset.RIFT ? "launch" : "install") + ": ");
                if (nameApp.ContainsKey(sel.ToLower()))
                {
                    choosen = true;
                }
                else
                {
                    Error("That app is not downloaded. Please type the full name displayed above.");
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
                DateTime t = TimeConverter.UnixTimeStampToDateTime(b.created_date);
                Logger.Log("   - " + displayName);
                Console.WriteLine(t.Day.ToString("D2") + "." + t.Month.ToString("D2") + "." + t.Year + "     " + displayName);
            }
            choosen = false;
            string ver = "";
            while (!choosen)
            {
                Console.WriteLine();
                ver = ConsoleUiController.QuestionString("Which version do you want?: ");
                if (!versionBinary.ContainsKey(ver))
                {
                    Error("This version does not exist.");
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
            LaunchApp(SelectFromInstalledApps(), openDir);
        }

        public void LaunchApp(AppReturnVersion selected, bool openDir = false)
        {
            
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
                Logger.Log("Searching downloaded apk in " + baseDirectory);
                Console.WriteLine("Searching downloaded APK");
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
                if(apk == "" || new FileInfo(apk).Length < 100)
                {
                    Logger.Log("No APK found. Can't install APK");
                    Console.WriteLine("No APK found. Can't install APK. Please try to download it again");
                    return;
                }

                Logger.Log("Asking if user wants to mod the APK");
                if(commands.HasArgument("--mod") || !auto && ConsoleUiController.QuestionString("Do you want to mod the apk before installing it (QuestPatcher is being used)? (y/N): ") == "y")
                {
                    string qPPath = exe + "QuestPatcher.exe";
                    if (!File.Exists(qPPath) || config.qPVersion != qPVersion)
                    {
                        Logger.Log("QP doesn't exist or is outdated. Downloading required version");
                        DownloadProgressUI d = new DownloadProgressUI();
                        d.StartDownload(qPDownloadLink, qPPath + ".zip");
                        if(!File.Exists(qPPath + ".zip"))
                        {
                            Logger.Log("File failed to download. Returning to Menu");
                            Error("QuestPatcher failed to download. App not installed.");
                            return;
                        }
                        Logger.Log("Extracting archive");
                        foreach(ZipArchiveEntry e in ZipFile.OpenRead(qPPath + ".zip").Entries)
                        {
                            if (e.Name.EndsWith(".exe")) e.ExtractToFile(qPPath);
                        }
                        config.qPVersion = qPVersion;
                        config.Save();
                    }
                    ProcessStartInfo info = new ProcessStartInfo
                    {
                        Arguments = "patch \"" + apk + "\" --handTracking --debuggable -o --resultPath \"" + apk + ".patched.apk\"",
                        FileName = qPPath,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                    };
                    Logger.Log("Starting QuestPatcher with args " + info.Arguments);
                    Console.WriteLine("Starting patching with QuestPatcher. This may take a minute.");
                    Process p = Process.Start(info);
                    while (!p.StandardOutput.EndOfStream)
                    {
                        string o = ((char)p.StandardOutput.Read()).ToString();
                        //Logger.Log(o);
                        Console.Write(o);
                    }
                    p.WaitForExit();
                    Logger.Log("QP exit code: " + p.ExitCode);
                    if(File.Exists(apk + ".patched.apk") && p.ExitCode == 0) apk = apk + ".patched.apk";
                    else
                    {
                        Logger.Log("QuestPatcher exited with exit code " + p.ExitCode + " which is not 0 indicating an error. Vanilla version will be installed.");
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine("QuestPatcher was unable to patch the APK. I'll be installing the vanilla version.");
                    }
                }

                ADBInteractor interactor = new ADBInteractor();
                Console.WriteLine("Installing apk to Quest if connected (this can take a minute):");
                Logger.Log("Installing apk");
                if(!interactor.ForceInstallAPK(apk))
                {
                    Logger.Log("Install failed", LoggingType.Warning);
                    Error("Install failed. See above for more info");
                    return;
                }
                Good("APK Installed. You should now be able to launch it from your Quest");
                return;
            }
            Logger.Log("Launching selected version");
            Logger.Log("Loading manifest");
            Console.WriteLine("Loading manifest");
            Manifest manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(baseDirectory + "manifest.json"));
            if(!CheckOculusFolder())
            {
                Error("Aborting since oculus software folder isn't set.");
                Logger.Log("Aborting since oculus software folder isn't set. Please set it in the main menu", LoggingType.Warning);
                return;
            }
            Console.ForegroundColor = ConsoleColor.White;
            string appDir = config.oculusSoftwareFolder + "\\Software\\" + manifest.canonicalName + "\\";
            Logger.Log("Starting app copy to " + appDir);
            Console.WriteLine("Copying application (this can take a few minutes)");
            if (File.Exists(appDir + "manifest.json"))
            {
                Manifest existingManifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(appDir + "manifest.json"));
                if(existingManifest.versionCode == manifest.versionCode && File.Exists(appDir + manifest.launchFile))
                {
                    Logger.Log("Version is already copied. Launching: " + appDir + manifest.launchFile);
                    Console.WriteLine("Version is already in the library folder. Launching");
                    Process.Start(appDir + manifest.launchFile, (manifest.launchParameters != null ? manifest.launchParameters : "") + " " + selected.version.extraLaunchArgs);
                    return;
                } else if(File.Exists(appDir + "RiftDowngrader_appId.txt"))
                {
                    string installedId = File.ReadAllText(appDir + "RiftDowngrader_appId.txt");
                    Logger.Log("Downgraded game already installed. Asking user wether to save the existing install.");
                    string choice = auto ? (commands.HasArgument("--copyold") ? "y" : "n") : ConsoleUiController.QuestionString("You already have a downgraded game version installed. Do you want me to save the files from " + existingManifest.version + " for next time you launch that version? (Y/n): ");
                    if (choice.ToLower() == "y" || choice == "")
                    {
                        Logger.Log("User wanted to save installed version. Copying");
                        Console.WriteLine("Copying from Oculus to app directory");
                        FileManager.DirectoryCopy(config.oculusSoftwareFolder + "\\Software\\" + manifest.canonicalName, exe + "apps\\" + selected.app.id + "\\" + installedId, true);
                        Good("Finished\n");
                    }
                    Console.ForegroundColor = ConsoleColor.White;
                    Logger.Log("Continuing with copy to oculus folder");
                    Console.WriteLine("Copying from app directory to oculus");
                }
            } else
            {
                Logger.Log("Installation not done by Rift Downgrader has been detected. Asking user if they want to save the installation.");
                string choice = auto ? (commands.HasArgument("--copyold") ? "y" : "n") : ConsoleUiController.QuestionString("Do you want to backup your current install? (Y/n): ");
                if (choice.ToLower() == "y" || choice == "")
                {
                    Logger.Log("User wanted to save installed version. Copying");
                    Console.WriteLine("Copying from Oculus to app directory");
                    FileManager.DirectoryCopy(config.oculusSoftwareFolder + "\\Software\\" + manifest.canonicalName, exe + "apps\\" + selected.app.id + "\\original_install", true);
                }
            }
            Logger.Log("Copying game");
            FileManager.DirectoryCopy(baseDirectory, appDir, true);
            Logger.Log("Copying manifest");
            File.Copy(baseDirectory + "manifest.json", config.oculusSoftwareFolder + "\\Manifests\\" + manifest.canonicalName + ".json", true);
            Logger.Log("Adding minimal manifest");
            File.WriteAllText(config.oculusSoftwareFolder + "\\Manifests\\" + manifest.canonicalName + ".json.mini", JsonSerializer.Serialize(manifest.GetMinimal()));
            Logger.Log("Adding version id into RiftDowngrader_appId.txt");
            File.WriteAllText(appDir + "RiftDowngrader_appId.txt", selected.version.id);
            Good("Finished.\nLaunching");
            Logger.Log("Copying finished. Launching.");
            Process.Start(appDir + manifest.launchFile, (manifest.launchParameters != null ? manifest.launchParameters : "") + " " + selected.version.extraLaunchArgs);
        }

        public bool CheckOculusFolder(bool set = false)
        {
            if(!config.oculusSoftwareFolderSet || set)
            {
                Logger.Log("Asking user for Oculus folder");
                if (!config.oculusSoftwareFolderSet) config.oculusSoftwareFolder = (string)Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus").GetValue("Base") + "Software";
                string f = ConsoleUiController.QuestionString("I need to move all the files to your Oculus software folder. " + (set ? "" : "You haven't set it yet. ") + "Please enter it now (default: " + config.oculusSoftwareFolder + "): ");
                string before = config.oculusSoftwareFolder;
                config.oculusSoftwareFolder = f == "" ? config.oculusSoftwareFolder : f;
                if (config.oculusSoftwareFolder.EndsWith("\\")) config.oculusSoftwareFolder = config.oculusSoftwareFolder.Substring(0, config.oculusSoftwareFolder.Length - 1);
                if (config.oculusSoftwareFolder.EndsWith("\\Software\\Software")) config.oculusSoftwareFolder = config.oculusSoftwareFolder.Substring(0, config.oculusSoftwareFolder.Length - 9);
                if(!Directory.Exists(config.oculusSoftwareFolder))
                {
                    Error("This folder does not exist. Try setting the folder again to a valid folder via the option in the main menu");
                    Logger.Log("User wanted to set a non existent folder as oculus software directory: " + config.oculusSoftwareFolder + ". Falling back to " + before, LoggingType.Warning);
                    config.oculusSoftwareFolder = before;
                    return false;
                }
                if (!Directory.Exists(config.oculusSoftwareFolder + "\\Software"))
                {
                    Error("This folder does not contain a Software directory where your games are stored. Did you set it as Oculus library in the Oculus app? If you did make sure you pasted the right path to the folder.");
                    Logger.Log(config.oculusSoftwareFolder + " does not contain Software folder. Falling back to " + before, LoggingType.Warning);
                    config.oculusSoftwareFolder = before;
                    return false;
                }
                if (!Directory.Exists(config.oculusSoftwareFolder + "\\Manifests"))
                {
                    Error("This folder does not contain a Manifests directory where your games manifests are stored. Did you set it as Oculus library in the Oculus app? If you did make sure you pasted the right path to the folder.");
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

        public void StoreSearch(string autoterm = "")
        {
            Console.WriteLine();
            Logger.Log("Stating store search. Asking for search term");
            string term = auto ? autoterm : ConsoleUiController.QuestionString("Search term: ");
            Logger.Log("User entered " + term);
            Console.ForegroundColor = ConsoleColor.White;
            Logger.Log("Requesting results");
            Console.WriteLine("Requesting results");
            ViewerData<ContextualSearch> s = GraphQLClient.StoreSearch(term, config.headset);
            Console.WriteLine();
            Logger.Log("Results: ");
            Console.WriteLine("Results: ");
            Console.WriteLine();
            Dictionary<string, string> nameId = new Dictionary<string, string>();
            foreach (CategorySearchResult c in s.data.viewer.contextual_search.all_category_results)
            {
                if (c.name == "APPS" || c.name == "CONCEPT")
                {
                    foreach (TargetObject<EdgesPrimaryBinaryApplication> r in c.search_results.nodes)
                    {
                        int increment = 0;
                        while (nameId.ContainsKey(r.target_object.display_name + (increment == 0 ? "" : " " + increment)))
                        {
                            increment++;
                        }
                        string name = r.target_object.display_name + (increment == 0 ? "" : " " + increment);
                        nameId.Add(name.ToLower(), r.target_object.id);
                        Logger.Log("   - " + name);
                        Console.WriteLine("   - " + name);
                        if (name.ToLower() == term.ToLower())
                        {
                            Logger.Log("Result is exact match. Auto selecting");
                            Console.WriteLine("Result is exact match. Auto selecting");
                            ShowVersions(r.target_object.id);
                            return;
                        }
                        
                    }
                }
            }
            Logger.Log("Requesting cache results");
            WebClient client = new WebClient();
            client.Headers.Add("user-agent", updater.AppName + "/" + updater.version);
            List<IndexEntry> apps = JsonSerializer.Deserialize<List<IndexEntry>>(client.DownloadString("https://computerelite.github.io/tools/Oculus/OlderAppVersions/index.json"));
            foreach(IndexEntry e in apps)
            {
                if (!e.name.ToLower().Contains(term.ToLower())) continue;
                if (Enum.GetName(typeof(Headset), config.headset) != e.headset) continue;
                if (nameId.ContainsKey(e.name) && nameId[e.name] == e.id) continue;
                int increment = 0;
                while (nameId.ContainsKey(e.name.ToLower() + (increment == 0 ? "" : " " + increment)))
                {
                    increment++;
                }
                string name = e.name + (increment == 0 ? "" : " " + increment);
                Logger.Log("   - " + name);
                Console.WriteLine("   - " + name);
                if (name.ToLower() == term.ToLower())
                {
                    Logger.Log("Result is exact match. Auto selecting");
                    Console.WriteLine("Result is exact match. Auto selecting");
                    ShowVersions(e.id);
                    return;
                }
                nameId.Add(name.ToLower(), e.id);
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
            if(auto && cont || !auto)
            {
                if (!cont && auto)
                {
                    Error("No app with the name " + term + " found");
                    Environment.Exit(1);
                }
                while (!choosen)
                {
                    sel = ConsoleUiController.QuestionString("App name ('abort' if the app isn't there): ");
                    if (nameId.ContainsKey(sel.ToLower()))
                    {
                        choosen = true;
                    }
                    else
                    {
                        Error("That app does not exist in the results. Please type the full name displayed above.");
                    }
                    if(sel.ToLower() == "abort")
                    {
                        return;
                    }
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
            List<AndroidBinary> versions = new List<AndroidBinary>();
            undefinedEndProgressBar.SetupSpinningWheel(500);
            undefinedEndProgressBar.UpdateProgress("Fetching Versions");
            Data<Application> versionS = GraphQLClient.VersionHistory(appId);
            string appName = versionS.data.node.display_name;
            foreach (Node<AndroidBinary> v in versionS.data.node.supportedBinaries.edges)
            {
                versions.Add(v.node);
            }
            string ver = "";
            Logger.Log("Fetching versions from online cache");
            undefinedEndProgressBar.UpdateProgress("Fetching versions from online cache");
            WebClient webClient = new WebClient();
            Logger.Log("Requesting apps in cache from https://computerelite.github.io/tools/Oculus/OlderAppVersions/index.json");
            List<IndexEntry> apps = JsonSerializer.Deserialize<List<IndexEntry>>(webClient.DownloadString("https://computerelite.github.io/tools/Oculus/OlderAppVersions/index.json"));
            if(apps.FirstOrDefault(x => x.id == appId) != null)
            {
                Logger.Log("Versions for " + appId + " exist online. Requesting them from https://computerelite.github.io/tools/Oculus/OlderAppVersions/" + appId + ".json and adding.");
                Data<ComputersCacheApplication> s = JsonSerializer.Deserialize<Data<ComputersCacheApplication>>(webClient.DownloadString("https://computerelite.github.io/tools/Oculus/OlderAppVersions/" + appId + ".json"));
                foreach (Node<AndroidBinary> b in s.data.node.binaries.edges)
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
            versions = versions.OrderBy(b => b.created_date).ToList<AndroidBinary>();
            Dictionary<string, AndroidBinary> versionBinary = new Dictionary<string, AndroidBinary>();
            foreach(AndroidBinary b in versions)
            {
                bool exists = false;
                foreach (AndroidBinary e in versions)
                {
                    if(e.version == b.version && e.version_code != b.version_code)
                    {
                        exists = true;
                        break;
                    }
                }
                string displayName = b.version + (exists ? " " + b.version_code : "");
                versionBinary.Add(displayName, b);
                DateTime t = TimeConverter.UnixTimeStampToDateTime(b.created_date);
                Logger.Log("   - " + displayName);
                Console.WriteLine((b.created_date != 0 ? t.Day.ToString("D2") + "." + t.Month.ToString("D2") + "." + t.Year : "Date not available") + "     " + displayName);
                if (auto && (commands.GetValue("--versionstring") == b.version || commands.GetValue("--versionid") == b.id || commands.GetValue("--versioncode") == b.versionCode.ToString()))
                {
                    Console.WriteLine("Found version");
                    ver = displayName;
                    break;
                }
            }
            bool choosen = false;
            if(ver == "")
            {
                if(!cont && auto)
                {
                    Error("No version found");
                    Environment.Exit(1);
                }
                while (!choosen)
                {
                    ver = ConsoleUiController.QuestionString("Which version do you want?: ");
                    if (!versionBinary.ContainsKey(ver))
                    {
                        Error("This version does not exist.");
                    }
                    else
                    {
                        choosen = true;
                    }
                }
            }
            
            Logger.Log("Selection of user is " + ver);
            AndroidBinary selected = versionBinary[ver];
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Console.WriteLine(selected.ToString());
            Console.WriteLine();
            Logger.Log("Asking if user wants to download " + selected.ToString());
            string choice = auto ? "y" : ConsoleUiController.QuestionString("Do you want to download this version? (Y/n): ");
            if (choice.ToLower() == "y" || choice == "")
            {
                if(Directory.Exists(exe + "apps\\" + appId + "\\" + selected.id))
                {
                    Logger.Log("Version is already downloaded. Asking if user wants to download a second time");
                    choice = auto ? "y" : ConsoleUiController.QuestionString("Seems like you already have the version " + selected.version + " downloaded. Do you want to download it again? (Y/n): ");
                    if (choice.ToLower() == "n") return;
                    Console.WriteLine("Answer was yes. Deleting existing versions");
                    FileManager.RecreateDirectoryIfExisting(exe + "apps\\" + appId + "\\" + selected.id);
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

        public string DecryptToken()
        {
            return config.access_token.Substring(0, 5) + PasswordEncryption.Decrypt(config.access_token.Substring(5), password);
        }

        public void StartDownload(AndroidBinary binary, string appId, string appName)
        {
            Console.ForegroundColor = ConsoleColor.White;
            if (!UpdateAccessToken(true))
            {
                Logger.Log("Access token not provided. aborting.", LoggingType.Warning);
                Error("Valid access token is needed to proceed. Aborting.");
                return;
            }
            string baseDirectory = commands.HasArgument("--destination") ? commands.GetValue("--destination") : exe + "apps\\" + appId + "\\" + binary.id + "\\";
            Logger.Log("Creating " + baseDirectory);
            Directory.CreateDirectory(baseDirectory);
            bool success = false;
            if (config.headset == Headset.MONTEREY) success = GameDownloader.DownloadMontereyGame(baseDirectory + "app.apk", DecryptToken(), binary.id);
            else success = GameDownloader.DownloadRiftGame(baseDirectory, DecryptToken(), binary.id);
            if(!success)
            {
                Logger.Log("Download failed", LoggingType.Warning);
                Error("Download failed");
                return;
            }
            Console.ForegroundColor = ConsoleColor.White;
            Logger.Log("Adding version to config");
            Console.WriteLine("Saving version info");
            bool found = false;
            for(int aa = 0; aa < config.apps.Count; aa++)
            {
                if(config.apps[aa].id == appId)
                {
                    found = true;
                    bool exists = false;
                    foreach(ReleaseChannelReleaseBinary b in config.apps[aa].versions)
                    {
                        if(b.id == binary.id)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if(!exists) config.apps[aa].versions.Add(ReleaseChannelReleaseBinary.FromAndroidBinary(binary));
                }
            }
            App a = new App();
            a.name = appName;
            a.id = appId;
            a.headset = config.headset;
            a.versions.Add(ReleaseChannelReleaseBinary.FromAndroidBinary(binary));
            if (!found)
            {
                config.apps.Add(a);
            }
            config.Save();
            Console.ForegroundColor = ConsoleColor.Green;
            Logger.Log("Downgrading finished");
            string choice;
            if (config.headset == Headset.RIFT)
            {
                Console.WriteLine("Finished. You can now launch the game from the launch app option in the main menu. It is mandatory to launch it from there so the downgraded game gets copied to the Oculus folder and doesn't fail the entitlement checks.");
                choice = auto ? "n" : ConsoleUiController.QuestionString("Do you want to launch the game now? (Y/n)");
            }
            else
            {
                Console.WriteLine("Finished. You can now install the game from the install app option in the main menu. This is mandatory so that the game gets installed to your quest.");
                choice = auto ? "n" : ConsoleUiController.QuestionString("Do you want to install the game now? (Y/n)");
            }
            if (choice == "n") return;
            LaunchApp(new AppReturnVersion(a, ReleaseChannelReleaseBinary.FromAndroidBinary(binary)));
        }

        public bool UpdateAccessToken(bool onlyIfNeeded = false)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Logger.Log("Updating access_token");

            if (config.tokenRevision != 3)
            {
                Logger.Log("User needs to enter token again. Reason: token has been saved before password SHA256 has been added. Resetting and saving Token.");
                config.access_token = "";
                config.Save();
                Console.WriteLine("You need to enter your access_token again so it can be securely stored");
            }
            else if (onlyIfNeeded) return true;
            if (onlyIfNeeded) Console.WriteLine("Your access_token is needed to authenticate downloads.");
            Logger.Log("Asking user if they want a guide");
            string choice = ConsoleUiController.QuestionString("Do you need a guide on how to get the access token? (Y/n): ");
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
            string at = ConsoleUiController.SecureQuestionString("access_token (hidden): ");
            Logger.Log("Removing property name if needed");
            String[] parts = at.Split(':');
            if(parts.Length >= 2)
            {
                at = parts[1];
            }
            at = at.Replace(" ", "");
            if (TokenTools.IsUserTokenValid(at))
            {
                bool good = false;
                Logger.Log("Token valid. asking for password.");
                Console.WriteLine("You now need to provide a password to encrypt yout token for storing. If you forget this password at any point you just have to provide your Token again.");
                while(!good)
                {
                    password = ConsoleUiController.SecureQuestionString("Password (input hidden): ");
                    if (password.Length < 8)
                    {
                        Error("Please have at least 8 characters for your password.");
                    }
                    else good = true;
                }
                return SavePasswordAndToken(at, password);
            } else
            {
                Logger.Log("Token not valid", LoggingType.Warning);
                Error("Token is not valid. Please try getting you access_token with another request as described in the guide.");
                return false;
            }
        }

        public bool SavePasswordAndToken(string at, string password)
        {
            config.passwordSHA256 = Hasher.GetSHA256OfString(password);
            config.access_token = at.Substring(0, 5) + PasswordEncryption.Encrypt(at.Substring(5), password);
            config.tokenRevision = 3;
            config.Save();
            GraphQLClient.oculusStoreToken = DecryptToken();
            if (!ShowUsername()) return false;
            return true;
        }

        public bool ShowUsername()
        {
            GraphQLClient.oculusStoreToken = DecryptToken();
            Logger.Log("Getting username");
            UndefinedEndProgressBar usernamegetter = new UndefinedEndProgressBar();
            usernamegetter.UpdateProgress("Getting username");
            usernamegetter.StopSpinningWheel();
            try
            {
                ViewerData<OculusUserWrapper> currentUser = GraphQLClient.GetCurrentUser();
                if (currentUser.data.viewer.user == null) throw new Exception("No, your mom");
                Logger.Log("Logged in as " + currentUser.data.viewer.user.alias);
                Console.WriteLine("You are currently logged in as " + currentUser.data.viewer.user.alias);
                return true;
            } catch (Exception ex)
            {
                Logger.Log("Error while requesting Username. Token is probably expired.");
                Error("Error while requesting username. Your token is probably expired. Please update it with option 5 (update access_token) to be able to download games again.");
                return false;
            }
        }

        public bool IsPasswordValid(string password)
        {
            //yes this is basic
            Logger.Log("Checking if password SHA matches saved one");
            if (Hasher.GetSHA256OfString(password) == config.passwordSHA256) return true;
            return false;
        }

        public void SetupProgram()
        {
            Logger.Log("Setting up program");
            Console.WriteLine();
            Console.WriteLine("Setting up Program directory");
            Logger.Log("Creating apps dir");
            FileManager.CreateDirectoryIfNotExisting(exe + "apps");
            Console.WriteLine("Finished");
            if(!commands.HasArgument("--noupdatecheck")) updater.UpdateAssistant();
        }
    }
}
