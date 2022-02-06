using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator
{
    public abstract class InventoryBase : IInventoryJobExtension
    {
        protected ILogger logger;

        protected InventoryJobConfiguration JobConfig { get; set; }

        protected string F5Version { get; set; }

        public string ExtensionName => string.Empty;

        public abstract string GetStoreType();

        public abstract JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory);

        protected void ParseJobProperties()
        {
            LogHandlerCommon.MethodEntry(logger, JobConfig.CertificateStoreDetails, "ParseJobProperties");
            dynamic properties = JsonConvert.DeserializeObject(JobConfig.CertificateStoreDetails.Properties.ToString());
            if (string.IsNullOrEmpty(properties.F5Version?.ToString())) { throw new Exception("Missing job property string: F5Version"); }
            F5Version = properties.F5Version.ToString();
            LogHandlerCommon.Trace(logger, JobConfig.CertificateStoreDetails, $"F5 version '{F5Version}'");
        }
    }
}
