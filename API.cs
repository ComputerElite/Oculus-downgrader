using System;
using System.Collections.Generic;

namespace Oculus.API
{
    public class ReleaseChannelSkeleton
    {
        public ReleaseChannelNode data { get; set; } = new ReleaseChannelNode(); 
    }

    public class ReleaseChannelNode
    {
        public ReleaseChannelsProperty node { get; set; } = new ReleaseChannelsProperty();
    }

    public class ReleaseChannelsProperty
    {
        public string id { get; set; } = "";
        public string platform { get; set; } = "";
        public ReleaseChannelsList release_channels { get; set; } = new ReleaseChannelsList();
    }

    public class ReleaseChannelsList
    {
        public List<ReleaseChannel> nodes { get; set; } = new List<ReleaseChannel>();
    }

    public class ReleaseChannel
    {
        public string id { get; set; } = "";
        public string channel_name { get; set; } = "";
    }


    public class ReleaseChannelReleasesSkeleton
    {
        public ReleaseChannelReleasesNode data { get; set; } = new ReleaseChannelReleasesNode();
    }

    public class ReleaseChannelReleasesNode
    {
        public ReleaseChannelReleasesProperty node { get; set; } = new ReleaseChannelReleasesProperty();
    }

    public class ReleaseChannelReleasesProperty
    {
        public ReleaseChannelReleaseBinaryList binaries { get; set; } = new ReleaseChannelReleaseBinaryList();
        public ReleaseChannelReleasesLatestBinary latest_supported_binary { get; set; } = new ReleaseChannelReleasesLatestBinary();
    }

    public class ReleaseChannelReleasesLatestBinary
    {
        public ReleaseChannelReleasesLatestBinaryBinaryApplication binary_application { get; set; } = new ReleaseChannelReleasesLatestBinaryBinaryApplication();
    }

    public class ReleaseChannelReleasesLatestBinaryBinaryApplication
    {
        public string display_name { get; set; } = "";
    }

    public class ReleaseChannelReleaseBinaryList
    {
        public List<ReleaseChannelReleaseBinaryNode> edges { get; set; } = new List<ReleaseChannelReleaseBinaryNode>();
    }

    public class ReleaseChannelReleaseBinaryNode
    {
        public ReleaseChannelReleaseBinary node { get; set; } = new ReleaseChannelReleaseBinary();
    }

    public class ReleaseChannelReleaseBinary
    {
        public string change_log { get; set; } = "";
        public string id { get; set; } = "";
        public string version { get; set; } = "";
        public int version_code { get; set; } = 0;
        public long created_date { get; set; } = 0;

        public override string ToString()
        {
            return "Version: " + version + "(" + id + ")\nChangelog: " + change_log;
        }
    }

    public class StoreSearchResultSkeleton
    {
        public StoreSearchResultViewer data { get; set; } = new StoreSearchResultViewer();
    }

    public class StoreSearchResultViewer
    {
        public StoreSearchResultContextSearch viewer { get; set; } = new StoreSearchResultContextSearch();
    }
    public class StoreSearchResultContextSearch
    {
        public StoreSearchResultCategoryList contextual_search { get; set; } = new StoreSearchResultCategoryList();
    }

    public class StoreSearchResultCategoryList
    {
        public List<StoreSearchResultCategory> all_category_results { get; set; } = new List<StoreSearchResultCategory>();
    }

    public class StoreSearchResultCategory
    {
        public string name { get; set; } = "";
        public StoreSearchSearchResultList search_results { get; set; } = new StoreSearchSearchResultList();
    }

    public class StoreSearchSearchResultList
    {
        public List<StoreSearchSearchResult> nodes { get; set; } = new List<StoreSearchSearchResult>();
    }

    public class StoreSearchSearchResult
    {
        public StoreSearchSearchResultTargetObject target_object { get; set; } = new StoreSearchSearchResultTargetObject();
    }

    public class StoreSearchSearchResultTargetObject
    {
        public string id { get; set; } = "";
        public string display_name { get; set; } = "";
    }

    public class Manifest
    {
        public string appId { get; set; } = "";
        public string canonicalName { get; set; } = "";
        public bool isCore { get; set; } = false;
        public string packageType { get; set; } = "APP";
        public string launchFile { get; set; } = "";
        public string launchParameters { get; set; } = "";
        public string launchFile2D { get; set; } = null;
        public string launchParameters2D { get; set; } = "";
        public string version { get; set; } = "1.0";
        public int versionCode { get; set; } = 0;
        public string[] redistributables { get; set; } = new string[0];
        public Dictionary<string, ManifestFile> files { get; set; } = new Dictionary<string, ManifestFile>();
        public bool firewallExceptionsRequired { get; set; } = false;
        public string parentCanonicalName { get; set; } = null;
        public int manifestVersion { get; set; } = 1;

        public Manifest GetMinimal()
        {
            Manifest mini = this;
            KeyValuePair<string, ManifestFile> launchFile = new KeyValuePair<string, ManifestFile>();
            foreach (KeyValuePair<string, ManifestFile> file in this.files)
            {
                if (file.Key == mini.launchFile)
                {
                    launchFile = file;
                    break;
                }
            }
            mini.files = new Dictionary<string, ManifestFile>();
            mini.files.Add(launchFile.Key, launchFile.Value);
            return mini;
        }
    }

    public class ManifestFile
    {
        public string sha256 { get; set; } = "";
        public long size { get; set; } = 0;
        public long segmentSize { get; set; } = 10000000;
        public object[] segments { get; set; } = new object[0];
    }
}