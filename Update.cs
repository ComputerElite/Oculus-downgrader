using System;
using System.Collections.Generic;

namespace RIFT_Downgrader
{
    public class UpdateFile
    {
        public List<UpdateEntry> Updates { get; set; } = new List<UpdateEntry>();
    }

    public class UpdateEntry
    {
        public List<string> Creators { get; set; } = new List<string>();
        public string Changelog { get; set; } = "N/A";
        public string Download { get; set; } = "N/A";
        public string Version { get; set; } = "1.0.0";
        public int comparedToCurrentVersion = -2; //0 = same, -1 = earlier, 1 = newer, -2 Error

        public Version GetVersion()
        {
            return new Version(Version);
        }
    }
}