using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Oculus.API;
using OculusGraphQLApiLib;
using OculusGraphQLApiLib.Game;

namespace RIFT_Downgrader
{
    public class Config
    {
        public string access_token { get; set; } = "";
        public string passwordSHA256 { get; set; } = "";
        public string oculusSoftwareFolder { get; set; } = "C:\\Program Files\\Oculus\\Software";
        public bool oculusSoftwareFolderSet { get; set; } = false;
        public int tokenRevision { get; set; } = 1;
        public List<App> apps { get; set; } = new List<App>();
        public Headset headset { get; set; } = Headset.RIFT;

        public static Config LoadConfig()
        {
            if (!File.Exists(DowngradeManager.exe + "config.json")) return new Config();
            return JsonSerializer.Deserialize<Config>(File.ReadAllText(DowngradeManager.exe + "config.json"));
        }

        public void Save()
        {
            File.WriteAllText(DowngradeManager.exe + "config.json", JsonSerializer.Serialize(this));
        }

        public void AddCanonicalNames()
        {
            for(int i = 0; i < apps.Count; i++)
            {
                if(apps[i].canonicalName == "")
                {
                    foreach(App a in apps)
                    {
                        if(File.Exists(DowngradeManager.exe + "apps\\" + apps[i].id + "\\" + apps[i].versions[0].id + "\\manifest.json"))
                        {
                            apps[i].canonicalName = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(DowngradeManager.exe + "apps\\" + apps[i].id + "\\" + apps[i].versions[0].id + "\\manifest.json")).canonicalName;
                            break;
                        }
                    }
                }
            }
            Save();
        }
    }

    public class App
    {
        public string name { get; set; } = "N/A";
        public string id { get; set; } = "";
        public string canonicalName { get; set; } = "";
        public Headset headset { get; set; } = Headset.RIFT;
        public List<ReleaseChannelReleaseBinary> versions { get; set; } = new List<ReleaseChannelReleaseBinary>();
        public App() { }
        public App(string name, string id)
        {
            this.name = name;
            this.id = id;
        }
    }

    public class AppVersion
    {
        public string id { get; set; } = "";
        public string version { get; set; } = "";
        public string version_code { get; set; } = "";
    }

    public class AppReturnVersion
    {
        public App app { get; set; } = new App();
        public ReleaseChannelReleaseBinary version { get; set; } = new ReleaseChannelReleaseBinary();

        public AppReturnVersion(App app, ReleaseChannelReleaseBinary version)
        {
            this.app = app;
            this.version = version;
        }
    }
}