using OculusGraphQLApiLib.Results;
using System;
using System.Collections.Generic;

namespace Oculus.API
{
    public class ReleaseChannelReleaseBinary
    {
        public string change_log { get; set; } = "";
        public string id { get; set; } = "";
        public string version { get; set; } = "";
        public long version_code { get; set; } = 0;
        public long created_date { get; set; } = 0;
        public string extraLaunchArgs { get; set; } = "";

        public static ReleaseChannelReleaseBinary FromAndroidBinary(OculusBinary a)
        {
            ReleaseChannelReleaseBinary b = new ReleaseChannelReleaseBinary();
            b.version = a.version;
            b.version_code = a.versionCode;
            b.id = a.id;
            b.created_date = a.created_date ?? 0;
            b.change_log = a.change_log;
            return b;
        }

        public override string ToString()
        {
            return "Version: " + version + " (" + id + ")\nChangelog: " + change_log;
        }
    }
}