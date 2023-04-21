using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator
{
    #region F5 data models

    internal class F5NodeDevice
    {
        public string name { get; set; }
        public string failoverState { get; set; }
    }

    internal class F5NodeDeviceList
    {
        public F5NodeDevice[] items { get; set; }
    }

    internal class F5PartitionList
    {
        public F5Partition[] items { get; set; }
    }

    internal class F5Partition
    {
        public string name { get; set; }
        public string fullPath { get; set; }
    }

    internal class F5PagedResult
    {
        public int currentItemCount { get; set; }
        public int itemsPerPage { get; set; }
        public int pageIndex { get; set; }
        public int startIndex { get; set; }
        public int totalItems { get; set; }
        public int totalPages { get; set; }
        public string nextLink { get; set; }
    }

    internal class F5PagedCABundles : F5PagedResult
    {
        public F5CABundle[] items { get; set; }
    }

    internal class F5CABundle
    {
        public string name { get; set; }
        public string fullPath { get; set; }
        public string[] includeBundle { get; set; }
    }

    internal class F5PagedSSLProfiles : F5PagedResult
    {
        public F5SSLProfile[] items { get; set; }
    }

    internal class F5SSLProfile
    {
        public string name { get; set; }
        public bool isBundle { get; set; }
        public string keyType { get; set; }
    }

    internal class F5Key
    {
        public string name { get; set; }
    }

    internal class F5PagedLTMSSLProfiles : F5PagedResult
    {
        public F5LTMSSLProfile[] items { get; set; }
    }

    internal class F5LTMSSLProfile
    {
        public string name { get; set; }
        public string partition { get; set; }
        public F5CertificateChain[] certKeyChain { get; set; }
    }

    internal class F5CertificateChain
    {
        public string name { get; set; }
        public string cert { get; set; }
        public string chain { get; set; }
        public string key { get; set; }
    }

    internal class F5InstallCommand
    {
        public string command { get; set; }
        public string name { get; set; }

        [Newtonsoft.Json.JsonProperty("from-local-file")]
        public string localfile { get; set; }

        public string passphrase { get; set; }
        public string partition { get; set; }
    }

    internal class F5BashCommand
    {
        public string command { get; set; }
        public string utilCmdArgs { get; set; }
        public string commandResult { get; set; }
    }

    internal class F5BundleInclude
    {
        public string[] includeBundle { get; set; }
    }

    public class F5Transaction
    {
        public string transid { get; set; }
        public string state { get; set; }
        public int timeout { get; set; }
        public string kind { get; set; }
        public string selfLink { get; set; }
    }

    public class F5CommitTransaction
    {
        public string state { get; set; }
        public bool validateOnly { get; set; }
    }

    public class F5LoginRequest
    {
        public string username { get; set; }
        public string password { get; set; }
        public string loginProviderName { get; set; }
    }

    public class F5LoginResponse
    {
        public F5LoginToken token { get; set; }
    }

    public class F5LoginToken
    {
        public string token { get; set; }
    }

    // F5 data models
    #endregion
}
