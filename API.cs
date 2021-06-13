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
        public string launchFile { get; set; } = "";
        public string launchParameters { get; set; } = null;
        public Dictionary<string, ManifestFile> files { get; set; } = new Dictionary<string, ManifestFile>();
    }

    public class ManifestFile
    {
        public string sha256 { get; set; } = "";
        public long size { get; set; } = 0;
    }
}