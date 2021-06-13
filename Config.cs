using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Oculus.API;

namespace RIFT_Downgrader
{
    public class Config
    {
        public string access_token { get; set; } = "";
        public List<App> apps { get; set; } = new List<App>();

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
        public List<ReleaseChannelReleaseBinary> versions { get; set; } = new List<ReleaseChannelReleaseBinary>();
    }

    public class AppVersion
    {
        public string id { get; set; } = "";
        public string version { get; set; } = "";
        public string version_code { get; set; } = "";
    }
}