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
using QuestPatcher.Axml;
using OculusGraphQLApiLib.Folders;
using OculusDB.Database;
using OculusGraphQLApiLib.GraphQL;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace RIFT_Downgrader
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Logger.SetLogFile(AppDomain.CurrentDomain.BaseDirectory + "Log.log");
            SetupExceptionHandlers();
            DowngradeManager.updater = new Updater("1.11.49", "https://github.com/ComputerElite/Oculus-downgrader", "Oculus Downgrader", Assembly.GetExecutingAssembly().Location);
            Logger.LogRaw("\n\n");
            Logger.Log("Starting Oculus Downgrader version " + DowngradeManager.updater.version);
            if (args.Length == 1 && args[0] == "--update")
            {
                Logger.Log("Starting in update mode");
                DowngradeManager.updater.Update();
                return;
            }

            DowngradeManager.commands = new CommandLineCommandContainer(args);
            DowngradeManager.commands.AddCommandLineArgument(new List<string>() { "--update", "-U" }, true, "Starts in update mode trying to install an update in the parent folder"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string>() { "--noupdatecheck", "-nU" }, true, "Starts Oculus downgrader without checking for updates"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string>() { "download", "d" }, true, "Starts download of an app/game or at least opens the version page");
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--token" }, false, "Sets the oculus token for Oculus downgrader", "Oculus token"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--savetoken" }, true, "Saves the token provided via --token. Needs --password to encrypt the token"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--password" }, false, "Password to encrypt a token if --savetoken is specified. If no token is specified this password will be used to decrypt the saved token", "password"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--destination" }, false, "Destination to download a game to", "location"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--search", "-s" }, false, "Searches for an app in the oculus store", "query"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--headset", "-h" }, false, "Changes and saves the headset. QUEST, GEARVR and RIFT are supported", "Headset", "RIFT"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--mod", "-m" }, true, "Attempts to mod quest games if you launch them and then installs the modded version"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--continue" }, true, "Allow user input if some arguments are missing. If not pressemt Oculus downgrader will show an Error if you miss an argument");
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--userexecuted", "--noquit" }, true, "Makes the application not quit after unsucessful attempts");
			DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--skipprompts" }, true, "Skips all user prompts");


			DowngradeManager.commands.AddCommandLineArgument(new List<string> { "launch", "l" }, true, "Launches an app/game if downloaded"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "backup", "b" }, true, "Creates a backup of an app"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--appid" }, false, "Appid of game to download/launch", "appid"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--appname" }, false, "Name of game to launch", "name"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--versioncode" }, false, "VersonCode of the game version to download/launch", "versioncode"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--versionid" }, false, "Id of the game version to download/launch", "versionid"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--versionstring" }, false, "VersionString of the game version to download/launch. Less precise than other version selecting", "versionstring"); // Done
            DowngradeManager.commands.AddCommandLineArgument(new List<string> { "--copyold" }, true, "If you want to backup your current install"); // Done

            if(DowngradeManager.commands.HasArgument("imconfused"))
            {
                Console.WriteLine("How DARE you be confused. Get unconfused! https://youtu.be/TMrtLsQbaok?t=188");
                return;
            }
            if (DowngradeManager.commands.HasArgument("help") || DowngradeManager.commands.HasArgument("?"))
            {
                DowngradeManager.commands.ShowHelp(DowngradeManager.updater.AppName);
                return;
            }
            DowngradeManager m = new DowngradeManager();
            m.Menu();
        }

        public static void SetupExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            HandleException((Exception)e.ExceptionObject);

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                HandleException(e.Exception);
                e.SetObserved();
            };
        }

        public static void HandleException(Exception e)
        {
            Logger.Log("An unhandled exception has occured:\n" + e.ToString(), LoggingType.Crash);
            DowngradeManager.Error("\n\nAn unhandled exception has occured. Check the log for more info and send it to ComputerElite for the (probably) bug to get fixed. Press any key to close out.");
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
        public string qPVersion = "2.2.4";
        public string qPDownloadLink = "https://github.com/ComputerElite/QuestPatcherBuilds/releases/download/2.2.4/QuestPatcher.zip";
        public bool first = true;
        /// <summary>
        /// Wether cli args are used or not
        /// </summary>
        public bool auto = false;
        public bool cont = false;

        public static void Error(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void Good(string text)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public void HandleCLIArgs()
        {
            if (commands.HasArgument("--token"))
            {
                GraphQLClient.userToken = commands.GetValue("--token");
                Console.WriteLine("Set token to " + GraphQLClient.oculusStoreToken);
                password = "fuck off I don't need a password you idiot";
                if(commands.HasArgument("--savetoken") && commands.HasArgument("--password"))
                {
                    if(commands.GetValue("--password").Length < 8)
                    {
                        Error("The password has to be at least 8 characters long. Not saving");
                    } else
                    {
                        if(!SavePasswordAndToken(GraphQLClient.oculusStoreToken, commands.GetValue("--password")))
                        {
                            password = commands.GetValue("--password");
                            Error("Issue saving password and token");
                        } else
                        {
                            Good("Saved password and token");
                        }
                    }
                }
            }
            cont = commands.HasArgument("--continue");
            bool hasHeadset = false;
            if(commands.HasArgument("--headset"))
            {
                hasHeadset = true;
                ChangeHeadsetType(commands.GetValue("--headset"));
                config.Save();
            }
            if (commands.HasArgument("--password"))
            {
                password = commands.GetValue("--password");
                if (!IsPasswordValid(password))
                {
                    Error("Password is invalid. Closing application");
                    Console.ForegroundColor = ConsoleColor.White;
                    Exit(1);
                    return;
                }
                DecryptToken();
            }
            if (commands.HasArgument("launch"))
            {
                if ((commands.HasArgument("--appid") || commands.HasArgument("--appname")) && (commands.HasArgument("--versionstring") || commands.HasArgument("--versioncode") || commands.HasArgument("--versionid"))) {
                    foreach(App a in config.apps)
                    {
                        if(a.name.ToLower() == commands.GetValue("--appname").ToLower() || a.id == commands.GetValue("--appid"))
                        {
                            if (hasHeadset && config.headset != a.headset) continue;
                            foreach(ReleaseChannelReleaseBinary b in a.versions)
                            {
                                if(b.id == commands.GetValue("--versionid") || b.version_code.ToString() == commands.GetValue("--versioncode") || b.version == commands.GetValue("--versionstring"))
                                {
                                    // Matching version, launch it you idiot
                                    auto = true;
                                    LaunchApp(new AppReturnVersion(a, b), false);
                                    Exit(0);
                                    return;
                                }
                            }
                            foreach (string d in Directory.GetDirectories(exe + "apps" + Path.DirectorySeparatorChar + a.id))
                            {
                                string dirName = Path.GetFileName(d);
                                if (dirName.StartsWith("backup") || dirName.StartsWith("original_install"))
                                {
                                    if (dirName == commands.GetValue("--versionid"))
                                    {
                                        // Matching version, launch it you idiot
                                        auto = true;
                                        LaunchApp(new AppReturnVersion(a, new ReleaseChannelReleaseBinary { id = commands.GetValue("--versionid") }), false);
                                        Exit(0);
                                        return;
                                    }
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
                Exit(1);
                return;
            }
            if (commands.HasArgument("backup"))
            {
                if ((commands.HasArgument("--appid") || commands.HasArgument("--appname")))
                {
                    foreach (App a in config.apps)
                    {
                        if (a.name.ToLower() == commands.GetValue("--appname").ToLower() || a.id == commands.GetValue("--appid"))
                        {
                            if (hasHeadset && config.headset != a.headset) continue;
                            // Matching version, launch it you idiot
                            auto = true;
                            CreateBackup(new AppReturnVersion(a, new ReleaseChannelReleaseBinary()));
                            Exit(0);
                            return;
                        }
                    }
                    Error("App not found");
                }
                else
                {
                    Error("You have to have --appid/--appname to launch an app");
                }
                Exit(1);
                return;
            }
            if (commands.HasArgument("download"))
            {
                auto = true;
                if((commands.HasArgument("--appname") || commands.HasArgument("--appid")) && (commands.HasArgument("--versionstring") || commands.HasArgument("--versioncode") || commands.HasArgument("--versionid") || commands.HasArgument("--userexecuted")) || commands.HasArgument("--search") && commands.HasArgument("--"))
                {
                    if(commands.HasArgument("--appid"))
                    {
                        ShowVersions(commands.GetValue("--appid"));
                    }
                    if(commands.HasArgument("--appname") || commands.HasArgument("--search"))
                    {
                        StoreSearch(commands.HasArgument("--appname") ? commands.GetValue("--appname") : commands.GetValue("--search"));
                    }
                    Exit(0);
                    return;
                } else
                {
                    Error("You need --appname/--appid/--search and --versionid/--versioncode/--versionstring or --search");
                    Exit(1);
                    return;
                }
            }
        }

        public void Exit(int code)
        {
            if(commands.HasArgument("--noquit"))
            {
                auto = false;
            } else
            {
                Environment.Exit(code);
            }
        }

        public void Menu()
        {
            Console.WriteLine("Welcome to Oculus downgrader. Navigate the program by typing the number corresponding to your action and hitting enter. You can always cancel an action by closing the program.");
            SetupProgram();

            HandleCLIArgs();
            while (true)
            {
                Console.WriteLine();
                //if(!IsTokenValid(config.access_token)) Console.WriteLine("Hello. For Oculus downgrader to function you need to provide your access_token in order to do requests to Oculus and basically use this tool");
                if (UpdateAccessToken(true))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Logger.Log("Showing main menu");
                    Console.WriteLine("Check if you got the right headset selected (option 8). Rift for Oculus Link, Air link and Rift/Rift s. Quest for Quest 1 and 2");
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("[1]  Downgrade Beat Saber" + (config.headset != Headset.RIFT && config.headset != Headset.HOLLYWOOD ? " (Not available on " + HeadsetTools.GetHeadsetDisplayNameGeneral(config.headset) + ")" : ""));
                    Console.WriteLine("[2]  Downgrade another " + HeadsetTools.GetHeadsetDisplayNameGeneral(config.headset) + " app");
                    Console.WriteLine("[3]  " + HeadsetTools.GetHeadsetInstallActionName(config.headset) + " App");
                    Console.WriteLine("[4]  Open app installation directory (ONLY do if you know what you are doing. If you want to start the app, use option 3)");
                    Console.WriteLine("[5]  Update access_token");
                    Console.WriteLine("[6]  Update oculus folder");
                    Console.WriteLine("[7]  Validate installed app");
                    Console.WriteLine("[8]  Change Headset (currently " + HeadsetTools.GetHeadsetDisplayNameGeneral(config.headset) + ")");
                    Console.WriteLine("[9]  Install Package");
                    Console.WriteLine("[10] Create Backup");
                    Console.WriteLine("[11] Direct execute");
                    Console.WriteLine("[12] Open graphical ui");
                    Console.WriteLine("[13] Settings");
					Console.WriteLine("[14] Exit");
                    string choice = ConsoleUiController.QuestionString("Choice: ");
                    Logger.Log("User choose option " + choice);
                    switch (choice)
                    {
                        case "1":
                            if (config.headset != Headset.RIFT && config.headset != Headset.HOLLYWOOD) continue;
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
                            ChangeHeadsetTypeUser();
                            break;
                        case "9":
                            InstallPackage();
                            break;
                        case "10":
                            CreateBackup();
                            break;
                        case "11":
                            if (!CheckPassword()) break;
                            StartWithArgs();
                            break;
                        case "12":
                            if (!CheckPassword()) break;
                            OculusDB();
                            break;
                        case "13":
                            Settings();
                            break;
						case "14":
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

        public void Settings()
        {
            string choice = ConsoleUiController.ShowMenu(new []
            {
                "Show developer only versions you got access to (currently " + config.requestVersionsFromOculus + ")",
                "Show Oculus token (Do not share)",
                "Export token to OculusDB (this will submit your token to OculusDB's server but it won't get stored. Alternatively save it yourself at the utils page. This will not send it to the server)"
                
            });
            switch(choice)
            {
                case "1":
                    config.requestVersionsFromOculus = !config.requestVersionsFromOculus;
                    config.Save();
                    break;
                case "2":
                    if (!CheckPassword())
                    {
                        Error("You need to enter your password to see your token");
                        break;
                    }
                    Console.WriteLine("Your token is: " + DecryptToken());
                    break;
                case "3":
                    if (!CheckPassword())
                    {
                        Error("You need to enter your password to see your token");
                        break;
                    }

                    string url = "https://oculusdb.rui2015.me/utils?token=" + DecryptToken();
                    ProcessStartInfo info = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = url
                    };
                    Process.Start(info);
                    break;
            }
        }

        public void OculusDB()
        {
            UpdateChrome();
            ChromeDriver driver = new ChromeDriver(exe + "chrome-win64", new ChromeOptions() { });
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromMinutes(1));
            driver.Url = "https://oculusdb.rui2015.me/search?query=Beat%20Saber&isoculusdowngrader=yesofcitis";
            string cmd = "";
            string last = driver.Url;
            while (cmd == "")
            {
                wait = new WebDriverWait(driver, TimeSpan.FromMinutes(10));
                wait.Until(d => d.PageSource.Contains("--appid"));
                cmd = driver.PageSource.Substring(driver.PageSource.IndexOf("d --appid"), driver.PageSource.IndexOf("</code>") - driver.PageSource.IndexOf("d --appid"));
            }
            driver.Close();
            Console.WriteLine(cmd);
            commands.parsedCommand = new ParsedCommand("-nU --userexecuted " + cmd).args.ToArray();
            HandleCLIArgs();
        }

        public void StartWithArgs()
        {
            string codeOrFile = ConsoleUiController.QuestionString("Enter the code or a file path: ");
            string[] args = (File.Exists(codeOrFile.Replace("\"", "")) ? File.ReadAllText(codeOrFile.Replace("\"", "")) : codeOrFile).Split('|');
            foreach(string arg in args)
            {
                commands.parsedCommand = new ParsedCommand("-nU --userexecuted " + arg).args.ToArray();
                HandleCLIArgs();
            }
        }

		public void UpdateChrome()
        {
            if (File.Exists(exe + "chromeversion.txt"))
            {
                if (File.ReadAllText(exe + "chromeversion.txt") == "120") return;
            }
            Logger.Log("Updating Chrome");
            Console.WriteLine("Downloading Chrome driver");
            DownloadProgressUI d = new DownloadProgressUI();
            d.StartDownload("https://edgedl.me.gvt1.com/edgedl/chrome/chrome-for-testing/120.0.6099.109/win64/chrome-win64.zip", "chrome.zip");
            Logger.Log("Extracting zip");
            Console.WriteLine("Extracting package");
            ZipFile.ExtractToDirectory("chrome.zip", ".");
            d.StartDownload("https://edgedl.me.gvt1.com/edgedl/chrome/chrome-for-testing/120.0.6099.109/win64/chromedriver-win64.zip", "driver.zip");
            ZipArchive a = ZipFile.OpenRead("driver.zip");
            foreach (ZipArchiveEntry e in a.Entries)
            {
                if (e.Name.EndsWith(".exe"))
                {
                    e.ExtractToFile("chrome-win64" + Path.DirectorySeparatorChar + "chromedriver.exe", true);
                    break;
                }
            }
            a.Dispose();
            File.WriteAllText(exe + "chromeversion.txt", "120");
        }

        // It just works
        public string LoginWithFacebook()
        {
            UpdateChrome();
            LoginClient client = new LoginClient();
            LoginResponse url = client.StartLogin();


            // Set up Selenium WebDriver with BrowserMob Proxy
            var options = new ChromeOptions();
            options.AddArgument("--remote-debugging-port=9222");
            // Create WebDriver instance
            IWebDriver driver = new ChromeDriver(exe + "chrome-win64", options);

            // Register a request filter to handle custom URI protocol
            driver.Navigate().GoToUrl(url.url);
            Console.WriteLine(driver.Url);
            INetwork network = driver.Manage().Network;
            network.StartMonitoring();
            string at = "";
            bool loggingIn = false;
            network.NetworkResponseReceived += (sender, e) =>
            {
                if (e.ResponseUrl.Contains("approve"))
                {
                    if (loggingIn) return;
                    loggingIn = true;
                    string json = e.ResponseBody.Replace("for (;;);", "").Replace("\\/", "/");
                    Console.WriteLine(json);
                    driver.Close();
                    LoginApproveResponse response = JsonSerializer.Deserialize<LoginApproveResponse>(json);
                    at = client.UriCallback(response);
                }
            };
            while (at == "")
            {
                Thread.Sleep(1000);
            }

            return at;
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
                Console.WriteLine("Please enter the password you entered when setting your token. If you forgot this password please restart Oculus downgrader and change your token to set a new password.");
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

        public void ChangeHeadsetTypeUser()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Logger.Log("Asking which headset the user wants");
            string choice = ConsoleUiController.QuestionString("Which headset do you want to select? (Quest, GearVR, Go or Rift): ");
            ChangeHeadsetType(choice);
            config.Save();
        }

        public void ChangeHeadsetType(string input)
        {
            switch (input.ToLower())
            {
                case "quest":
                    Logger.Log("Setting headset to Quest");
                    config.headset = Headset.HOLLYWOOD;
                    Console.WriteLine("Set headset to Quest");
                    break;
                case "monterey":
                    Logger.Log("Setting headset to Quest");
                    config.headset = Headset.HOLLYWOOD;
                    Console.WriteLine("Set headset to Quest");
                    break;
                case "hollywood":
                    Logger.Log("Setting headset to Quest");
                    config.headset = Headset.HOLLYWOOD;
                    Console.WriteLine("Set headset to Quest");
                    break;
                case "rift":
                    Logger.Log("Setting headset to Rift");
                    config.headset = Headset.RIFT;
                    Console.WriteLine("Set headset to Rift");
                    break;
                case "laguna":
                    Logger.Log("Setting headset to Rift");
                    config.headset = Headset.RIFT;
                    Console.WriteLine("Set headset to Rift");
                    break;
                case "gearvr":
                    Logger.Log("Setting headset to GearVR");
                    config.headset = Headset.GEARVR;
                    Console.WriteLine("Set headset to GearVR");
                    break;
                case "go":
                    Logger.Log("Setting headset to Pacific");
                    config.headset = Headset.PACIFIC;
                    Console.WriteLine("Set headset to Go");
                    break;
                case "pacific":
                    Logger.Log("Setting headset to Pacific");
                    config.headset = Headset.PACIFIC;
                    Console.WriteLine("Set headset to Go");
                    break;
                default:
                    Console.WriteLine("This headset does not exist. Not setting");
                    Logger.Log("Headset does not exist. Not setting");
                    break;
            }
        }

        public void ValidateVersionUser()
        {
            
            if (config.headset == Headset.HOLLYWOOD)
            {
                Logger.Log("Cannot validate files of Quest app.", LoggingType.Warning);
                Console.ForegroundColor= ConsoleColor.DarkYellow;
                Console.WriteLine("Cannot validate files of Quest app.");
                return;
            }
            if (config.headset == Headset.GEARVR)
            {
                Logger.Log("Cannot validate files of GearVR app.", LoggingType.Warning);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("Cannot validate files of GearVR app.");
                return;
            }
            if (config.headset == Headset.PACIFIC)
            {
                Logger.Log("Cannot validate files of Go app.", LoggingType.Warning);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("Cannot validate files of Go app.");
                return;
            }
            Console.ForegroundColor = ConsoleColor.White;
            string choice = ConsoleUiController.QuestionString("Do you want to validate a currently installed version (I) or downloaded version (d)?: ");
            if (choice.ToLower() == "d")
            {
                AppReturnVersion selected = SelectFromInstalledApps(true, "validate");
                Console.WriteLine();
                ValidateVersion(selected);
                return;
            }
            string canonicalName = SelectFromOculusApps("validate");
            if (canonicalName == null)
            {
                Error("Couldn't select app");
                return;
            }
            if (!Validator.ValidateGameInstall(OculusFolder.GetSoftwareDirectory(config.oculusSoftwareFolder,canonicalName), OculusFolder.GetManifestPath(config.oculusSoftwareFolder, canonicalName)))
            {
                choice = ConsoleUiController.QuestionString("As the game is corrupted or modified, do you want to repair it? (Y/n): ");
                if (choice.ToLower() == "n") return;
                Validator.RepairGameInstall(OculusFolder.GetSoftwareDirectory(config.oculusSoftwareFolder, canonicalName), OculusFolder.GetManifestPath(config.oculusSoftwareFolder, canonicalName), DecryptToken());
            }
        }

        public string SelectFromOculusApps(string actionName)
        {
            if (!CheckOculusFolder()) return null;
            if (actionName == "")
            {
                actionName = HeadsetTools.GetHeadsetInstallActionName(config.headset).ToLower();
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Logger.Log("Showing installed oculus apps");
            Console.WriteLine("Installed apps:");
            Console.WriteLine();
            Dictionary<string, string> nameApp = new Dictionary<string, string>();
            foreach (string canonicalName in OculusFolder.GetCanonicalNamesOfInstalledApps(config.oculusSoftwareFolder))
            {
                nameApp.Add(canonicalName, canonicalName);
                Logger.Log("   - " + canonicalName);
                Console.WriteLine(canonicalName);
            }
            Console.WriteLine();
            bool choosen = false;
            string sel = "";
            while (!choosen)
            {
                sel = ConsoleUiController.QuestionString("Which app do you want to " + actionName + " ('cancel' to cancel): ");
                if (nameApp.ContainsKey(sel.ToLower()))
                {
                    choosen = true;
                } else if (sel.ToLower() == "cancel")
                {
                    return null;
                }
                else
                {
                    Error("That app is not installed. Please type the full name displayed above.");
                }
            }
            Logger.Log("User selected " + sel);
            string selected = nameApp[sel.ToLower()];


            return selected;
        }

        public void ValidateVersion(AppReturnVersion selected)
        {
            string baseDirectory = exe + "apps" + Path.DirectorySeparatorChar + selected.app.id + Path.DirectorySeparatorChar + selected.version.id + Path.DirectorySeparatorChar + "";
            if(!Validator.ValidateGameInstall(baseDirectory, baseDirectory + "manifest.json"))
            {
                string choice = ConsoleUiController.QuestionString("As the game is corrupted or modified, do you want to repair it? (Y/n): ");
                if (choice.ToLower() == "n") return;
                Validator.RepairGameInstall(baseDirectory, baseDirectory + "manifest.json", DecryptToken(), selected.version.id);
            }
        }

        public AppReturnVersion SelectFromInstalledApps(bool selectVersion = true, string actionName = "")
        {
            config.AddCanonicalNames();
            if (actionName == "")
            {
                actionName = HeadsetTools.GetHeadsetInstallActionName(config.headset).ToLower();
            }
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
                    Logger.Log("Not showing " + a.name + " as it is not for " + HeadsetTools.GetHeadsetDisplayNameGeneral(config.headset));
                }
            }
            Console.WriteLine();
            bool choosen = false;
            string sel = "";
            while (!choosen)
            {
                sel = ConsoleUiController.QuestionString("Which app do you want to " + actionName + ": ");
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


            if(!selectVersion)
            {
                return new AppReturnVersion(selected, new ReleaseChannelReleaseBinary());
            }


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
            foreach(string d in Directory.GetDirectories(exe + "apps" + Path.DirectorySeparatorChar + selected.id))
            {
                string dirName = Path.GetFileName(d);
                if (dirName.StartsWith("backup") || dirName.StartsWith("original_install"))
                {
                    versionBinary.Add(dirName, new ReleaseChannelReleaseBinary { id = dirName });
                    Logger.Log("   - " + dirName);
                    Console.WriteLine("UNKNOWN DATE" + "   " + dirName);
                }
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
            LaunchApp(SelectFromInstalledApps(true, "open"), openDir);
        }

        public void CreateBackup()
        {
            if(config.headset != Headset.RIFT)
            {
                Error("Can only create backups of Rift apps");
                return;
            }
            CreateBackup(SelectFromInstalledApps(false, "backup"));
        }

        public void LaunchApp(AppReturnVersion selected, bool openDir = false)
        {
            Console.ForegroundColor = ConsoleColor.White;
            string baseDirectory = exe + "apps" + Path.DirectorySeparatorChar + selected.app.id + Path.DirectorySeparatorChar + selected.version.id + Path.DirectorySeparatorChar + "";
            if (openDir)
            {
                Logger.Log("Only opening directory of install.");
                Console.WriteLine("Opening directory");
                Process.Start("explorer", "/select," + baseDirectory);
                return;
            }
            if (selected.app.headset == Headset.HOLLYWOOD || selected.app.headset == Headset.MONTEREY || selected.app.headset == Headset.GEARVR ||selected.app.headset == Headset.PACIFIC)
            {
                Logger.Log("Searching downloaded apk in " + baseDirectory);
                Console.WriteLine("Searching downloaded APK");
                string apk = "";
                if(!Directory.Exists(baseDirectory))
				{
					Logger.Log("apk directory doesn't exist. Can't install apk");
					Error("Version is not downloaded. Please download the version");
                    return;
                }
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
                    Error("No APK found. Can't install APK. Please try to download it again");
                    return;
                }

                Logger.Log("Asking if user wants to mod the APK");
                if(commands.HasArgument("--mod") || (!auto || auto && commands.HasArgument("--userexecuted")) && ConsoleUiController.QuestionString("Do you want to mod the apk before installing it (QuestPatcher is being used)? (y/N): ") == "y")
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
                    if(File.Exists(apk + ".patched.apk") && p.ExitCode == 0) apk += ".patched.apk";
                    else
                    {
                        Logger.Log("QuestPatcher exited with exit code " + p.ExitCode + " which is not 0 indicating an error. Vanilla version will be installed.");
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine("QuestPatcher was unable to patch the APK. I'll be installing the vanilla version.");
                        Console.ForegroundColor= ConsoleColor.White;
                    }
                }

                ADBInteractor interactor = new ADBInteractor();
                interactor.SelectDevice();
                Console.WriteLine("Uninstalling old verson.");
                Logger.Log("uninstalling old version");

                // Get app id
                ZipArchive apkArchive = ZipFile.OpenRead(apk);
                MemoryStream manifestStream = new MemoryStream();
                apkArchive.GetEntry("AndroidManifest.xml").Open().CopyTo(manifestStream);
                manifestStream.Position = 0;
                AxmlElement mamamiaManifest = AxmlLoader.LoadDocument(manifestStream);
                string packageId = "";
                foreach (AxmlAttribute a in mamamiaManifest.Attributes)
                {
                    if(a.Name == "package")
                    {
                        packageId = (string)a.Value;
                        break;
                    }
                }
                List<AndroidUser> users = interactor.SelectUsers("install the game version.");
                if(users.Count <= 0)
                {
                    Error("No user selected. Maybe your Quest isn't connected. Please check the connection.");
                    return;
                }
                apkArchive.Dispose();
                interactor.Uninstall(packageId, users);

                Console.WriteLine("Installing apk to " + HeadsetTools.GetHeadsetDisplayNameGeneral(config.headset) + " if connected (this can take a minute):");
                Logger.Log("Installing apk");
                if(!interactor.InstallAPK(apk, users))
                {
                    Logger.Log("Install failed", LoggingType.Warning);
                    Error("Install failed. See above for more info");
                    return;
                }
                Good("APK installed, now copying obbs");
                if(Directory.Exists(baseDirectory + "obbs"))
                {
                    string[] files = Directory.GetFiles(baseDirectory + "obbs");
                    int done = 0;
                    ProgressBarUI p = new ProgressBarUI();
                    UndefinedEndProgressBar u = new UndefinedEndProgressBar();
                    p.Start();
                    u.Start();
                    p.UpdateProgress(done, files.Length);
                    foreach (string obb in files)
                    {
                        u.UpdateProgress("Pushing " + Path.GetFileName(obb));
                        interactor.Push(obb, "/sdcard/Android/obb/" + packageId + "/" + Path.GetFileName(obb));
                        done++;
                        p.UpdateProgress(done, files.Length);
                    }
                    u.StopSpinningWheel();
                }
                Good("Game Installed. You should now be able to launch it from your Quest");
                return;
            }

            // Rift
            Logger.Log("Launching selected version");
            Logger.Log("Loading manifest");
            Console.WriteLine("Loading manifest");
            if(!File.Exists(baseDirectory + "manifest.json"))
            {
                Logger.Log("Manifest does not exists: This may indicate that you tried to launch a backup made with Oculus Downgrader prior to version 1.9.7 which can't be restored automatically.");
                Error("Manifest does not exist. This may indicate that you tried to launch a backup made with Oculus Downgrader prior to version 1.9.7 which can't be restored automatically.");
                if (ConsoleUiController.QuestionString("Do you want to open the backups folder instead? (Y/n): ").ToLower() == "n") return;
                Process.Start("explorer", "/select," + baseDirectory);
                return;
            }
            Manifest manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(baseDirectory + "manifest.json"));
            if(!CheckOculusFolder())
            {
                Error("Aborting since oculus software folder isn't set.");
                Logger.Log("Aborting since oculus software folder isn't set. Please set it in the main menu", LoggingType.Warning);
                return;
            }
            Console.ForegroundColor = ConsoleColor.White;
            string appDir = OculusFolder.GetSoftwareDirectory(config.oculusSoftwareFolder, manifest.canonicalName);
            Logger.Log("Starting app copy to " + appDir);
            Console.WriteLine("Copying application (this can take a few minutes)");
            if (File.Exists(appDir + "manifest.json"))
            {
                Manifest existingManifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(appDir + "manifest.json"));
                string installedId = File.Exists(appDir + "RiftDowngrader_appId.txt") ? File.ReadAllText(appDir + "RiftDowngrader_appId.txt") : "";
                if (installedId == selected.version.id && File.Exists(appDir + manifest.launchFile))
                {
                    Logger.Log("Version is already copied. Launching: " + appDir + manifest.launchFile);
                    Console.WriteLine("Version is already in the library folder. Launching");
                    Process.Start(new ProcessStartInfo { Arguments = (manifest.launchParameters ?? "") + " " + selected.version.extraLaunchArgs, FileName = appDir + manifest.launchFile, WorkingDirectory = appDir });
                    return;
                } else if(File.Exists(appDir + "RiftDowngrader_appId.txt"))
                {
                    
                    Logger.Log("Downgraded game already installed. Asking user wether to save the existing install.");
                    string choice = auto ? (commands.HasArgument("--copyold") ? "y" : "n") : ConsoleUiController.QuestionString("You already have a downgraded game version installed. Do you want me to save the files from " + existingManifest.version + " for next time you launch that version? (Y/n): ");
                    if (choice.ToLower() == "y" || choice == "")
                    {
                        Logger.Log("User wanted to save installed version. Copying");
                        Console.WriteLine("Copying from Oculus to app directory");
                        FileManager.DirectoryCopy(OculusFolder.GetSoftwareDirectory(config.oculusSoftwareFolder, manifest.canonicalName), exe + "apps" + Path.DirectorySeparatorChar + selected.app.id + Path.DirectorySeparatorChar + installedId, true);
                        File.Copy(OculusFolder.GetManifestPath(config.oculusSoftwareFolder, manifest.canonicalName), exe + "apps" + Path.DirectorySeparatorChar + selected.app.id + Path.DirectorySeparatorChar + installedId + Path.DirectorySeparatorChar + "manifest.json", true);
                        Good("Finished\n");
                    }
                    Console.ForegroundColor = ConsoleColor.White;
                    Logger.Log("Continuing with copy to oculus folder");
                    Console.WriteLine("Copying from app directory to oculus");
                }
            } else if(Directory.Exists(appDir))
            {
                Logger.Log("Installation not done by Oculus downgrader has been detected. Asking user if they want to save the installation.");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("WARNING: The current install will be overridden. To not lose data, create a backup first.");
                string choice = auto ? (commands.HasArgument("--copyold") ? "y" : "n") : ConsoleUiController.QuestionString("Do you want to backup your current install? (Y/n): ");
                if (choice.ToLower() == "y" || choice == "")
                {
                    Logger.Log("User wanted to save installed version. Copying");
                    Console.WriteLine("Copying from Oculus to app directory");
                    CreateBackup(selected, manifest.canonicalName);
                }
            }
            Logger.Log("Copying game");
            FileManager.DirectoryCopy(baseDirectory, appDir, true);
            Logger.Log("Copying manifest");
            File.Copy(baseDirectory + "manifest.json", OculusFolder.GetManifestPath(config.oculusSoftwareFolder, manifest.canonicalName), true);
            Logger.Log("Adding minimal manifest");
            File.WriteAllText(OculusFolder.GetManifestPath(config.oculusSoftwareFolder, manifest.canonicalName) + ".mini", JsonSerializer.Serialize(manifest.GetMinimal()));
            Logger.Log("Adding version id into RiftDowngrader_appId.txt");
            File.WriteAllText(appDir + "RiftDowngrader_appId.txt", selected.version.id);
            Good("Finished.\nLaunching");
            Logger.Log("Copying finished. Launching.");
            Logger.Log(appDir + manifest.launchFile + "   " + baseDirectory);
            Process.Start(new ProcessStartInfo { Arguments = (manifest.launchParameters ?? "") + " " + selected.version.extraLaunchArgs, FileName = appDir + manifest.launchFile, WorkingDirectory = appDir });
        }

        public void CreateBackup(AppReturnVersion selected)
        {
            CreateBackup(selected, selected.app.canonicalName);
        }

        public void CreateBackup(AppReturnVersion selected, string canonicalName)
        {
            Logger.Log(OculusFolder.GetManifestPath(config.oculusSoftwareFolder, canonicalName));
            Manifest manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(OculusFolder.GetManifestPath(config.oculusSoftwareFolder, canonicalName)));
            string appDir = OculusFolder.GetSoftwareDirectory(config.oculusSoftwareFolder, manifest.canonicalName);
            string backupDirName = "backup_" + DateTime.Now.ToString("dd_MM_yyyy__HH_mm_ss");
            string backupDir = exe + "apps" + Path.DirectorySeparatorChar + selected.app.id + Path.DirectorySeparatorChar + backupDirName + Path.DirectorySeparatorChar + "";
            FileManager.DirectoryCopy(appDir, backupDir, true, !auto);
            File.Copy(OculusFolder.GetManifestPath(config.oculusSoftwareFolder, manifest.canonicalName), backupDir + "manifest.json", true);
            if (!auto) Good("Created Backup. You can launch it any time to restore.");
            else Console.WriteLine(backupDirName);
        }

        public bool CheckOculusFolder(bool set = false)
        {
            if(!config.oculusSoftwareFolderSet || set)
            {
                Logger.Log("Asking user for Oculus folder");
                if (!config.oculusSoftwareFolderSet && Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus") != null) config.oculusSoftwareFolder = (string)Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus").GetValue("Base") + "Software";
                string f = ConsoleUiController.QuestionString("I need to move all the files to your Oculus software folder. " + (set ? "" : "You haven't set it yet. ") + "Please enter it now. Don't know what this means? Press enter to use the suggested folder (default: " + config.oculusSoftwareFolder + "): ");
                string before = config.oculusSoftwareFolder;
                config.oculusSoftwareFolder = f == "" ? config.oculusSoftwareFolder : f;
                if (config.oculusSoftwareFolder.EndsWith("" + Path.DirectorySeparatorChar + "")) config.oculusSoftwareFolder = config.oculusSoftwareFolder.Substring(0, config.oculusSoftwareFolder.Length - 1);
                if (config.oculusSoftwareFolder.EndsWith("" + Path.DirectorySeparatorChar + "Software" + Path.DirectorySeparatorChar + "Software")) config.oculusSoftwareFolder = config.oculusSoftwareFolder.Substring(0, config.oculusSoftwareFolder.Length - 9);
                if(!Directory.Exists(config.oculusSoftwareFolder))
                {
                    Error("This folder does not exist. Try setting the folder again to a valid folder via the option in the main menu");
                    Logger.Log("User wanted to set a non existent folder as oculus software directory: " + config.oculusSoftwareFolder + ". Falling back to " + before, LoggingType.Warning);
                    config.oculusSoftwareFolder = before;
                    return false;
                }
                if (!Directory.Exists(config.oculusSoftwareFolder + Path.DirectorySeparatorChar + "Software"))
                {
                    if(config.oculusSoftwareFolder.EndsWith("" + Path.DirectorySeparatorChar + "Software"))
                    {
                        config.oculusSoftwareFolder = FileManager.GetParentDirIfExisting(config.oculusSoftwareFolder);

                    }
                    if(!Directory.Exists(config.oculusSoftwareFolder + Path.DirectorySeparatorChar + "Software"))
                    {
                        Error("This folder does not contain a Software directory where your games are stored. Did you set it as Oculus library in the Oculus app? If you did make sure you pasted the right path to the folder.");
                        Logger.Log(config.oculusSoftwareFolder + " does not contain Software folder. Falling back to " + before, LoggingType.Warning);
                        config.oculusSoftwareFolder = before;
                        return false;
                    }
                }
                if (!Directory.Exists(config.oculusSoftwareFolder + Path.DirectorySeparatorChar + "Manifests"))
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
            Logger.Log("Starting store search. Asking for search term");
            string term = auto ? autoterm : ConsoleUiController.QuestionString("Search term: ");
            Logger.Log("User entered " + term);
            Console.ForegroundColor = ConsoleColor.White;
            
            Dictionary<string, string> nameIdRaw = new Dictionary<string, string>();
            Dictionary<string, string> nameId = new Dictionary<string, string>();
            int failed = 0;
            
            Logger.Log("Requesting results from Oculus");
            Console.WriteLine("Requesting results from Oculus");
            try
            {
                ViewerData<ContextualSearch> s = GraphQLClient.StoreSearch(term, config.headset);
                Console.WriteLine();
                foreach (CategorySearchResult c in s.data.viewer.contextual_search.all_category_results)
                {
                    if (c.name == "APPS" || c.name == "CONCEPT")
                    {
                        foreach (TargetObject<EdgesPrimaryBinaryApplication> r in c.search_results.nodes)
                        {
                            if (!nameIdRaw.ContainsKey(r.target_object.id))
                                nameIdRaw.Add(r.target_object.id, r.target_object.display_name);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Error("Couldn't get results from Oculus");
                Logger.Log("Couldn't get results from Oculus: " + e);
                failed += 1;
            }

            try
            {
                
                Logger.Log("Requesting results from OculusDB");
                Console.WriteLine("Requesting results from OculusDB");
                List<DBApplication> applications = JsonSerializer.Deserialize<List<DBApplication>>(
                    new WebClient().DownloadString("https://oculusdb.rui2015.me/api/v1/search/" + term));
                foreach (DBApplication application in applications)
                {
                    if (!nameIdRaw.ContainsKey(application.id))
                        nameIdRaw.Add(application.id, application.displayName);
                }

            } catch(Exception e)
            {
                Error("Couldn't get results from OculusDB");
                Logger.Log("Couldn't get results from OculusDB: " + e);
                failed += 1;
            }

            if (failed >= 2)
            {
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
            }

            Console.WriteLine();
            Logger.Log("Results: ");
            Console.WriteLine("Results: ");
            foreach (KeyValuePair<string,string> pair in nameIdRaw)
            {
                int increment = 0;
                string name;
                while (nameId.ContainsKey(name = (pair.Value + (increment == 0 ? "" : " " + increment)).ToLower()))
                {
                    increment++;
                }
                
                nameId.Add(name, pair.Key);
                Logger.Log("   - " + name);
                Console.WriteLine("   - " + name);
                if (name.ToLower() == term.ToLower())
                {
                    Logger.Log("Result is exact match. Auto selecting");
                    Console.WriteLine("Result is exact match. Auto selecting");
                    ShowVersions(pair.Key);
                    return;
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
			if (auto && commands.GetValue("--versionid") != "")
			{
				undefinedEndProgressBar.UpdateProgress("Requesting version from Oculus due to version id existing");
                try
                {
                    Data<OculusBinary> hiddenApp = GraphQLClient.GetBinaryDetails(commands.GetValue("--versionid"));
                    undefinedEndProgressBar.StopSpinningWheel();
                    Download(hiddenApp.data.node, appId, hiddenApp.data.node.binary_application.displayName);
                }
                catch (Exception e)
                {
                    undefinedEndProgressBar.UpdateProgress("Request to Oculus failed. Requesting from OculusDB instead. OculusBB may not have every version...");
                    Logger.Log("Request to Oculus failed. Requesting from OculusDB instead. OculusBB may not have every version: " + e);
                    try
                    {
                        DBVersion version = JsonSerializer.Deserialize<DBVersion>(new WebClient().DownloadString("https://oculusdb.rui2015.me/api/v1/id/" + commands.GetValue("--versionid")));
                        
                        undefinedEndProgressBar.StopSpinningWheel();
                        OculusBinary b = new OculusBinary
                            { version = version.version, versionCode = version.versionCode, id = version.id, change_log = version.changeLog};
                        Download(b, appId, version.parentApplication.displayName);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Request to OculusDB failed too. Cannot proceed: " + ex);
                        Error("Request to OculusDB and Oculus failed. Cannot proceed with download");
                        return;
                    }
                }
				return;
			}
			List<OculusBinary> versions = new List<OculusBinary>();
            undefinedEndProgressBar.SetupSpinningWheel(500);
            Logger.Log("Fetching versions from OculusDB");
            undefinedEndProgressBar.UpdateProgress("Fetching versions from OculusDB");
            WebClient webClient = new WebClient();
            string appName = "";
            ConnectedList s = new();
            try
            {
                if(config.requestVersionsFromOculus) throw new Exception("Forced request from Oculus");
                
                Logger.Log("Requesting versions from https://oculusdb.rui2015.me/api/v1/connected/" + appId + " and adding.");
                s = JsonSerializer.Deserialize<ConnectedList>(webClient.DownloadString("https://oculusdb.rui2015.me/api/v1/connected/" + appId));

                appName = s.applications[0].displayName;
                foreach (DBVersion b in s.versions)
                {
                    OculusBinary bin = new OculusBinary
                    {
                        id = b.id,
                        version = b.version,
                        version_code = b.versionCode,
                        created_date = b.created_date,
                        binary_release_channels = new Nodes<ReleaseChannel>()
                    };
                    for (int i = 0; i < b.binary_release_channels.nodes.Count; i++)
                    {
                        bin.binary_release_channels.nodes.Add(new ReleaseChannel { channel_name = b.binary_release_channels.nodes[i].channel_name, id = b.binary_release_channels.nodes[i].id });
                    }
                    versions.Add(bin);
                }

                if (versions.Count <= 0)
                    throw new Exception("The fuck happened, no versions came back from OculusDB!!!");
            }
            catch (Exception e)
            {
                undefinedEndProgressBar.UpdateProgress("Requesting versions from Oculus");
                Logger.Log("Error while requesting versions from OculusDB, falling back to Oculus\n\n" + e, LoggingType.Warning);
                Data<NodesPrimaryBinaryApplication> versionS = GraphQLClient.AllVersionsOfApp(appId);
                appName = versionS.data.node.display_name;
                foreach (OculusBinary v in versionS.data.node.primary_binaries.nodes)
                {
                    versions.Add(v);
                }
            }
            
            string ver = "";
            
            undefinedEndProgressBar.StopSpinningWheel();
            Console.WriteLine("Date is in format DD-MM-YYYY");
            Logger.Log("Versions of " + appName);
            Console.WriteLine("Versions of " + appName);
            Console.WriteLine();
            versions = versions.OrderBy(b => b.version_code).ToList();
            foreach(OculusBinary b in versions)
            {
                bool exists = auto;
                foreach (OculusBinary e in versions)
                {
                    if(e.version == b.version && e.version_code != b.version_code && e.binary_release_channels != null && e.binary_release_channels.nodes != null && e.binary_release_channels.nodes.Count > 0)
                    {
                        exists = true;
                        break;
                    }
                }
                string displayName = b.version + (exists ? "_" + b.version_code : "");
                if (auto && (commands.GetValue("--versionstring") == b.version || commands.GetValue("--versionid") == b.id || commands.GetValue("--versioncode") == b.versionCode.ToString()))
                {
                    Console.WriteLine("Found version");
                    ver = displayName;
                    break;
                }

                if (b.binary_release_channels == null || b.binary_release_channels.nodes == null || b.binary_release_channels.nodes.Count <= 0) continue;
                DateTime t = TimeConverter.UnixTimeStampToDateTime(b.created_date);
                Logger.Log("   - " + displayName);
                string d = (b.created_date != 0 ? t.ToString("dd.MM.yyyy") : "Date not available") + "     " + displayName;
                d = d.PadRight(40);
                d += "Release Channels " + String.Join(", ", b.binary_release_channels.nodes.Select(x => x.channel_name));
                Console.WriteLine(d);

            }
            bool choosen = false;
            OculusBinary selected = new OculusBinary();
            if (ver == "")
            {
                if(!cont && auto && !commands.HasArgument("--userexecuted"))
                {
                    Error("No version found");
                    Environment.Exit(1);
                }
                while (!choosen)
                {
                    ver = ConsoleUiController.QuestionString("Which version do you want?: ");
                    foreach (OculusBinary v in versions)
                    {
                        if ((ver.ToLower().StartsWith(v.version.ToLower()) && (s.versions.FirstOrDefault(x => ver.ToLower().StartsWith(x.version.ToLower()) && x.id != v.id && x.binary_release_channels.nodes.Count > 0) == null || (ver.Trim().Length <= v.versionCode.ToString().Length || v.versionCode.ToString() == ver.Trim().Substring(ver.Trim().Length - v.versionCode.ToString().Length))) || v.id == ver) && v.binary_release_channels.nodes.Count > 0)
                        {
                            selected = v;
                            choosen = true;
                        }
                        
                    }
                    if (!choosen)
                    {
                        Error("This version does not exist.");
                    }
                }
            } else
            {
                foreach (OculusBinary v in versions)
                {
                    if ((ver.ToLower().StartsWith(v.version.ToLower()) && (s.versions.FirstOrDefault(x => ver.ToLower().StartsWith(x.version.ToLower()) && x.id != v.id && x.binary_release_channels.nodes.Count > 0) == null || v.versionCode.ToString() == ver.Trim().Substring(ver.Trim().Length - v.versionCode.ToString().Length)) || v.id == ver) && v.binary_release_channels.nodes.Count > 0)
                    {
                        selected = v;
                        choosen = true;
                    }

                }
            }

			Logger.Log("Selection of user is " + ver);
			Download(selected, appId, appName);
        }

        public void Download(OculusBinary selected, string appId, string appName)
        {
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine();
			Console.WriteLine(selected.ToString());
			Console.WriteLine();
			Logger.Log("Asking if user wants to download " + selected.ToString());
			string choice = auto ? "y" : ConsoleUiController.QuestionString("Do you want to download this version? (Y/n): ");
            if (selected.binary_release_channels != null && selected.binary_release_channels.nodes != null && selected.binary_release_channels.nodes.Count >= 1)
            {
                Logger.Log("Setting selected release channel to " + selected.binary_release_channels.nodes[0].id + " (" + selected.binary_release_channels.nodes[0].channel_name + ")");
                GraphQLClient.ChangeSelectedReleaseChannel(appId, selected.binary_release_channels.nodes[0].id);
            }
			if (choice.ToLower() == "y" || choice == "")
			{
				if (Directory.Exists(exe + "apps" + Path.DirectorySeparatorChar + appId + Path.DirectorySeparatorChar + selected.id))
				{
					Logger.Log("Version is already downloaded. Asking if user wants to download a second time");
					choice = auto ? "y" : (config.headset == Headset.RIFT ? ConsoleUiController.QuestionString("Seems like you already have version " + selected.version + " (partially) downloaded. Do you want to download it again/resume the download? (Y/n): ") : ConsoleUiController.QuestionString("Seems like you already have version " + selected.version + " (partially) downloaded. Do you want to redownload the game? (Y/n): "));
                    if (choice.ToLower() == "n")
                    {
                        Logger.Log("Trying to continue with after download stuff as user did not want to download again");
                        // Get selected app from config
                        App app = config.apps.FirstOrDefault(x => x.id == appId && x.headset == config.headset);
                        if (app == null)
                        {
                            Logger.Log("Could not find selected app in config");
                            Error("Could not find selected app in config");
                            return;
                        }
                        AfterDownload(app, selected);
                        return;
                    }
				}
				Console.WriteLine("Starting download");
				StartDownload(selected, appId, appName);
			}
			else
			{
				Logger.Log("Downgrading aborted");
				Console.ForegroundColor = ConsoleColor.White;
				Console.WriteLine("Downgrading aborted");
			}
		}

        public string DecryptToken()
        {
            if (commands.HasArgument("--token")) return commands.GetValue("--token");
            return config.access_token.Substring(0, 5) + PasswordEncryption.Decrypt(config.access_token.Substring(5), password);
        }

        public void StartDownload(OculusBinary binary, string appId, string appName, bool skipDownload = false)
        {
            // Save version info so it can be validated later even if the download fails
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
            App a = new App() { name = appName, id = appId, headset = config.headset};
            a.versions.Add(ReleaseChannelReleaseBinary.FromAndroidBinary(binary));
            if (!found)
            {
                config.apps.Add(a);
            }
            config.Save();
            
            if(!skipDownload)
            {
                Console.ForegroundColor = ConsoleColor.White;
                if (!UpdateAccessToken(true))
                {
                    Logger.Log("Access token not provided. aborting.", LoggingType.Warning);
                    Error("Valid access token is needed to proceed. Aborting.");
                    return;
                }
                string baseDirectory = commands.HasArgument("--destination") ? commands.GetValue("--destination") : exe + "apps" + Path.DirectorySeparatorChar + appId + Path.DirectorySeparatorChar + binary.id + Path.DirectorySeparatorChar + "";
                Logger.Log("Creating " + baseDirectory);
                Directory.CreateDirectory(baseDirectory);
                bool success;

                if (config.headset == Headset.HOLLYWOOD) success = GameDownloader.DownloadMontereyGame(baseDirectory + "app.apk", DecryptToken(), binary.id);
                else if (config.headset == Headset.GEARVR) success = GameDownloader.DownloadGearVRGame(baseDirectory + "app.apk", DecryptToken(), binary.id);
                else if (config.headset == Headset.PACIFIC) success = GameDownloader.DownloadPacificGame(baseDirectory + "app.apk", DecryptToken(), binary.id);
                else success = GameDownloader.DownloadRiftGameParallel(baseDirectory, DecryptToken(), binary.id);
                if (!success)
                {
                    Logger.Log("Download failed", LoggingType.Warning);
                    Error("Download failed");
                    return;
                }

                if(config.headset != Headset.RIFT)
                {
                    Good("Apk downloaded, now downloading obbs");
                    Console.ForegroundColor = ConsoleColor.White;
                    Logger.Log("Requesting obbs from Oculus");
                    Console.WriteLine("Requesting OBBs from Oculus");
                    try
                    {
                        Data<OculusBinary> b = GraphQLClient.GetBinaryDetails(binary.id);
						List<Obb> obbs = new List<Obb>();
						if (b.data.node.obb_binary != null)
						{
							obbs.Add(new Obb() { filename = b.data.node.obb_binary.file_name, bytes = b.data.node.obb_binary.sizeNumerical, id = b.data.node.obb_binary.id });
						}
                        foreach(AssetFile f in b.data.node.asset_files.nodes)
                        {
                            obbs.Add(new Obb() { filename = f.file_name, bytes = f.sizeNumerical, id = f.id });
						}
						if(obbs.Count <= 0)
						{
							Logger.Log("OBB List has 0 entries. Downloading nothing");
							Console.WriteLine("No obbs to download");
						} else
                        {
							GameDownloader.DownloadObbFiles(baseDirectory + "obbs" + Path.DirectorySeparatorChar, DecryptToken(), obbs);
						}
					} catch(Exception e)
                    {
                            Logger.Log("Couldn't get obbs via fallback. Trying to get from OculusDB next: " + e.ToString(), LoggingType.Warning);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Couldn't get obbs from Oculus. Unknown error. Trying with OculusDB now");
                            try
                            {
                                DBVersion version = JsonSerializer.Deserialize<DBVersion>(
                                    new WebClient().DownloadString("https://oculusdb.rui2015.me/api/v1/id/" +
                                                                   binary.id));
                                if (version.obbList != null)
                                {
                                    List<Obb> obbs = new List<Obb>();
                                    foreach (OBBBinary obbBinary in version.obbList)
                                    {
                                        obbs.Add(new Obb {filename = obbBinary.file_name, bytes = obbBinary.sizeNumerical, id = obbBinary.id});
                                    }
                                    GameDownloader.DownloadObbFiles(baseDirectory + "obbs" + Path.DirectorySeparatorChar, DecryptToken(), obbs);
                                }
                                else
                                {
                                    Logger.Log("OBB List has 0 entries. Checking fallback oculus");
                                    Console.WriteLine("No obbs on OculusDB, checking fallback on Oculus");
                                    try
                                    {
                                        Data<OculusBinary> b = GraphQLClient.GetMoreBinaryDetails(binary.id);
                                        if (b.data.node != null)
                                        {
                                            if (b.data.node.obb_binary == null)
                                            {
                                                Logger.Log("No obbs to download according to fallback");
                                                Console.WriteLine("No obbs to download");
                                            }
                                            else
                                            {
                                                   
                                                List<Obb> obbs = new List<Obb>{new Obb {bytes = b.data.node.obb_binary.sizeNumerical, filename = b.data.node.obb_binary.file_name, id = b.data.node.obb_binary.id}};
                                                GameDownloader.DownloadObbFiles(baseDirectory + "obbs" + Path.DirectorySeparatorChar, DecryptToken(), obbs);
                                            }
                                        }
                                    }
                                    catch (Exception e2)
                                    {
                                        Logger.Log("Fallback failed, we'll just assume there are no obbs to download: " + e2);
                                        Console.Write("Failed to get obbs from OculusDB and Oculus. Assuming there are no obbs to download.");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Log("Request to OculusDB failed too: " + ex);
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("Request to OculusDB and Oculus failed. Could not check if obbs are needed.");
                            }
                        
                    }
                    
                }
            }
            
            AfterDownload(a, binary);
        }

        public void AfterDownload(App a, OculusBinary binary)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Logger.Log("Downgrading finished");
            string choice;
            bool askLaunch = auto && !commands.HasArgument("--userexecuted");
            if (auto && commands.HasArgument("--skipprompts")) askLaunch = true;
            if (config.headset == Headset.RIFT)
            {
                Console.WriteLine("Finished. You can now launch the game from the launch app option in the main menu. It is mandatory to launch it from there so the downgraded game gets copied to the Oculus folder and doesn't fail the entitlement checks.");
                choice = askLaunch ? "n" : ConsoleUiController.QuestionString("Do you want to launch the game now? (Y/n): ");
            }
            else if(config.headset == Headset.HOLLYWOOD)
            {
                Console.WriteLine("Finished. You can now install the game from the install app option in the main menu. This is mandatory so that the game gets installed to your quest.");
                choice = askLaunch ? "n" : ConsoleUiController.QuestionString("Do you want to install the game now? (Y/n): ");
            }
            else
            {
                Console.WriteLine("Finished. You can now install the game from the install app option in the main menu. This is mandatory so that the game gets installed to your phone.");
                choice = askLaunch ? "n" : ConsoleUiController.QuestionString("Do you want to install the game now? (Y/n): ");
            }
            if (choice == "n") return;
            LaunchApp(new AppReturnVersion(a, ReleaseChannelReleaseBinary.FromAndroidBinary(binary)));
        }

        public bool UpdateAccessToken(bool onlyIfNeeded = false)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Logger.Log("Updating access_token");
            if (auto)
            {
                if (!commands.HasArgument("--token") && password == "")
                {
                    Error("You must specify a token with --token or a password to decrypt a saved token with --password");
                    return false;
                }

                return true;
            }

            if (config.tokenRevision != 3)
            {
                Logger.Log("User needs to enter token again. Reason: token has been saved before password SHA256 has been added. Resetting and saving Token.");
                config.access_token = "";
                config.Save();
                Console.WriteLine("You need to enter your access_token again so it can be securely stored");
            }
            else if (onlyIfNeeded) return true;
            if (onlyIfNeeded) Console.WriteLine("Your access_token is needed to authenticate downloads.");
            Logger.Log("Asking user if they want to use the new selenium sign in method.");

            string choice = ConsoleUiController.QuestionString("Do you want to log in with email and password? If logging in didn't work press n. (Y/n): ");
            Console.ForegroundColor = ConsoleColor.White;
            string at;
            if (choice.ToLower() == "y" || choice == "")
            {
                at = LoginWithFacebook();
            } else
            {
                Logger.Log("Asking user if they want a guide");
                choice = ConsoleUiController.QuestionString("Do you need a guide on how to get the access token? (Y/n): ");
                Console.ForegroundColor = ConsoleColor.White;
                if (choice.ToLower() == "y" || choice == "")
                {
                    //Console.WriteLine("Guide does not exist atm.");
                    Console.WriteLine("Open https://computerelite.github.io/tools/Oculus/ObtainTokenNew.html in your browser");
                    Logger.Log("Showing guide");
                    // NET 6 does weird stuff. Can't open a link via Process.Start();
                    //Process.Start("https://computerelite.github.io/tools/Oculus/ObtainTokenNew.html");
                }
                Console.WriteLine();
                Logger.Log("Asking for access_token");
                Console.WriteLine("Please enter your access_token (it'll be saved locally and is used to authenticate downloads)");
                at = ConsoleUiController.SecureQuestionString("access_token (hidden): ");
                Logger.Log("Removing property name if needed");
                String[] parts = at.Split(':');
                if (parts.Length >= 2)
                {
                    at = parts[1];
                }
            }

           
            at = at.Replace(" ", "");
            if (TokenTools.IsUserTokenValid(at))
            {
                bool good = false;
                Logger.Log("Token valid. asking for password.");
                Console.WriteLine("You now need to provide a password to encrypt your token for storing. If you forget this password at any point you just have to provide your Token again.");
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
            GraphQLClient.userToken = DecryptToken();
            if (!ShowUsername()) return false;
            return true;
        }

        public bool ShowUsername()
        {
            GraphQLClient.userToken = DecryptToken();
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
                Logger.Log("Error while requesting Username. Token is probably expired: \n" + ex.ToString());
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
