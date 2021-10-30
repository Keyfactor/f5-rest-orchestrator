using CSS.Common.Logging;
using Keyfactor.Platform.Extensions.Agents.Delegates;
using Keyfactor.Platform.Extensions.Agents.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator
{
    public abstract class DiscoveryBase : LoggingClientBase, IAgentJobExtension
    {
        protected AnyJobConfigInfo JobConfig { get; set; }

        public string GetJobClass() { return "Discovery"; }

        public abstract string GetStoreType();

        public abstract AnyJobCompleteInfo processJob(AnyJobConfigInfo config, SubmitInventoryUpdate submitInventory, SubmitEnrollmentRequest submitEnrollmentRequest, SubmitDiscoveryResults sdr);

        protected string DiscoverActiveNode()
        {
            LogHandler.MethodEntry(Logger, JobConfig, "DiscoverActiveNode");
            F5Client f5 = new F5Client(JobConfig);
            string activeNode = f5.GetActiveNode();
            LogHandler.Debug(Logger, JobConfig, $"Active node '{activeNode}'");
            LogHandler.MethodExit(Logger, JobConfig, "DiscoverActiveNode");
            return activeNode;
        }
    }
}
