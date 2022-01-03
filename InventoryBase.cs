using CSS.Common.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator
{
    public abstract class InventoryBase : IInventoryJobExtension
    {
        protected AnyJobConfigInfo JobConfig { get; set; }

        protected string F5Version { get; set; }

        public string ExtensionName => string.Empty;

        public abstract string GetStoreType();

        public abstract JobResult processJob(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory);

        protected void ParseJobProperties()
        {
            LogHandler.MethodEntry(logger, JobConfig, "ParseJobProperties");
            dynamic properties = JsonConvert.DeserializeObject(JobConfig.CertificateStoreDetails.Properties.ToString());
            if (string.IsNullOrEmpty(properties.F5Version?.ToString())) { throw new Exception("Missing job property string: F5Version"); }
            F5Version = properties.F5Version.ToString();
            LogHandler.Trace(logger, JobConfig, $"F5 version '{F5Version}'");
        }
    }
}
