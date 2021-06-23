using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Oculus.API;

namespace RIFT_Downgrader
{
    public class Config
    {
        public string access_token { get; set; } = "";
        public string oculusSoftwareFolder { get; set; } = "C:\\Program Files\\Oculus\\Software";
        public bool oculusSoftwareFolderSet { get; set; } = false;
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
    }

    public class App
    {
        public string name { get; set; } = "N/A";
        public string id { get; set; } = "";
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